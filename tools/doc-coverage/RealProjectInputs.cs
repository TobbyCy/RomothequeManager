using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RomothequeManager.DocCoverage;

/// <summary>
/// Exact source/reference/define inputs captured from the application MSBuild CoreCompile
/// target. Both base and HEAD are built separately in native Windows worktrees.
/// </summary>
internal sealed class RealProjectInputs
{
    internal required string AssemblyName { get; init; }
    internal required string TargetPath { get; init; }
    internal required string Configuration { get; init; }
    internal required string TargetFramework { get; init; }
    internal required string Nullable { get; init; }
    internal required string LangVersion { get; init; }
    internal required string[] Defines { get; init; }
    internal required string[] Sources { get; init; }
    internal required string[] References { get; init; }

    internal static RealProjectInputs Read(string path)
    {
        if (!File.Exists(path)) throw new InvalidDataException("REAL_MSBUILD_MANIFEST_MISSING");
        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in File.ReadLines(path, Encoding.UTF8))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.StartsWith("PROPERTY|", StringComparison.Ordinal))
            {
                var fields = line.Split('|', 3);
                if (fields.Length != 3 || !props.TryAdd(fields[1], fields[2]))
                    throw new InvalidDataException("REAL_MSBUILD_PROPERTY_DUPLICATE");
            }
            else if (line.StartsWith("SOURCE|", StringComparison.Ordinal))
                sources.Add(Path.GetFullPath(line["SOURCE|".Length..]));
            else if (line.StartsWith("REFERENCE|", StringComparison.Ordinal))
                refs.Add(Path.GetFullPath(line["REFERENCE|".Length..]));
            else if (line.Length != 0)
                throw new InvalidDataException("REAL_MSBUILD_UNKNOWN_MANIFEST_ENTRY:" + line[..Math.Min(30,line.Length)]);
        }
        string Get(string key) => props.GetValueOrDefault(key) ?? throw new InvalidDataException("REAL_MSBUILD_PROPERTY_MISSING:" + key);
        var targetPath = Path.GetFullPath(Get("TargetPath"));
        if (Get("AssemblyName") != "RomothequeManager.V2" ||
            Get("Configuration") != "Release" ||
            Get("TargetFramework") != "net10.0" ||
            sources.Count < 20 || refs.Count < 20 ||
            !File.Exists(targetPath) ||
            sources.Any(x => !File.Exists(x) || !x.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) ||
            refs.Any(x => !File.Exists(x) || !x.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("REAL_MSBUILD_INPUTS_INVALID_OR_INCOMPLETE");
        return new RealProjectInputs
        {
            AssemblyName = Get("AssemblyName"),
            TargetPath = targetPath,
            Configuration = Get("Configuration"),
            TargetFramework = Get("TargetFramework"),
            Nullable = Get("Nullable"),
            LangVersion = Get("LangVersion"),
            Defines = Get("DefineConstants").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            Sources = sources.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            References = refs.Order(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }

    internal CSharpCompilation Compile()
    {
        var version = LanguageVersion.Default;
        if (LangVersion.Length != 0 && !LanguageVersionFacts.TryParse(LangVersion, out version))
            throw new InvalidDataException("REAL_MSBUILD_LANG_VERSION_INVALID:" + LangVersion);
        var parse = new CSharpParseOptions(version, DocumentationMode.Diagnose, preprocessorSymbols: Defines);
        var trees = Sources.Select(path => CSharpSyntaxTree.ParseText(
            File.ReadAllText(path, Encoding.UTF8), parse, path)).ToArray();
        var refs = References.Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var options = new CSharpCompilationOptions(OutputKind.WindowsApplication,
            nullableContextOptions: Nullable.Equals("enable", StringComparison.OrdinalIgnoreCase)
                ? NullableContextOptions.Enable : NullableContextOptions.Disable);
        var compilation = CSharpCompilation.Create(AssemblyName, trees, refs, options);
        var errors = compilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Take(20).Select(d => d.ToString()).ToArray();
        if (errors.Length != 0)
            throw new InvalidDataException("REAL_MSBUILD_ROSLYN_DIAGNOSTICS_ERROR:" + string.Join(" || ",errors));
        return compilation;
    }

    internal void VerifyActualAssembly(CSharpCompilation source, IEnumerable<string> eligibleIds)
    {
        var asmRef = MetadataReference.CreateFromFile(TargetPath);
        var meta = CSharpCompilation.Create("DocCoverageReferenceAudit",
            references: References.Select(x => MetadataReference.CreateFromFile(x)).Append(asmRef),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var missing = new List<string>();
        var all = eligibleIds.Distinct(StringComparer.Ordinal).ToArray();
        foreach (var id in all)
        {
            var symbol = DocumentationCommentId.GetFirstSymbolForDeclarationId(id, meta);
            if (symbol is null ||
                symbol.ContainingAssembly?.Identity.Name != AssemblyName)
                missing.Add(id);
        }
        if (missing.Count != 0)
            throw new InvalidDataException("REAL_ASSEMBLY_DECLARED_API_MISSING:" +
                missing.Count + ":" + string.Join(",", missing.Take(12)));
        Console.WriteLine("DOC_COVERAGE_REAL_ASSEMBLY_CROSSCHECK_PASS " +
            "symbols=" + all.Length + " referenceAssemblies=" + References.Length +
            " compiledSources=" + Sources.Length + " defines=" + string.Join(",", Defines));
    }
}
