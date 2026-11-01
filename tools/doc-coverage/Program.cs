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
    // Fail-closed eligibility: all explicitly declared non-private production
    // API members (public/protected and internal) are counted. The classification
    // does not depend on the spelling of a file or a type such as 'Service'.
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

    private static Measurement Measure(string sha, CSharpCompilation compilation, string root)
    {
        var errors = compilation.GetDiagnostics()
            .Where(x => x.Severity == DiagnosticSeverity.Error).Take(12).ToArray();
        if (errors.Length != 0)
            throw new InvalidDataException("REAL_COMPILATION_DIAGNOSTICS:" +
                string.Join("|", errors.Select(x => x.ToString())));
        var eligible = new Dictionary<string, Api>(StringComparer.Ordinal);
        var excluded = new Dictionary<string, Api>(StringComparer.Ordinal);
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
        return new Measurement(sha, list.Count, list.Count(x => x.Documented), list,
            excluded.Values.OrderBy(x => x.Id, StringComparer.Ordinal).ToList());
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
        return 0;
    }
}
