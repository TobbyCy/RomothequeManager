#if NEXT04_IMPORT_CI
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace RomothequeManager.V2;

// CI-only entrypoint in the real native Windows executable. It cannot be
// compiled into the normal portable binary without an explicit CI build flag.
internal static class Next04ImportEngineHarness
{
    internal static int Run(string fixtureRoot)
    {
        // Do not provide a generic CLI import entrypoint for user libraries.
        var temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            Environment.GetEnvironmentVariable("RM_NEXT04_IMPORT_TEST_ONLY") != "1" ||
            string.IsNullOrWhiteSpace(temp))
            throw new InvalidOperationException("NEXT04_IMPORT_TEST_ENV_REQUIRED");

        var root = Path.GetFullPath(fixtureRoot);
        var tempPrefix = Path.GetFullPath(temp).TrimEnd(Path.DirectorySeparatorChar) +
            Path.DirectorySeparatorChar;
        if (!root.StartsWith(tempPrefix, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(root).Equals("demo", StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(Path.GetDirectoryName(root)!).StartsWith(
                "rm-next04-import-engine-", StringComparison.Ordinal))
            throw new InvalidOperationException("NEXT04_IMPORT_TEST_ROOT_NOT_GENERATED");

        var manifest = Path.Combine(root, "manifest.json");
        if (!File.Exists(manifest) || new FileInfo(manifest).Length > 65536)
            throw new InvalidOperationException("NEXT04_IMPORT_TEST_MANIFEST_REQUIRED");
        using (var doc = JsonDocument.Parse(File.ReadAllText(manifest)))
            if (!doc.RootElement.GetProperty("synthetic").GetBoolean() ||
                doc.RootElement.GetProperty("schemaVersion").GetString() != "demo-library-root/1")
                throw new InvalidOperationException("NEXT04_IMPORT_TEST_SYNTHETIC_ONLY");

        var idealRoot = Path.Combine(root, "IDEAL");
        var chaosRoot = Path.Combine(root, "CHAOS");
        var mysteryRoot = Path.Combine(root, "MYSTERY");
        foreach (var variant in new[] { idealRoot, chaosRoot, mysteryRoot })
            if (!File.Exists(Path.Combine(variant, "manifest.json")) ||
                !Directory.Exists(Path.Combine(variant, "ROMS")))
                throw new InvalidOperationException("NEXT04_IMPORT_TEST_VARIANT_MISSING");

        var ideal = new EsDeImportSession(idealRoot).Import();
        Require(ideal.SourceKind == "ES_DE_READ_ONLY" && ideal.Games.Count == 30,
            "IDEAL_30_REFERENCES");
        Require(ideal.Games.Select(x => x.Platform).Distinct().Count() == 13,
            "IDEAL_13_SYSTEMS");
        Require(ideal.Games.Any(x => x.Genre is not null && x.Developer is not null &&
            x.ReleaseDate is not null), "IDEAL_DECLARED_METADATA");
        var chaos = new EsDeImportSession(chaosRoot).Import();
        Require(chaos.Games.Count == 30, "CHAOS_30_REFERENCES");
        Require(chaos.Diagnostics.Count(x => x.Code == "ROM_MISSING") == 2,
            "CHAOS_TWO_MISSING_REFERENCES");
        try
        {
            _ = new EsDeImportSession(mysteryRoot).Import();
            throw new InvalidOperationException("MYSTERY_FALSE_SUCCESS");
        }
        catch (InvalidDataException)
        {
            Console.WriteLine("NEXT04_ENGINE_MYSTERY_NO_XML_REJECTED");
        }

        var scratch = Path.Combine(Path.GetDirectoryName(root)!, "engine-scratch");
        Directory.CreateDirectory(scratch);
        foreach (var convention in new[] { "local", "historic", "modern" })
            CheckConvention(scratch, convention);
        CheckAtomicReimport(scratch);
        CheckLargeSynthetic(scratch);

        Console.WriteLine("NEXT04_IMPORT_ENGINE_NATIVE_PASS source=DEMO01_SYNTHETIC " +
            "ideal=30/13 chaos=30/2missing mystery=REJECTED " +
            "conventions=3 lastGood=KEPT metadata=DECLARED onlyReadOnly=1");
        return 0;
    }

    private static void CheckConvention(string scratch, string convention)
    {
        var root = Path.Combine(scratch, "convention-" + convention);
        var rom = Path.Combine(root, "ROMS", "gba");
        Directory.CreateDirectory(rom);
        File.WriteAllText(Path.Combine(rom, "Test.gba"), "SYNTHETIC_NON_EXECUTABLE");
        var xml = convention switch
        {
            "local" => Path.Combine(rom, "gamelist.xml"),
            "historic" => Path.Combine(root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"),
            "modern" => Path.Combine(root, "ES-DE", "gamelists", "gba", "gamelist.xml"),
            _ => throw new InvalidOperationException("INVALID_TEST_CONVENTION")
        };
        Directory.CreateDirectory(Path.GetDirectoryName(xml)!);
        var xmlText = "<gameList><game><path>./Test.gba</path><name>Native &amp; fictif</name>" +
            "<genre>Aventure</genre><developer>Studio Test</developer>" +
            "<releasedate>20010101T000000</releasedate></game></gameList>";
        File.WriteAllText(xml, xmlText, new UTF8Encoding(false));
        var sourceHash = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(xml));
        var session = new EsDeImportSession(root);
        var before = session.Import();
        var after = session.Import();
        Require(before.Games.Count == 1 &&
            before.Games.Single().Genre == "Aventure" &&
            before.Games.Single().Developer == "Studio Test" &&
            before.Games.Single().ReleaseDate == "20010101T000000" &&
            after.ContentFingerprint == before.ContentFingerprint &&
            after.LibraryId == before.LibraryId, "CONVENTION_" + convention);
        Require(sourceHash.SequenceEqual(System.Security.Cryptography.SHA256.HashData(
            File.ReadAllBytes(xml))), "SOURCE_IMMUTABLE_" + convention);
        Console.WriteLine("NEXT04_ENGINE_CONVENTION_PASS kind=" + convention);
    }

    private static void CheckAtomicReimport(string scratch)
    {
        var root = Path.Combine(scratch, "last-good");
        var xml = Path.Combine(root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(xml)!);
        Directory.CreateDirectory(Path.Combine(root, "ROMS", "gba"));
        const string a = "<gameList><game><path>./A.gba</path>" +
            "<genre>Un</genre></game></gameList>";
        const string b = "<gameList><game><path>./A.gba</path>" +
            "<genre>Deux</genre></game></gameList>";
        File.WriteAllText(xml, a);
        var session = new EsDeImportSession(root);
        var first = session.Import();
        File.WriteAllText(xml, b);
        var current = session.Import();
        Require(first.Games.Single().GameId == current.Games.Single().GameId &&
            first.ContentFingerprint != current.ContentFingerprint &&
            first.LibraryId == current.LibraryId, "METADATA_ONLY_IDENTITY");

        File.WriteAllText(xml, "<gameList><game><path>");
        try
        {
            _ = session.Import();
            throw new InvalidOperationException("MALFORMED_FALSE_SUCCESS");
        }
        catch (EsDeXmlReadException)
        {
            Require(ReferenceEquals(current, session.Current), "LASTGOOD_AFTER_XML_FAILURE");
        }
        File.WriteAllText(xml, "<!DOCTYPE gameList><gameList/>");
        try
        {
            _ = session.Import();
            throw new InvalidOperationException("DTD_FALSE_SUCCESS");
        }
        catch (EsDeXmlReadException)
        {
            Require(ReferenceEquals(current, session.Current), "LASTGOOD_AFTER_DTD_FAILURE");
        }
        Console.WriteLine("NEXT04_ENGINE_LASTGOOD_PASS metadataOnly=1 xmlRejected=1 dtdRejected=1");
    }

    private static void CheckLargeSynthetic(string scratch)
    {
        var root = Path.Combine(scratch, "large");
        var xml = Path.Combine(root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        Directory.CreateDirectory(Path.GetDirectoryName(xml)!);
        Directory.CreateDirectory(Path.Combine(root, "ROMS", "gba"));
        var builder = new StringBuilder("<gameList>");
        for (var i = 0; i < 2000; i++)
            builder.Append("<game><path>./FAKE_").Append(i.ToString("D4"))
                .Append(".gba</path><name>Original Simulation</name></game>");
        builder.Append("</gameList>");
        File.WriteAllText(xml, builder.ToString(), new UTF8Encoding(false));
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var timer = Stopwatch.StartNew();
        var result = EsDeCatalogueImporter.Read(root);
        timer.Stop();
        var delta = GC.GetTotalMemory(forceFullCollection: false) - before;
        Require(result.Games.Count == 2000 && result.Diagnostics.Count(x =>
            x.Code == "ROM_MISSING") == 2000, "LARGE_2000_REFERENCES");
        Console.WriteLine("NEXT04_ENGINE_PERF_SYNTHETIC count=2000 elapsed_ms=" +
            timer.ElapsedMilliseconds + " managed_delta_bytes=" + delta +
            " xml_bytes=" + new FileInfo(xml).Length);
    }

    private static void Require(bool valid, string code)
    {
        if (!valid) throw new InvalidDataException("NEXT04_ENGINE_ASSERT_" + code);
    }
}
#endif
