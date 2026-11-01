using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace RomothequeManager.DocCoverage;

internal static class Program
{
    private static readonly JsonSerializerOptions JsonStyle = new() { WriteIndented = true };
    // Fail-closed eligibility: all explicitly declared non-private production
    // API members (public/protected and internal) are counted. The classification
    // does not depend on the spelling of a file or a type such as 'Service'.
    private record Api(string Id, string Kind, string File, int Line,
        bool Documented, string Reason, bool InternalContract);
    private record GeneratedExclusion(string SymbolId, string Rule,
        string GeneratorIdentity, string GeneratorVersion, string GeneratorHash,
        string GeneratedSource, string SourceHash, string CompilationSHA,
        int DeclarationsCount, string XamlSource, string XamlInputHash);
    private record Measurement(string Sha, int Eligible, int Documented,
        List<Api> Symbols, List<Api> Exclusions,
        List<GeneratedExclusion>? GeneratedExclusions = null)
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
            // No changed generator/version/source list may disappear behind an exclusion.
            VerifyGeneratedInputsStable(baseInputs.GeneratedProvenance, headInputs.GeneratedProvenance);
            var head = Measure(headSha, headCompilation, root, headInputs.GeneratedProvenance);
            var baseline = Measure(baseSha, baseCompilation, root, baseInputs.GeneratedProvenance);
            headInputs.VerifyActualAssembly(headCompilation, head.Symbols.Select(x => x.Id));
            baseInputs.VerifyActualAssembly(baseCompilation, baseline.Symbols.Select(x => x.Id));
            Directory.CreateDirectory(dir);
            Save(dir, "head.json", head);
            Save(dir, "base.json", baseline);
            Save(dir, "generated-exclusions-head.json", head.GeneratedExclusions ?? []);
            Save(dir, "generated-exclusions-base.json", baseline.GeneratedExclusions ?? []);
            // Reconcile every non-private eligible declaration before/after generated exclusions.
            Console.WriteLine("DOC_GENERATED_RECONCILIATION HEAD all=" +
                (head.Eligible + (head.GeneratedExclusions?.Count ?? 0)) +
                " manual=" + head.Eligible + " excluded=" +
                (head.GeneratedExclusions?.Count ?? 0));
            Console.WriteLine("DOC_GENERATED_RECONCILIATION BASE all=" +
                (baseline.Eligible + (baseline.GeneratedExclusions?.Count ?? 0)) +
                " manual=" + baseline.Eligible + " excluded=" +
                (baseline.GeneratedExclusions?.Count ?? 0));
            var (pass, added, removed, lostDocs) = EvaluateGate(baseline, head);
            Save(dir, "gate.json", new
            {
                schema = "1.0", headSha, sourceSha, baseSha, headEligible = head.Eligible,
                headDocumented = head.Documented, headPercent = head.Percent,
                baseEligible = baseline.Eligible, baseDocumented = baseline.Documented,
                basePercent = baseline.Percent, addedEligible = added,
                missingNew = added.Where(x => !x.Documented), lostDocs, removedEligible = removed,
                headEligibleAll = head.Eligible + (head.GeneratedExclusions?.Count ?? 0),
                baseEligibleAll = baseline.Eligible + (baseline.GeneratedExclusions?.Count ?? 0),
                generatedExcludedHead = head.GeneratedExclusions ?? [],
                generatedExcludedBase = baseline.GeneratedExclusions ?? [],
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
            md.AppendLine().AppendLine("## GENERATED_AVALONIA_ONLY: verified excluded IDs");
            foreach (var g in head.GeneratedExclusions ?? [])
                md.AppendLine("- " + g.SymbolId + " | " + g.Rule +
                    " | " + g.GeneratorIdentity + "@" + g.GeneratorVersion +
                    " | " + g.GeneratedSource + " | sourceHash=" + g.SourceHash +
                    " | SHA=" + g.CompilationSHA + " | declarations=" + g.DeclarationsCount +
                    " | XAML=" + g.XamlSource);
            md.AppendLine().AppendLine("## Missing API documentation");
            foreach (var s in head.Symbols.Where(x => !x.Documented))
                md.AppendLine("- " + s.Id + " at " + s.File + ":" + s.Line + ": " + s.Reason);
            md.AppendLine().AppendLine("## Exclusions (one entry per source symbol)");
            foreach (var s in head.Exclusions)
                md.AppendLine("- " + s.Id + " at " + s.File + ":" + s.Line + ": " + s.Reason);
            File.WriteAllText(Path.Combine(dir, "report.md"), md.ToString(), new UTF8Encoding(false));
            foreach (var group in head.Symbols.GroupBy(x => x.File).OrderBy(x => x.Key, StringComparer.Ordinal))
                Console.WriteLine("DOC_COVERAGE_FILE file=" + group.Key +
                    " eligible=" + group.Count() + " documented=" + group.Count(x => x.Documented) +
                    " missing=" + group.Count(x => !x.Documented));
            foreach (var group in (head.GeneratedExclusions ?? []).GroupBy(x => x.GeneratedSource)
                .OrderBy(x => x.Key, StringComparer.Ordinal))
                Console.WriteLine("DOC_GENERATED_FILE source=" + group.Key + " excluded=" + group.Count());
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

    // Same predicate is used by native Windows CI and every red/green canary.
    private static (bool Pass, Api[] Added, Api[] Removed, Api[] LostDocs)
        EvaluateGate(Measurement baseline, Measurement head)
    {
        var b = baseline.Symbols.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var h = head.Symbols.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var added = head.Symbols.Where(x => !b.ContainsKey(x.Id)).ToArray();
        var removed = baseline.Symbols.Where(x => !h.ContainsKey(x.Id)).ToArray();
        var lostDocs = head.Symbols.Where(x => b.TryGetValue(x.Id, out var old)
            && old.Documented && !x.Documented).ToArray();
        bool pass = baseline.Eligible > 0 && head.Eligible > 0 &&
            10L * head.Documented >= 9L * head.Eligible &&
            added.All(x => x.Documented) &&
            (long)head.Documented * baseline.Eligible >=
                (long)baseline.Documented * head.Eligible &&
            removed.Length == 0 && lostDocs.Length == 0;
        return (pass, added, removed, lostDocs);
    }

    private static void VerifyGeneratedInputsStable(
        IReadOnlyDictionary<string, GeneratedSourceProvenance> baseline,
        IReadOnlyDictionary<string, GeneratedSourceProvenance> head)
    {
        // CI worktrees use different absolute temp roots; compare compiler-relative
        // generator outputs, identities and real analyzer hashes instead.
        static string[] Manifest(IReadOnlyDictionary<string, GeneratedSourceProvenance> value) =>
            value.Values.Select(x => x.RelativePath.Replace('\\', '/') + "|" +
                x.GeneratorIdentity + "|" + x.GeneratorVersion + "|" + x.GeneratorHash +
                "|" + x.SourceHash + "|" + x.XamlInputHash + "|" + x.Attested)
                .Order(StringComparer.Ordinal).ToArray();
        var before = Manifest(baseline);
        var after = Manifest(head);
        if (before.Length == 0 || after.Length == 0 || !before.SequenceEqual(after, StringComparer.Ordinal))
            throw new InvalidDataException("DOC_GENERATED_COMPILER_OUTPUT_OR_VERSION_CHANGED_REVIEW_REQUIRED:" +
                "base=" + before.Length + " head=" + after.Length);
        Console.WriteLine("DOC_GENERATED_BASE_HEAD_PROVENANCE_STABLE sources=" + after.Length);
    }

    private static Measurement Measure(string sha, CSharpCompilation compilation, string root,
        IReadOnlyDictionary<string, GeneratedSourceProvenance>? provenance = null)
    {
        var errors = compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error).Take(12).ToArray();
        if (errors.Length != 0)
            throw new InvalidDataException("REAL_COMPILATION_DIAGNOSTICS:" +
                string.Join("|", errors.Select(x => x.ToString())));
        var eligible = new Dictionary<string, Api>(StringComparer.Ordinal);
        var excluded = new Dictionary<string, Api>(StringComparer.Ordinal);
        var generatedExcluded = new Dictionary<string, GeneratedExclusion>(StringComparer.Ordinal);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            foreach (var node in tree.GetRoot().DescendantNodesAndSelf())
            {
                if (node is not (BaseTypeDeclarationSyntax or DelegateDeclarationSyntax
                    or BaseMethodDeclarationSyntax or BasePropertyDeclarationSyntax
                    or EventDeclarationSyntax or VariableDeclaratorSyntax or EnumMemberDeclarationSyntax))
                    continue;
                var symbol = model.GetDeclaredSymbol(node);
                if (symbol is null || symbol.IsImplicitlyDeclared || symbol is not
                    (INamedTypeSymbol or IMethodSymbol or IPropertySymbol or IEventSymbol or IFieldSymbol))
                    continue;
                if (symbol is IMethodSymbol m && m.MethodKind is not
                    (MethodKind.Ordinary or MethodKind.Constructor or MethodKind.UserDefinedOperator
                     or MethodKind.Conversion or MethodKind.ExplicitInterfaceImplementation))
                    continue;
                var id = symbol.GetDocumentationCommentId();
                if (string.IsNullOrWhiteSpace(id)) continue;
                var raw = tree.FilePath.Replace('\\', '/');
                var marker = raw.LastIndexOf("src/RomothequeManager.V2/", StringComparison.OrdinalIgnoreCase);
                var file = marker >= 0 ? raw[marker..] : raw;
                int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                bool publicApi = Exposed(symbol);
                // No filename/type-name heuristics or implicit harness exemptions:
                // a new internal ReadAccessPolicy.AllowsRead is eligible just as
                // any other declared internal contract from the compiled project.
                bool critical = !publicApi && ContractMember(symbol);
                bool included = publicApi || critical;
                var declarations = symbol.DeclaringSyntaxReferences;
                // A partial type with even one manual declaration is NEVER generated-only.
                // No filename suffix, guessed namespace, or missing-XML condition can grant exclusion.
                var matches = declarations.Select(x =>
                {
                    var generatedPath = x.SyntaxTree.FilePath;
                    if (provenance is null || !provenance.TryGetValue(generatedPath, out var ev))
                        return (Evidence: (GeneratedSourceProvenance?)null, Valid: false);
                    var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                        x.SyntaxTree.GetText().ToString()))).ToLowerInvariant();
                    bool valid = ev.Attested &&
                        ev.GeneratorIdentity == "Avalonia.Generators" &&
                        !string.IsNullOrWhiteSpace(ev.GeneratorVersion) &&
                        !string.IsNullOrWhiteSpace(ev.GeneratorHash) &&
                        !ev.XamlSource.StartsWith("UNMAPPED_", StringComparison.Ordinal) &&
                        string.Equals(ev.SourceHash, sourceHash, StringComparison.Ordinal);
                    return (Evidence: (GeneratedSourceProvenance?)ev, Valid: valid);
                }).ToArray();
                bool generatedOnly = included && declarations.Length > 0 &&
                    matches.Length == declarations.Length &&
                    matches.All(x => x.Valid && x.Evidence is not null);
                if (generatedOnly)
                {
                    var evidence = matches[0].Evidence!;
                    // Each distinct symbol is counted once even for multiple source declarations.
                    generatedExcluded.TryAdd(id, new GeneratedExclusion(id, "GENERATED_AVALONIA_ONLY",
                        evidence.GeneratorIdentity, evidence.GeneratorVersion, evidence.GeneratorHash,
                        evidence.RelativePath, evidence.SourceHash, sha,
                        declarations.Length, evidence.XamlSource, evidence.XamlInputHash));
                    continue;
                }
                var reason = included
                    ? XmlStatus(symbol, compilation, new HashSet<string>(StringComparer.Ordinal))
                    : "EXCLUDED_NONCONTRACT_PRIVATE_OR_FILE_LOCAL";
                var value = new Api(id, symbol.Kind.ToString(), file, line,
                    included && reason == "DOCUMENTED", reason, critical);
                if (included) eligible.TryAdd(id, value);
                else excluded.TryAdd(id, value);
            }
        }
        var list = eligible.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList();
        if (generatedExcluded.Keys.Any(eligible.ContainsKey))
            throw new InvalidDataException("DOC_GENERATED_MANUAL_ID_COLLISION");
        return new Measurement(sha, list.Count, list.Count(x => x.Documented), list,
            excluded.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList(),
            generatedExcluded.Values.OrderBy(x => x.SymbolId, StringComparer.Ordinal).ToList());
    }

    private static bool Exposed(ISymbol symbol)
    {
        for (ISymbol? x = symbol; x is not null && x is not INamespaceSymbol; x = x.ContainingSymbol)
            if (x.DeclaredAccessibility is not
                (Accessibility.Public or Accessibility.Protected or Accessibility.ProtectedOrInternal))
                return false;
        return true;
    }

    private static bool ContractMember(ISymbol symbol)
    {
        for (ISymbol? x = symbol; x is not null && x is not INamespaceSymbol; x = x.ContainingSymbol)
            if (x.DeclaredAccessibility == Accessibility.Private) return false;
        return true;
    }

    private static ISymbol? FindInheritanceTarget(ISymbol symbol)
    {
        ISymbol? direct = symbol switch
        {
            IMethodSymbol m => (ISymbol?)m.OverriddenMethod ?? m.ExplicitInterfaceImplementations.FirstOrDefault(),
            IPropertySymbol p => (ISymbol?)p.OverriddenProperty ?? p.ExplicitInterfaceImplementations.FirstOrDefault(),
            IEventSymbol e => (ISymbol?)e.OverriddenEvent ?? e.ExplicitInterfaceImplementations.FirstOrDefault(),
            INamedTypeSymbol t => t.BaseType ?? t.Interfaces.FirstOrDefault(),
            _ => null
        };
        if (direct is not null) return direct;
        var container = symbol.ContainingType;
        if (container is null) return null;
        foreach (var face in container.AllInterfaces)
        foreach (var candidate in face.GetMembers())
        {
            var implementation = container.FindImplementationForInterfaceMember(candidate);
            if (implementation is not null &&
                SymbolEqualityComparer.Default.Equals(implementation.OriginalDefinition,
                    symbol.OriginalDefinition))
                return candidate;
        }
        return null;
    }

    private static string XmlStatus(ISymbol symbol, CSharpCompilation compilation,
        HashSet<string> visited)
    {
        var id = symbol.GetDocumentationCommentId() ?? symbol.ToDisplayString();
        if (!visited.Add(id)) return "INHERITDOC_CYCLE";
        var xml = symbol.GetDocumentationCommentXml(expandIncludes: false);
        if (string.IsNullOrWhiteSpace(xml)) return "MISSING_XML";
        try
        {
            var doc = XDocument.Parse(xml);
            var summaries = doc.Descendants("summary").ToArray();
            if (summaries.Length > 1) return "DUPLICATE_XML_SUMMARY";
            var inheritance = doc.Descendants("inheritdoc").ToArray();
            if (inheritance.Length > 1) return "DUPLICATE_INHERITDOC";
            if (inheritance.Length == 1)
            {
                var cref = inheritance[0].Attribute("cref")?.Value;
                ISymbol? target = !string.IsNullOrWhiteSpace(cref) ?
                    DocumentationCommentId.GetFirstSymbolForDeclarationId(cref!, compilation) ??
                    DocumentationCommentId.GetFirstSymbolForReferenceId(cref!, compilation) :
                    FindInheritanceTarget(symbol);
                if (target is null || SymbolEqualityComparer.Default.Equals(target, symbol))
                    return "UNRESOLVED_INHERITDOC";
                var status = XmlStatus(target, compilation, visited);
                if (status != "DOCUMENTED") return "INHERITDOC_TARGET_" + status;
                if (summaries.Length == 0) return "DOCUMENTED";
            }
            if (summaries.Length != 1) return "MISSING_XML_SUMMARY";
            var summary = System.Text.RegularExpressions.Regex.Replace(summaries[0].Value, @"\s+", " ").Trim();
            if (summary.Length < 12 ||
                summary.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct(StringComparer.OrdinalIgnoreCase).Count() < 3)
                return "MISSING_OR_TRIVIAL_SUMMARY";
            if (System.Text.RegularExpressions.Regex.IsMatch(summary,
                @"(?i)\b(todo|tbd|placeholder|autogenerated|to be documented|lorem ipsum)\b"))
                return "PLACEHOLDER_SUMMARY";
            var badParam = doc.Descendants("param").Any(p =>
                string.IsNullOrWhiteSpace(p.Attribute("name")?.Value) ||
                string.IsNullOrWhiteSpace(p.Value));
            if (badParam) return "MALFORMED_PARAM_DOC";
            return "DOCUMENTED";
        }
        catch (System.Xml.XmlException) { return "INVALID_XML"; }
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
        const string existing = """
            namespace Canary {
            /// <summary>Documents a stable source class and its local behavior.</summary>
            public partial class Example {
            /// <summary>Returns the stable example number to its caller.</summary>
            public int Existing() => 1;
            }}
            """;
        const string newPublicMissing = """
            namespace Canary {
            /// <summary>Documents a stable source class and its local behavior.</summary>
            public partial class Example {
            /// <summary>Returns the stable example number to its caller.</summary>
            public int Existing() => 1;
            public int NewApi() => 2;
            }}
            """;
        const string newPublicGood = """
            namespace Canary {
            /// <summary>Documents a stable source class and its local behavior.</summary>
            public partial class Example {
            /// <summary>Returns the stable example number to its caller.</summary>
            public int Existing() => 1;
            /// <summary>Returns a second stable value without any filesystem access.</summary>
            public int NewApi() => 2;
            }}
            """;
        const string duplicateSummary = """
            namespace Canary {
            /// <summary>Documents a stable source class and its local behavior.</summary>
            public partial class Example {
            /// <summary>Returns the stable example number to its caller.</summary>
            /// <summary>Conflicting duplicate description of the same method.</summary>
            public int Existing() => 1;
            }}
            """;
        const string lostSummary = """
            namespace Canary {
            /// <summary>Documents a stable source class and its local behavior.</summary>
            public partial class Example {
            public int Existing() => 1;
            }}
            """;
        const string removedMember = """
            namespace Canary {
            /// <summary>Documents a stable source class and its local behavior.</summary>
            public partial class Example { }
            }
            """;
        const string criticalMissing = """
            namespace Canary {
            internal sealed class ProchainePermissionApplicationService {
              internal bool ValidateGrant() => false;
            }}
            """;
        const string criticalGood = """
            namespace Canary {
            /// <summary>Checks a hypothetical temporary fixture permission without opening real files.</summary>
            internal sealed class ProchainePermissionApplicationService {
              /// <summary>Rejects the synthetic grant until an explicit scope is supplied.</summary>
              internal bool ValidateGrant() => false;
            }}
            """;
        const string implementation = """
            namespace Canary {
            /// <summary>Contract that exposes a read-only operation without modifying media.</summary>
            public interface IProbe {
              /// <summary>Returns the observed number without any filesystem operation.</summary>
              int Read();
            }
            /// <summary>Provides the in-memory implementation of a read-only example.</summary>
            public sealed class Probe : IProbe {
              /// <inheritdoc/>
              public int Read() => 42;
            }}
            """;
        const string unresolved = """
            namespace Canary {
            /// <summary>Exposes a service whose inherited documentation target is missing.</summary>
            public sealed class Wrong {
              /// <inheritdoc/>
              public void Read() {}
            }}
            """;
        const string wrongBaseline = """
            namespace Canary {
            /// <summary>Exposes a service whose method must carry its own valid documentation.</summary>
            public sealed class Wrong {
              /// <summary>Reads a fixed local example value without opening any file.</summary>
              public void Read() {}
            }}
            """;
        const string partial2 = """
            namespace Canary {
            public partial class Example {
            /// <summary>Returns a valid additional symbol defined by the second partial declaration.</summary>
            public int PartialExtra() => 3;
            }}
            """;
        const string privateOnly = """
            namespace Canary {
            /// <summary>Contains only an explicitly private implementation detail.</summary>
            public class PrivateOnly {
              private void Hidden() {}
            }}
            """;
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Where(File.Exists).Select(x => MetadataReference.CreateFromFile(x)).ToArray();
        if (refs.Length < 20) throw new InvalidOperationException("CANARY_TPA_MISSING");
        Measurement M(string[] texts, params string[] defines)
        {
            var parse = CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose)
                .WithPreprocessorSymbols(defines);
            var trees = texts.Select((text, i) => CSharpSyntaxTree.ParseText(
                text, parse, "src/RomothequeManager.V2/Canary" + i + ".cs")).ToArray();
            var compilation = CSharpCompilation.Create("Canary", trees, refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            return Measure("canary", compilation, ".");
        }
        // Synthetic provenance exercises the classifier; the separate Windows
        // build tests its real MSBuild/analyzer/XAML source-of-truth.
        Measurement WithGenerated(string generated, string[] manual, bool attested = true,
            bool corruptHash = false, bool fakeGenerator = false)
        {
            const string generatedPath =
                "generated-head/Avalonia.Generators/Avalonia.Generators.NameGenerator/Actual.g.cs";
            var parse = CSharpParseOptions.Default.WithDocumentationMode(DocumentationMode.Diagnose);
            var trees = new List<SyntaxTree> { CSharpSyntaxTree.ParseText(generated, parse, generatedPath) };
            trees.AddRange(manual.Select((code, index) => CSharpSyntaxTree.ParseText(
                code, parse, "src/RomothequeManager.V2/UserWritten" + index + ".g.cs")));
            var compilation = CSharpCompilation.Create("GeneratedCanary", trees, refs,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var source = new GeneratedSourceProvenance(generatedPath,
                "Avalonia.Generators/Avalonia.Generators.NameGenerator/Actual.g.cs",
                fakeGenerator ? "Untrusted.Generator" : "Avalonia.Generators", "1.0.0.0",
                "VERIFIED_ANALYZER_SHA256", corruptHash ? "TAMPERED" :
                    RealProjectInputs.Sha256Text(generated),
                "MainWindow.axaml", "VERIFIED_XAML_SET_SHA256", attested);
            var evidence = new Dictionary<string, GeneratedSourceProvenance>(
                StringComparer.OrdinalIgnoreCase) { [generatedPath] = source };
            return Measure("canary", compilation, ".", evidence);
        }
        void AssertRed(string name, Measurement before, Measurement after)
        {
            if (EvaluateGate(before, after).Pass)
                throw new InvalidOperationException("DOC_GATE_E2E_RED_FAILED:" + name);
            Console.WriteLine("DOC_GATE_E2E_RED_" + name + "_PASS");
        }
        void AssertGreen(string name, Measurement before, Measurement after)
        {
            if (!EvaluateGate(before, after).Pass)
                throw new InvalidOperationException("DOC_GATE_E2E_GREEN_FAILED:" + name);
            Console.WriteLine("DOC_GATE_E2E_GREEN_" + name + "_PASS");
        }
        var baseline = M([existing]);
        if (baseline.Eligible != 2 || baseline.Documented != 2)
            throw new InvalidOperationException("DOC_GATE_CANARY_BASELINE_INVALID");
        AssertRed("NEW_PUBLIC_UNDOCUMENTED", baseline, M([newPublicMissing]));
        AssertGreen("NEW_PUBLIC_DOCUMENTED", baseline, M([newPublicGood]));
        AssertRed("LOST_XML_SUMMARY", baseline, M([lostSummary]));
        AssertRed("REMOVED_ELIGIBLE_API", baseline, M([removedMember]));
        AssertRed("DUPLICATE_XML_SUMMARY", baseline, M([duplicateSummary]));
        AssertRed("INTERNAL_CRITICAL_MISSING", baseline, M([existing, criticalMissing]));
        AssertGreen("INTERNAL_CRITICAL_DOCUMENTED", baseline, M([existing, criticalGood]));
        AssertGreen("INHERITDOC_RESOLVED", M([implementation]), M([implementation]));
        AssertRed("INHERITDOC_UNRESOLVED", M([wrongBaseline]), M([unresolved]));
        AssertGreen("PARTIAL_DEDUP_DOCUMENTED", baseline, M([existing, partial2]));
        AssertGreen("PRIVATE_EXCLUDED", M([privateOnly]), M([privateOnly]));
        // P1 regression: a never-before-seen internal authorization contract
        // must fail through EvaluateGate even when the overall ratio is >90%.
        // In particular "ReadAccessPolicy" must not rely on a keyword list.
        var documentedStableSuite =
            "namespace RomothequeManager.V2 {\n" +
            "/// <summary>Contains twenty previously accepted synthetic read-only operations.</summary>\n" +
            "public sealed class StableApiSuite {\n" +
            string.Concat(Enumerable.Range(0, 20).Select(i =>
                "/// <summary>Returns the known synthetic number " + i +
                " with no filesystem access or mutation.</summary>\n" +
                "public int Read" + i + "() => " + i + ";\n")) +
            "}}\n";
        const string atypicalMissing = """
            namespace RomothequeManager.V2 {
            internal sealed class ReadAccessPolicy {
              internal bool AllowsRead() => false;
            }}
            """;
        const string atypicalDocumented = """
            namespace RomothequeManager.V2 {
            /// <summary>Controls fixture-only read admission without granting actual filesystem access.</summary>
            internal sealed class ReadAccessPolicy {
              /// <summary>Denies access to an unconfirmed synthetic read scope without opening any file.</summary>
              internal bool AllowsRead() => false;
            }}
            """;
        void CheckGeneratedCanaries()
        {
            const string generated = """
                namespace CanaryGen {
                public partial class SyntheticGenerated {
                    public int GeneratedMember() => 1;
                }}
                """;
            const string manualPartial = """
                namespace CanaryGen {
                public partial class SyntheticGenerated {
                    public int ManualMember() => 2;
                }}
                """;
            var pure = WithGenerated(generated, []);
            if (pure.Eligible != 0 || pure.GeneratedExclusions?.Count != 2 ||
                pure.GeneratedExclusions.Any(x => x.Rule != "GENERATED_AVALONIA_ONLY" ||
                    x.DeclarationsCount != 1 || x.SourceHash.Length != 64))
                throw new InvalidOperationException("DOC_GENERATED_TRUE_PROVENANCE_NOT_EXCLUDED");
            Console.WriteLine("DOC_GENERATED_TRUE_AVALONIA_ONLY_EXCLUDED_PASS");

            // A hand-authored partial declaration keeps the shared type eligible.
            // The wholly generated member may remain excluded independently.
            var mixed = WithGenerated(generated, [manualPartial]);
            if (!mixed.Symbols.Any(x => x.Id == "T:CanaryGen.SyntheticGenerated") ||
                !mixed.Symbols.Any(x => x.Id == "M:CanaryGen.SyntheticGenerated.ManualMember") ||
                mixed.GeneratedExclusions?.Count != 1)
                throw new InvalidOperationException("DOC_GENERATED_PARTIAL_MANUAL_WAS_EXCLUDED");
            Console.WriteLine("DOC_GENERATED_MIXED_PARTIAL_REMAINS_ELIGIBLE_PASS");

            foreach (var invalid in new[] {
                WithGenerated(generated, [], attested:false),
                WithGenerated(generated, [], corruptHash:true),
                WithGenerated(generated, [], fakeGenerator:true),
                M([generated]) // manual source even though a class name resembles generated output
            })
                if (invalid.Eligible != 2 || invalid.GeneratedExclusions?.Count != 0)
                    throw new InvalidOperationException("DOC_GENERATED_SPOOFED_SOURCE_EXCLUDED");
            Console.WriteLine("DOC_GENERATED_UNTRUSTED_OR_TAMPERED_REMAINS_ELIGIBLE_PASS");

            // An eligible manual API is never hidden by an unrelated authentic
            // generated exclusion, including above the 90% ratio threshold.
            var before = WithGenerated(generated, [documentedStableSuite]);
            var missing = WithGenerated(generated, [documentedStableSuite, atypicalMissing]);
            var fixedDocs = WithGenerated(generated, [documentedStableSuite, atypicalDocumented]);
            if (before.Documented != 21 || before.Eligible != 21 ||
                missing.Documented != 21 || missing.Eligible != 23 ||
                10L * missing.Documented < 9L * missing.Eligible ||
                EvaluateGate(before, missing).Pass || !EvaluateGate(before, fixedDocs).Pass)
                throw new InvalidOperationException("DOC_GENERATED_MANUAL_90_PLUS_GATE_BYPASS");
            Console.WriteLine("DOC_GENERATED_MANUAL_NEW_API_90_PLUS_RED_GREEN_PASS");

            var stable = new Dictionary<string, GeneratedSourceProvenance>(
                StringComparer.OrdinalIgnoreCase) {
                ["one"] = new GeneratedSourceProvenance("one", "Avalonia.Generators/One.g.cs",
                    "Avalonia.Generators", "1.0.0.0", "ANALYZER_HASH", "SOURCE_HASH",
                    "MainWindow.axaml", "XAML_HASH", true)
            };
            var changed = new Dictionary<string, GeneratedSourceProvenance>(stable,
                StringComparer.OrdinalIgnoreCase) {
                ["one"] = stable["one"] with { GeneratorVersion = "2.0.0.0" }
            };
            bool driftRejected = false;
            try { VerifyGeneratedInputsStable(stable, changed); }
            catch (InvalidDataException) { driftRejected = true; }
            if (!driftRejected) throw new InvalidOperationException("DOC_GENERATED_DRIFT_NOT_BLOCKED");
            Console.WriteLine("DOC_GENERATED_BASE_HEAD_VERSION_DRIFT_RED_PASS");
        }


        var highBaseline = M([documentedStableSuite]);
        var highMissing = M([documentedStableSuite, atypicalMissing]);
        var highDocumented = M([documentedStableSuite, atypicalDocumented]);
        var highGate = EvaluateGate(highBaseline, highMissing);
        var highAdded = highGate.Added.Select(x => x.Id).ToArray();
        if (highBaseline.Eligible != 21 || highBaseline.Documented != 21 ||
            highMissing.Eligible != 23 || highMissing.Documented != 21 ||
            10L * highMissing.Documented < 9L * highMissing.Eligible ||
            highAdded.Length != 2 ||
            !highAdded.Contains("T:RomothequeManager.V2.ReadAccessPolicy", StringComparer.Ordinal) ||
            !highAdded.Contains("M:RomothequeManager.V2.ReadAccessPolicy.AllowsRead", StringComparer.Ordinal) ||
            !highGate.Added.All(x => x.InternalContract && !x.Documented) ||
            highGate.Pass)
            throw new InvalidOperationException("DOC_GATE_HIGH_RATIO_INTERNAL_NEW_API_NOT_BLOCKED");
        Console.WriteLine("DOC_GATE_E2E_RED_ATYPICAL_INTERNAL_90_PLUS_PASS " +
            "documented=" + highMissing.Documented + " eligible=" + highMissing.Eligible +
            " addedUndocumented=" + highGate.Added.Length);
        AssertGreen("ATYPICAL_INTERNAL_DOCUMENTED_90_PLUS", highBaseline, highDocumented);
        var highPublicMissing = M([documentedStableSuite,
            "namespace RomothequeManager.V2 {\n" +
            "/// <summary>Contains a newly exported synthetic read probe that lacks an XML member summary.</summary>\n" +
            "public sealed class AdditionalApi { public bool Read() => false; }\n}\n"]);
        if (10L * highPublicMissing.Documented < 9L * highPublicMissing.Eligible)
            throw new InvalidOperationException("DOC_GATE_HIGH_RATIO_PUBLIC_CANARY_NOT_OVER_90");
        AssertRed("NEW_PUBLIC_UNDOCUMENTED_90_PLUS", highBaseline, highPublicMissing);

        const string conditional = """
            namespace Canary {
            /// <summary>Contains conditionally compiled contractual source declarations.</summary>
            public sealed class Conditional {
            #if RELEASE
              public int NewGrant() => 1;
            #endif
            }}
            """;
        var conditionalEnabled = M([conditional], "RELEASE");
        var conditionalDisabled = M([conditional]);
        if (conditionalEnabled.Eligible != 2 || conditionalDisabled.Eligible != 1)
            throw new InvalidOperationException("DOC_CANARY_MSBUILD_DEFINES_NOT_EFFECTIVE");
        Console.WriteLine("DOC_GATE_E2E_DEFINES_DIFFERENT_SYMBOL_SETS_PASS");
        CheckGeneratedCanaries();
        return 0;
    }
}
