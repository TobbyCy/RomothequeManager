using System.Text;
using System.Reflection;
using System.Security.Cryptography;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace RomothequeManager.DocCoverage;

// An exclusion is valid only when its source and generator were captured by the
// instrumented native compilation. Output folder names alone grant nothing.
internal sealed record GeneratedSourceProvenance(
    string FullPath, string RelativePath, string GeneratorIdentity,
    string GeneratorVersion, string GeneratorHash, string SourceHash,
    string XamlSource, string XamlInputHash, bool Attested);


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
    internal required IReadOnlyDictionary<string, GeneratedSourceProvenance> GeneratedProvenance { get; init; }

    internal static string Sha256(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    internal static string Sha256Text(string source) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source))).ToLowerInvariant();

    internal static string GeneratedKey(GeneratedSourceProvenance source) =>
        source.GeneratorIdentity + "/" + source.RelativePath.Replace('\\', '/');

    internal static RealProjectInputs Read(string path)
    {
        if (!File.Exists(path)) throw new InvalidDataException("REAL_MSBUILD_MANIFEST_MISSING");
        var props = new Dictionary<string, string>(StringComparer.Ordinal);
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var analyzers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var xamls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
            else if (line.StartsWith("ANALYZER|", StringComparison.Ordinal))
                analyzers.Add(Path.GetFullPath(line["ANALYZER|".Length..]));
            else if (line.StartsWith("XAML_SOURCE|", StringComparison.Ordinal))
                xamls.Add(Path.GetFullPath(line["XAML_SOURCE|".Length..]));
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
        var generatorRoot = Path.GetFullPath(Get("CompilerGeneratedFilesOutputPath"));
        if (!Directory.Exists(generatorRoot))
            throw new InvalidDataException("REAL_MSBUILD_SOURCE_GENERATOR_DIR_MISSING");
        var generated = Directory.EnumerateFiles(generatorRoot, "*.cs", SearchOption.AllDirectories)
            .Select(Path.GetFullPath).ToArray();
        if (generated.Length < 3)
            throw new InvalidDataException("REAL_MSBUILD_AVALONIA_GENERATED_SOURCES_MISSING");
        // Bind generator identity to actual compiler-selected analyzer assemblies,
        // and XAML provenance to actual project inputs. Unmapped or mixed symbols
        // remain eligible; never classify generated status by ".g.cs" suffix.
        var projectDirectory = Path.GetFullPath(Get("MSBuildProjectDirectory"));
        if (!Directory.Exists(projectDirectory) ||
            analyzers.Count == 0 || analyzers.Any(x => !File.Exists(x)))
            throw new InvalidDataException("DOC_GENERATED_REAL_ANALYZERS_MISSING");
        var xamlInputs = xamls.Where(File.Exists).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (xamls.Count != xamlInputs.Length)
            throw new InvalidDataException("DOC_GENERATED_XAML_INPUT_MISSING");
        if (xamlInputs.Length == 0)
            xamlInputs = Directory.EnumerateFiles(projectDirectory, "*.axaml", SearchOption.AllDirectories)
                .Where(x => !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase) &&
                    !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.OrdinalIgnoreCase).ToArray();
        if (xamlInputs.Length == 0)
            throw new InvalidDataException("DOC_GENERATED_XAML_INPUT_SET_EMPTY");
        var xamlFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n", xamlInputs.Select(x =>
                Path.GetRelativePath(projectDirectory, x).Replace('\\', '/') + "|" + Sha256(x)))))).ToLowerInvariant();
        var avaloniaAnalyzers = analyzers.Where(x =>
            Path.GetFileName(x).Equals("Avalonia.Generators.dll", StringComparison.OrdinalIgnoreCase))
            .Select(x => new { Path = x, Assembly = System.Reflection.AssemblyName.GetAssemblyName(x) })
            .Where(x => x.Assembly.Name == "Avalonia.Generators").ToArray();
        if (avaloniaAnalyzers.Length != 1)
            throw new InvalidDataException("DOC_GENERATED_AVALONIA_ANALYZER_AMBIGUOUS_OR_MISSING:" +
                avaloniaAnalyzers.Length);
        var analyzer = avaloniaAnalyzers[0];
        var analyzerHash = Sha256(analyzer.Path);
        var analyzerVersion = analyzer.Assembly.Version?.ToString() ?? "UNVERSIONED";
        var evidence = new Dictionary<string, GeneratedSourceProvenance>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in generated)
        {
            var relative = Path.GetRelativePath(generatorRoot, source).Replace('\\', '/');
            var fragments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var declaredGenerator = fragments.FirstOrDefault() ?? "";
            var authenticFolder = fragments.Length >= 3 &&
                string.Equals(declaredGenerator, analyzer.Assembly.Name, StringComparison.Ordinal);
            var contents = File.ReadAllText(source, Encoding.UTF8);
            var matchingXaml = xamlInputs.Where(x =>
                contents.Contains(Path.GetFileName(x), StringComparison.OrdinalIgnoreCase) ||
                relative.Contains(Path.GetFileNameWithoutExtension(x), StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var xamlSource = matchingXaml.Length == 1
                ? Path.GetRelativePath(projectDirectory, matchingXaml[0]).Replace('\\', '/')
                : "UNMAPPED_XAML_INPUT_SET_SHA256:" + xamlFingerprint;
            var attested = authenticFolder && matchingXaml.Length == 1;
            evidence.Add(source, new GeneratedSourceProvenance(source, relative,
                declaredGenerator, analyzerVersion, analyzerHash, Sha256Text(contents),
                xamlSource, xamlFingerprint, attested));
            Console.WriteLine("DOC_GENERATED_PROVENANCE " + relative +
                " generator=" + declaredGenerator + " version=" + analyzerVersion +
                " sourceHash=" + Sha256Text(contents) + " xaml=" + xamlSource +
                " attested=" + attested);
        }
        Console.WriteLine("DOC_COVERAGE_NATIVE_GENERATED_SOURCES count=" + generated.Length +
            " provenanceAttested=" + evidence.Values.Count(x => x.Attested));
        return new RealProjectInputs
        {
            AssemblyName = Get("AssemblyName"),
            TargetPath = targetPath,
            Configuration = Get("Configuration"),
            TargetFramework = Get("TargetFramework"),
            Nullable = Get("Nullable"),
            LangVersion = Get("LangVersion"),
            Defines = Get("DefineConstants").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries),
            Sources = sources.Concat(generated).Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            References = refs.Order(StringComparer.OrdinalIgnoreCase).ToArray(),
            GeneratedProvenance = evidence
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
        // Raw ECMA-335 metadata includes internal members hidden by a reference
        // compilation's accessibility policy; never exclude them from the audit.
        using var file = File.OpenRead(TargetPath);
        using var pe = new PEReader(file);
        var reader = pe.GetMetadataReader();
        var emitted = new HashSet<string>(StringComparer.Ordinal);
        var typeNames = new Dictionary<TypeDefinitionHandle, string>();
        string TypeName(TypeDefinitionHandle handle)
        {
            if (typeNames.TryGetValue(handle, out var cached)) return cached;
            var def = reader.GetTypeDefinition(handle);
            var simple = reader.GetString(def.Name);
            var parent = def.GetDeclaringType();
            var ns = reader.GetString(def.Namespace);
            var full = parent.IsNil ? (ns.Length == 0 ? simple : ns + "." + simple)
                : TypeName(parent) + "." + simple;
            typeNames[handle] = full;
            return full;
        }
        foreach (var handle in reader.TypeDefinitions)
        {
            var def = reader.GetTypeDefinition(handle);
            var full = TypeName(handle);
            emitted.Add("T:" + full);
            foreach (var mhandle in def.GetMethods())
            {
                var method = reader.GetMethodDefinition(mhandle);
                var name = reader.GetString(method.Name);
                var count = method.GetParameters().Count(h =>
                    reader.GetParameter(h).SequenceNumber > 0);
                emitted.Add("M:" + full + "." + name + "|" + count);
            }
            foreach (var fieldHandle in def.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                emitted.Add("F:" + full + "." + reader.GetString(field.Name));
            }
            foreach (var propHandle in def.GetProperties())
            {
                var prop = reader.GetPropertyDefinition(propHandle);
                emitted.Add("P:" + full + "." + reader.GetString(prop.Name));
            }
            foreach (var eventHandle in def.GetEvents())
            {
                var ev = reader.GetEventDefinition(eventHandle);
                emitted.Add("E:" + full + "." + reader.GetString(ev.Name));
            }
        }
        static int CountParameters(string parameters)
        {
            if (parameters.Length == 0) return 0;
            int depth = 0, n = 1;
            foreach (var c in parameters)
            {
                if (c is '{' or '[' or '<') depth++;
                else if (c is '}' or ']' or '>') depth--;
                else if (c == ',' && depth == 0) n++;
            }
            if (depth != 0) throw new InvalidDataException("DOC_ID_PARAMETERS_INVALID");
            return n;
        }
        static string Key(string id)
        {
            if (id.Length < 3 || id[1] != ':')
                throw new InvalidDataException("DOC_ID_FORMAT_INVALID");
            if (id[0] is 'T' or 'F' or 'E') return id;
            var sig = id.IndexOf('(');
            var stem = sig < 0 ? id : id[..sig];
            if (id[0] == 'P') return stem;
            if (id[0] != 'M') throw new InvalidDataException("DOC_ID_KIND_UNKNOWN");
            var last = stem.LastIndexOf('.');
            if (last < 2) throw new InvalidDataException("DOC_ID_METHOD_INVALID");
            var method = stem[(last + 1)..].Replace("#ctor", ".ctor", StringComparison.Ordinal)
                .Replace("#cctor", ".cctor", StringComparison.Ordinal);
            var genericIndex = method.IndexOf(new string((char)96, 2), StringComparison.Ordinal);
            if (genericIndex >= 0) method = method[..genericIndex];
            var count = sig < 0 ? 0 : CountParameters(id[(sig + 1)..id.IndexOf(')', sig)]);
            return stem[..last] + "." + method + "|" + count;
        }
        var ids = eligibleIds.Distinct(StringComparer.Ordinal).ToArray();
        var missing = ids.Where(id => !emitted.Contains(Key(id))).ToArray();
        if (missing.Length != 0)
            throw new InvalidDataException("REAL_ASSEMBLY_DECLARED_API_MISSING:" +
                missing.Length + ":" + string.Join(",", missing.Take(12)));
        // Unlike name+parameter-count, exact documentation IDs preserve the
        // parameter TYPE signature (including overloads) and generic arity.
        // Roslyn's default metadata import hides internal members, therefore
        // this independent audit explicitly imports non-public metadata.
        var exactAudit = CSharpCompilation.Create("DocCoverageExactSignatureAudit",
            references: References.Select(x => MetadataReference.CreateFromFile(x)).Append(
                MetadataReference.CreateFromFile(TargetPath)),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                metadataImportOptions: MetadataImportOptions.All));
        var assemblyRef = exactAudit.References.Last();
        var exactAssembly = exactAudit.GetAssemblyOrModuleSymbol(assemblyRef) as IAssemblySymbol
            ?? throw new InvalidDataException("DOC_EXACT_SIGNATURE_ASSEMBLY_UNAVAILABLE");
        var exactIds = new HashSet<string>(StringComparer.Ordinal);
        void VisitExact(ISymbol symbol)
        {
            var id = symbol.GetDocumentationCommentId();
            if (!string.IsNullOrWhiteSpace(id)) exactIds.Add(id);
            if (symbol is INamespaceSymbol ns)
            {
                foreach (var member in ns.GetMembers()) VisitExact(member);
            }
            else if (symbol is INamedTypeSymbol type)
            {
                foreach (var member in type.GetMembers()) VisitExact(member);
            }
        }
        VisitExact(exactAssembly.GlobalNamespace);
        var missingExact = ids.Where(id => !exactIds.Contains(id)).ToArray();
        if (missingExact.Length != 0)
            throw new InvalidDataException("REAL_ASSEMBLY_EXACT_DECLARATION_SIGNATURE_MISSING:" +
                missingExact.Length + ":" + string.Join(",", missingExact.Take(12)));
        Console.WriteLine("DOC_COVERAGE_EXACT_SIGNATURES_PASS " +
            "sourceEligible=" + ids.Length + " metadataDeclarations=" + exactIds.Count +
            " strictOverloadTypeIds=true internalImport=all");

        Console.WriteLine("DOC_COVERAGE_REAL_ASSEMBLY_CROSSCHECK_PASS " +
            "symbols=" + ids.Length + " metadataMembers=" + emitted.Count +
            " referenceAssemblies=" + References.Length +
            " compiledSources=" + Sources.Length +
            " defines=" + string.Join(",", Defines));
    }
}
