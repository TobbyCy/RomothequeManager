using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RomothequeManager.DocCoverage;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonStyle = new() { WriteIndented = true };
    // Named internal contractual backend files are additive to the exported API.
    private static readonly HashSet<string> Critical = new(StringComparer.Ordinal)
    {
        "CatalogStore.cs", "EsDeFingerprintV2.cs", "InventoryReader.cs",
        "InventoryApplicationService.cs", "EsDeCatalogueImporter.cs",
        "MediaIndex.cs", "MediaLinkProjection.cs", "MediaPreviewService.cs",
        "MediaPresentationService.cs", "MiAWindowsMediaScanner.cs",
        "MiBFixtureMediaApplicationService.cs", "RsAWindowsRomScanner.cs",
        "LegacyImportedImageBudget.cs"
    };
    private record Api(string Id, string Kind, string File, int Line,
        bool Documented, string Reason, bool InternalContract);
    private record Measurement(string Sha, int Eligible, int Documented,
        List<Api> Symbols, List<Api> Exclusions)
    {
        public double Percent => Eligible == 0 ? 0 : 100.0 * Documented / Eligible;
    }

    private static int Main(string[] args)
    {
        try
        {
            if (args.SequenceEqual(new[] { "--canaries" })) return RunCanaries();
            if (args.Length != 10 || args[0] != "--root" || args[2] != "--base" ||
                args[4] != "--out" || args[6] != "--head-inputs" || args[8] != "--base-inputs")
                throw new InvalidOperationException("Usage: --root DIR --base SHA --out DIR --head-inputs MSBUILD_MANIFEST --base-inputs MSBUILD_MANIFEST | --canaries");
            var root = Path.GetFullPath(args[1]);
            var baseSha = args[3];
            if (!System.Text.RegularExpressions.Regex.IsMatch(baseSha, "^[0-9a-f]{40}$"))
                throw new InvalidOperationException("BASE_SHA_INVALID");
            var dir = Path.GetFullPath(args[5]);
            var headSha = Git(root, "rev-parse", "HEAD").Trim();
            var sourceSha = Environment.GetEnvironmentVariable("DOC_SOURCE_SHA") ?? headSha;
            if (!System.Text.RegularExpressions.Regex.IsMatch(sourceSha, "^[0-9a-f]{40}$"))
                throw new InvalidOperationException("SOURCE_SHA_INVALID");
            var headInputs = RealProjectInputs.Read(args[7]);
            var baseInputs = RealProjectInputs.Read(args[9]);
            var headCompilation = headInputs.Compile();
            var baseCompilation = baseInputs.Compile();
            var head = Measure(headSha, headCompilation, root);
            var baseline = Measure(baseSha, baseCompilation, root);
            headInputs.VerifyActualAssembly(headCompilation, head.Symbols.Select(x => x.Id));
            baseInputs.VerifyActualAssembly(baseCompilation, baseline.Symbols.Select(x => x.Id));
            Directory.CreateDirectory(dir);
            Save(dir, "head.json", head);
            Save(dir, "base.json", baseline);
            var (pass, added, removed, lostDocs) = EvaluateGate(baseline, head);
            Save(dir, "gate.json", new
            {
                schema = "1.0", headSha, sourceSha, baseSha, headEligible = head.Eligible,
                headDocumented = head.Documented, headPercent = head.Percent,
                baseEligible = baseline.Eligible, baseDocumented = baseline.Documented,
                basePercent = baseline.Percent, addedEligible = added,
                missingNew = added.Where(x => !x.Documented), lostDocs, removedEligible = removed,
                pass, rule = "90% exact ratio, 100% newly eligible, no regression or silent removal"
            });
            var md = new StringBuilder();
            md.AppendLine("# Roslyn XML API coverage")
              .AppendLine().AppendLine("TESTED_TREE: " + headSha + " | SOURCE: " + sourceSha + " | BASE: " + baseSha)
              .AppendLine().AppendLine("HEAD: " + head.Documented + "/" + head.Eligible + " = " + head.Percent.ToString("F2") + "%")
              .AppendLine("BASE: " + baseline.Documented + "/" + baseline.Eligible + " = " + baseline.Percent.ToString("F2") + "%")
              .AppendLine("New: " + added.Length + " | New missing: " + added.Count(x => !x.Documented)
                  + " | Removed eligible: " + removed.Length)
              .AppendLine("GATE: " + (pass ? "PASS" : "FAIL"))
              .AppendLine().AppendLine("## Per source file")
              .AppendLine("| File | Documented | Eligible | Percent |")
              .AppendLine("|---|---:|---:|---:|");
            foreach (var f in head.Symbols.GroupBy(x => x.File).OrderBy(x => x.Key, StringComparer.Ordinal))
                md.AppendLine("| " + f.Key + " | " + f.Count(x => x.Documented) + " | "
                    + f.Count() + " | " + (100.0 * f.Count(x => x.Documented) / f.Count()).ToString("F2") + "% |");
            md.AppendLine().AppendLine("## Missing API documentation");
            foreach (var s in head.Symbols.Where(x => !x.Documented))
                md.AppendLine("- " + s.Id + " at " + s.File + ":" + s.Line + ": " + s.Reason);
            md.AppendLine().AppendLine("## Exclusions (one entry per source symbol)");
            foreach (var s in head.Exclusions)
                md.AppendLine("- " + s.Id + " at " + s.File + ":" + s.Line + ": " + s.Reason);
            File.WriteAllText(Path.Combine(dir, "report.md"), md.ToString(), new UTF8Encoding(false));
            Console.WriteLine("DOC_COVERAGE eligible=" + head.Eligible + " documented=" + head.Documented
                + " percent=" + head.Percent.ToString("F2") + " new=" + added.Length
                + " undocumentedNew=" + added.Count(x => !x.Documented) + " PASS=" + pass);
            return pass ? 0 : 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("DOC_COVERAGE_FAIL " + ex);
            return 2;
        }
    }

    private static Measurement Measure(string sha, IEnumerable<(string Name, string Source)> sources)
    {
        var options = new CSharpParseOptions(LanguageVersion.Preview, DocumentationMode.Diagnose);
        var trees = sources.OrderBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => CSharpSyntaxTree.ParseText(x.Source, options, x.Name)).ToArray();
        if (trees.Length == 0) throw new InvalidOperationException("ELIGIBLE_SOURCE_SET_EMPTY");
        // Production application is built natively before this analysis. Analyzing
        // declarations with Roslyn, rather than counting /// or CS1591 diagnostics.
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists).Select(p => MetadataReference.CreateFromFile(p));
        var compilation = CSharpCompilation.Create("RomothequeManager.V2",
            trees, refs, new CSharpCompilationOptions(OutputKind.WindowsApplication,
                nullableContextOptions: NullableContextOptions.Enable));
        var eligible = new Dictionary<string, Api>(StringComparer.Ordinal);
        var excluded = new Dictionary<string, Api>(StringComparer.Ordinal);
        foreach (var tree in trees)
        {
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            foreach (var node in tree.GetRoot().DescendantNodesAndSelf())
            {
                if (node is not (BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
                    or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax
                    or EventDeclarationSyntax or VariableDeclaratorSyntax or EnumMemberDeclarationSyntax)) continue;
                var symbol = model.GetDeclaredSymbol(node);
                if (symbol is null || symbol.IsImplicitlyDeclared || symbol is not
                    (INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IEventSymbol or IFieldSymbol)) continue;
                if (symbol is IMethodSymbol m && m.MethodKind is not
                    (MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator or MethodKind.Conversion))
                    continue;
                var id = symbol.GetDocumentationCommentId();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var file = tree.FilePath.Replace('\\', '/');
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                bool publicApi = Exposed(symbol);
                bool internalContract = !publicApi && Critical.Contains(Path.GetFileName(file)) && ContractMember(symbol);
                bool included = publicApi || internalContract;
                var reason = included ? XmlStatus(symbol) : "EXCLUDED_NONCONTRACT_INTERNAL_OR_PRIVATE";
                var value = new Api(id, symbol.Kind.ToString(), file, line,
                    included && reason == "DOCUMENTED", reason, internalContract);
                if (included) eligible.TryAdd(id, value);
                else excluded.TryAdd(id, value);
            }
        }
        var members = eligible.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        return new Measurement(sha, members.Count, members.Count(x => x.Documented), members,
            excluded.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
    }

    private static bool Exposed(ISymbol symbol)
    {
        for (ISymbol? x = symbol; x is not null && x is not INamespaceSymbol; x = x.ContainingSymbol)
            if (x.DeclaredAccessibility is not (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                return false;
        return true;
    }
    private static bool ContractMember(ISymbol symbol)
    {
        for (ISymbol? x = symbol; x is not null && x is not INamespaceSymbol; x = x.ContainingSymbol)
            if (x.DeclaredAccessibility == Accessibility.Private) return false;
        return true;
    }
    private static string XmlStatus(ISymbol symbol)
    {
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: false);
        if (string.IsNullOrWhiteSpace(xml)) return "MISSING_XML";
        try
        {
            var doc = XDocument.Parse(xml);
            // Conservative until an override/interface target is independently resolved.
            if (doc.Descendants("inheritdoc").Any()) return "UNRESOLVED_INHERITDOC";
            var summary = string.Join(" ", doc.Descendants("summary").Select(x => x.Value.Trim()));
            if (summary.Length < 12) return "MISSING_OR_TRIVIAL_SUMMARY";
            if (System.Text.RegularExpressions.Regex.IsMatch(summary,
                @"(?i)\b(todo|tbd|placeholder|autogenerated)\b")) return "PLACEHOLDER_SUMMARY";
            return "DOCUMENTED";
        }
        catch (System.Xml.XmlException) { return "INVALID_XML"; }
    }
    private static bool IsHarness(string path)
    {
        var name = Path.GetFileName(path);
        return name.Contains("Harness", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Capture", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith("LegacyMediaCi.cs", StringComparison.OrdinalIgnoreCase);
    }
    private static IEnumerable<(string Name, string Source)> LocalFiles(string root) =>
        Directory.EnumerateFiles(Path.Combine(root, "src", "RomothequeManager.V2"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Replace('\\', '/').Split('/').Any(s => s is "obj" or "bin") && !IsHarness(p))
            .Select(p => (Path.GetRelativePath(root, p).Replace('\\', '/'), File.ReadAllText(p, Encoding.UTF8)));
    private static IEnumerable<(string Name, string Source)> GitFiles(string root, string sha)
    {
        foreach (var path in Git(root, "ls-tree", "-r", "--name-only", sha)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(x => x.StartsWith("src/RomothequeManager.V2/", StringComparison.Ordinal)
                && x.EndsWith(".cs", StringComparison.Ordinal) && !IsHarness(x)))
            yield return (path, Git(root, "show", sha + ":" + path));
    }
    private static string Git(string root, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = root,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("GIT_START_FAILED");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new InvalidOperationException("GIT_READ_FAILED " + error);
        return output;
    }
    private static void Save(string dir, string name, object value) =>
        File.WriteAllText(Path.Combine(dir, name), JsonSerializer.Serialize(value, JsonStyle), new UTF8Encoding(false));

    private static int RunCanaries()
    {
        const string good = "namespace Canary {\n/// <summary>Documents the example class used for testing.</summary>\npublic class Example {\n/// <summary>Returns a stable number to the caller.</summary>\npublic int Existing() => 1;\n}}";
        const string newBad = "namespace Canary {\n/// <summary>Documents the example class used for testing.</summary>\npublic class Example {\n/// <summary>Returns a stable number to the caller.</summary>\npublic int Existing() => 1;\npublic int NewApi() => 2;\n}}";
        const string newGood = "namespace Canary {\n/// <summary>Documents the example class used for testing.</summary>\npublic class Example {\n/// <summary>Returns a stable number to the caller.</summary>\npublic int Existing() => 1;\n/// <summary>Returns a second independent sample number.</summary>\npublic int NewApi() => 2;\n}}";
        const string removed = "namespace Canary {\n/// <summary>Documents the example class used for testing.</summary>\npublic class Example {\npublic int Existing() => 1;\n}}";
        Measurement M(string s) => Measure("canary", new[] { ("src/RomothequeManager.V2/Canary.cs", s) });
        var b = M(good); var red = M(newBad); var green = M(newGood); var loss = M(removed);
        if (b.Eligible != 2 || b.Documented != 2 ||
            red.Eligible != 3 || red.Documented != 2 ||
            green.Eligible != 3 || green.Documented != 3 ||
            loss.Eligible != 2 || loss.Documented != 1)
            throw new InvalidOperationException("DOC_COVERAGE_CANARY_WRONG_ORACLE");
        Console.WriteLine("DOC_CANARY_RED_NEW_UNDOCUMENTED_PASS");
        Console.WriteLine("DOC_CANARY_RED_REMOVED_SUMMARY_PASS");
        Console.WriteLine("DOC_CANARY_GREEN_RESTORED_PASS");
        return 0;
    }
}