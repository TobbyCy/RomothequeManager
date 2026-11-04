using System.Security;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Inventory.RsA;
using SkiaSharp;

namespace RomothequeManager.V2.Tests;

/// <summary>Entirely fictional, disposable XML/ROMS/MEDIAS structure tests.
/// No personal source, ROM contents, network, content hash or game decisions.</summary>
[TestClass]
public sealed class Next05ReconciliationStructureTests
{
    private string _root = "";
    private string _mediaRoot = "";
    private static readonly string[] Platforms = ["gb", "ps1", "ps2", "ps3"];
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        Platforms.ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
    private static readonly IReadOnlyDictionary<string, MediaKind> Roles =
        new Dictionary<string, MediaKind> { ["covers"] = MediaKind.Cover };

    [TestInitialize]
    public void Initialize()
    {
        // RS-A only authorizes this exact self-generated temp naming convention.
        _root = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"));
        _mediaRoot = Path.Combine(_root, "MEDIAS");
        Directory.CreateDirectory(_mediaRoot);
        var xml = Platforms.ToDictionary(x => x, _ => new StringBuilder());

        for (var i = 1; i <= 19; i++)
        {
            var stem = $"Fiction-GB-{i:D2}";
            Inert("ROMS/gb/" + stem + ".gb");
            Cover("gb/" + stem + ".png");
            Entry(xml["gb"], stem + ".gb", stem, "../covers/" + stem + ".png");
        }

        // 53 invented PS1 stems each have a BIN sidecar plus a CUE and a CHD.
        // The two XML entries are independent references, not duplicate decisions.
        for (var i = 1; i <= 53; i++)
        {
            var stem = $"Fiction-PS1-{i:D3}";
            Inert("ROMS/ps1/" + stem + ".bin");
            Inert("ROMS/ps1/" + stem + ".cue");
            Inert("ROMS/ps1/" + stem + ".chd");
            Cover("ps1/" + stem + ".png");
            Entry(xml["ps1"], stem + ".cue", stem, "../covers/" + stem + ".png");
            Entry(xml["ps1"], stem + ".chd", stem);
        }

        // Two functional PS2 folders. Their distinction is in the source path,
        // not an assertion that ISO and CHD files are equivalent.
        foreach (var stem in new[] { "Fiction-PS2-ISO-01", "Fiction-PS2-ISO-02" })
        {
            Inert("ROMS/ps2/ISOS/" + stem + ".iso");
            Entry(xml["ps2"], "ISOS/" + stem + ".iso", stem,
                stem.EndsWith("01", StringComparison.Ordinal)
                    ? "../covers/" + stem + ".png" : null);
            if (stem.EndsWith("01", StringComparison.Ordinal))
                Cover("ps2/" + stem + ".png");
        }
        foreach (var stem in new[] { "Fiction-PS2-CHD-01", "Fiction-PS2-CHD-02" })
        {
            Inert("ROMS/ps2/CHD/" + stem + ".chd");
            Entry(xml["ps2"], "CHD/" + stem + ".chd", stem,
                stem.EndsWith("01", StringComparison.Ordinal)
                    ? "../covers/" + stem + ".png" : null);
            Cover("ps2/" + stem + ".png"); // CHD-02 is a local candidate, not XML-declared.
        }
        Entry(xml["ps2"], "ISOS/Fiction-PS2-Unavailable.iso",
            "Fiction-PS2-Unavailable");

        // One PS3 folder reference has the structural PARAM.SFO + EBOOT.BIN
        // markers. Twelve inert internal files are observations, not games.
        const string ps3 = "Fiction-PS3-Directory";
        Inert("ROMS/ps3/" + ps3 + "/PS3_GAME/PARAM.SFO");
        Inert("ROMS/ps3/" + ps3 + "/PS3_GAME/USRDIR/EBOOT.BIN");
        for (var i = 1; i <= 10; i++)
            Inert($"ROMS/ps3/{ps3}/PS3_GAME/USRDIR/INTERNAL_{i:D2}.dat");
        Cover("ps3/" + ps3 + ".png");
        Entry(xml["ps3"], ps3, ps3, "../covers/" + ps3 + ".png");

        foreach (var platform in Platforms)
            Inert("MEDIAS/" + platform + "/metadata/es-de-gamelist.xml",
                "<gameList>" + xml[platform] + "</gameList>");
    }

    private string Inert(string relative, string contents = "FICTIONAL_TEST_METADATA_ONLY")
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents, Encoding.UTF8);
        return path;
    }

    private void Cover(string relative)
    {
        var source = Path.Combine(_mediaRoot,
            relative.Replace('/', Path.DirectorySeparatorChar));
        var path = Path.Combine(Path.GetDirectoryName(source)!, "covers",
            Path.GetFileName(source));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bitmap = new SKBitmap(3, 3);
        bitmap.Erase(SKColors.SkyBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 85);
        File.WriteAllBytes(path, encoded.ToArray());
    }

    private static void Entry(StringBuilder xml, string path, string name, string? image = null)
    {
        xml.Append("<game><path>./").Append(path).Append("</path><name>")
            .Append(name).Append("</name>");
        if (image is not null) xml.Append("<image>").Append(image).Append("</image>");
        xml.Append("</game>");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_root.Length > 0 &&
            Path.GetFileName(_root).StartsWith("rm-rsa-fixture-", StringComparison.Ordinal) &&
            Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    private InventorySnapshot Native()
    {
        var reader = new RsAFixtureRomSession(new RsABudget(
            MaxDepth: 8, MaxFiles: 500, MaxDirectories: 100,
            TimeLimit: TimeSpan.FromMinutes(2), 0));
        reader.SelectFixture(Path.Combine(_root, "ROMS"), Guid.NewGuid().ToString("D"));
        // Selection and consent are separate; no bytes are read from these inert files.
        Assert.AreEqual(InventoryScanStatus.Failed, reader.Scan(null).ScanStatus);
        var grant = reader.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(2));
        var result = reader.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus,
            string.Join(",", result.Diagnostics.Select(x => x.Code)));
        Assert.AreEqual(InventoryScanStatus.Failed, reader.Scan(grant).ScanStatus,
            "One-shot token must not be replayable.");
        Assert.AreSame(result, reader.Current);
        return result;
    }

    private MediaIndexSnapshot IndexedMedia()
    {
        var service = new MediaIndexApplicationService(s =>
            s.MediaRoot == _mediaRoot);
        var scope = new MediaRootScope(_mediaRoot, Aliases, Roles,
            MaxDepth: 4, MaxFiles: 100, MaxDirectories: 40,
            TimeLimit: TimeSpan.FromMinutes(1));
        var failed = service.ScanConfirmed(scope, explicitConsent: false);
        Assert.AreEqual(InventoryScanStatus.Failed, failed.ScanStatus);
        var approved = service.ScanConfirmed(scope, explicitConsent: true);
        Assert.AreEqual(InventoryScanStatus.Complete, approved.ScanStatus);
        return approved;
    }

    [TestMethod]
    public void REC_01_ExactReferenceVsObservedFilesAndMediaCandidateMatrix()
    {
        if (!OperatingSystem.IsWindows()) return;
        var catalogue = EsDeCatalogueImporter.Read(_root);
        var roms = Native();
        var media = IndexedMedia();
        Assert.AreEqual(131, catalogue.Games.Count); // GB19 PS1(53x2) PS2(4+missing) PS3(1)
        Assert.AreEqual(130, catalogue.Games.Count(x => x.RomExists),
            "An actual PS3 game directory is available; only the fictitious PS2 file is absent.");
        Assert.AreEqual(1, catalogue.Diagnostics.Count(x => x.Code == "ROM_MISSING"));
        Assert.AreEqual(75, catalogue.Games.Sum(x => x.Media.Count)); // XML-declared
        Assert.AreEqual(75, catalogue.Games.Sum(x => x.Media.Count(m => m.Exists)));
        Assert.AreEqual(194, roms.ObservedFileCount); // GB19 + PS1(53x3) + PS2(4) + PS3(12)
        Assert.AreEqual(12, roms.Observations.Count(x => x.PlatformKey == "ps3"),
            "PS3 technical contents remain visible as file evidence, not games.");
        Assert.AreEqual(76, media.ObservedFileCount); // 75 referenced images + one extra PS2 cover
        Assert.AreEqual(76, media.Observations.Count(x => x.HeaderValidated));
        Assert.AreEqual(0, roms.PotentialDuplicates.Count,
            "Filename and basename overlap is not a proven physical duplicate.");

        // A claim is from the imported XML, never fabricated from a matching title.
        var observed = roms.Observations.ToDictionary(x => x.RelativePath,
            StringComparer.Ordinal);
        var claims = catalogue.Games.SelectMany(game =>
        {
            if (!game.RomRelativePath.StartsWith("ROMS/", StringComparison.Ordinal) ||
                !observed.TryGetValue(game.RomRelativePath["ROMS/".Length..], out var rom))
                return Array.Empty<ExplicitMediaReference>();
            return game.Media.Where(m => m.Kind == "image" && m.Exists &&
                m.RelativePath.StartsWith("MEDIAS/", StringComparison.Ordinal))
                .Select(m => new ExplicitMediaReference(rom.ObservationId,
                    m.RelativePath, "ESDE_XML_ONLY", "SYNTHETIC_GAMELIST")).ToArray();
        }).ToArray();
        Assert.AreEqual(74, claims.Length,
            "The PS3 directory has no fake file observation; its XML claim stays separate.");
        var mapping = VerifiedScopeMapping.FromExplicitSelections(roms, media, Aliases,
            "FOUR_EXPLICIT_SYNTHETIC_ROOTS");
        var rows = MediaLinkProjection.Reconcile(roms, media, mapping, claims);
        var rowById = roms.Observations.ToDictionary(x => x.ObservationId);
        Assert.AreEqual(182, rows.Count,
            "PS3_GAME internal EBOOT.BIN is not a standalone media/game candidate.");
        Assert.AreEqual(74, rows.Count(x => x.Status == MediaLinkStatus.EXACT_REFERENCE));
        Assert.AreEqual(106, rows.Count(x => x.Status == MediaLinkStatus.AMBIGUOUS),
            "53 BIN/CUE/CHD basename families remain ambiguous for CHD and BIN.");
        Assert.AreEqual(1, rows.Count(x =>
            x.Status == MediaLinkStatus.UNIQUE_CONVENTION_CANDIDATE));
        Assert.AreEqual(1, rows.Count(x => x.Status == MediaLinkStatus.UNMATCHED));
        Assert.IsFalse(rows.Any(x =>
            rowById[x.RomObservationId].PlatformKey == "ps3"),
            "Files internal to a PS3_GAME tree must not become game candidates.");
        var gallery = new MediaPresentationService().Build(roms, media, mapping, claims);
        Assert.AreEqual(1, gallery.UnlinkedMedias.Count,
            "PS3 cover is observed and declared but has no trustworthy file-game link.");
        Assert.IsTrue(gallery.UnlinkedMedias.Single().RelativePath.StartsWith(
            "MEDIAS/ps3/covers/", StringComparison.Ordinal));
        Assert.IsTrue(rows.All(x => x.Status != MediaLinkStatus.EXACT_REFERENCE ||
            x.SourceProvenance.Any(p => p.Contains("SYNTHETIC_GAMELIST"))));
        Assert.AreEqual(53, Enumerable.Range(1, 53).Count(i =>
        {
            var stem = $"Fiction-PS1-{i:D3}";
            return roms.Observations.Count(x => x.PlatformKey == "ps1" &&
                Path.GetFileNameWithoutExtension(x.RelativePath) == stem) == 3;
        }), "53 basename groups are variant candidates, never confirmed duplicates.");
        Console.WriteLine("RECON_SYNTHETIC_MATRIX GB_XML=19 GB_ROM=19 GB_COVER=19 GB_EXACT=19 " +
            "PS1_XML=106 PS1_FILES=159 PS1_COVERS=53 PS1_VARIANT_GROUPS=53 " +
            "PS2_XML=5 PS2_FILES=4 PS2_MISSING=1 PS2_COVERS=3 " +
            "PS3_XML=1 PS3_DIR_AVAILABLE=1 PS3_INTERNAL_FILES=12 " +
            "TOTAL_XML=131 AVAILABLE_REFS=130 OBSERVED_FILES=194 " +
            "DECLARED_MEDIA=75 OBSERVED_COVERS=76 BOUND_XML_CLAIMS=74 " +
            "EXACT_LINKS=74 AMBIGUOUS=106 UNIQUE_CANDIDATE=1 UNMATCHED=1");
    }

    [TestMethod]
    public void REC_02_Ps3DirectoryAvailabilityNeverMeansValidatedPlayability()
    {
        var imported = EsDeCatalogueImporter.Read(_root);
        var ps3 = imported.Games.Single(x => x.Platform == "ps3");
        Assert.IsTrue(ps3.RomExists, "A real PS3 game folder is an available path.");
        Assert.AreEqual("ROMS/ps3/Fiction-PS3-Directory", ps3.RomRelativePath);
        Assert.AreEqual(1, ps3.Media.Count);
        Assert.IsTrue(ps3.Media.Single().Exists);
        Assert.IsFalse(imported.Diagnostics.Any(x =>
            x.Code == "ROM_MISSING" && x.Platform == "ps3"));
        Assert.AreEqual(131, imported.Games.Select(x => x.GameId).Distinct().Count());
        Assert.AreEqual(131, imported.Games.Select(x => x.VariantId).Distinct().Count());

        // An arbitrary folder on PS2 is NOT accepted as a game file.
        var xml = Path.Combine(_root, "MEDIAS", "ps2", "metadata", "es-de-gamelist.xml");
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "ps2", "JustAFolder"));
        var doc = XDocument.Load(xml);
        doc.Root!.Add(new XElement("game",
            new XElement("path", "./JustAFolder"),
            new XElement("name", "Not a PS2 file")));
        doc.Save(xml);
        var withPs2Directory = EsDeCatalogueImporter.Read(_root);
        Assert.IsFalse(withPs2Directory.Games.Single(x =>
            x.RomRelativePath == "ROMS/ps2/JustAFolder").RomExists);

        // Present but empty or arbitrary folders are NOT PS3 games.
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "ps3", "EmptyFolder"));
        Inert("ROMS/ps3/ArbitraryFolder/README.txt");
        Inert("ROMS/ps3/PartialFolder/PS3_GAME/PARAM.SFO");
        Inert("ROMS/ps3/WrongMarkerType/PS3_GAME/PARAM.SFO");
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "ps3",
            "WrongMarkerType", "PS3_GAME", "USRDIR", "EBOOT.BIN"));
        var ps3Xml = Path.Combine(_root, "MEDIAS", "ps3", "metadata",
            "es-de-gamelist.xml");
        var ps3Doc = XDocument.Load(ps3Xml);
        foreach (var folder in new[] { "EmptyFolder", "ArbitraryFolder", "PartialFolder",
            "WrongMarkerType" })
            ps3Doc.Root!.Add(new XElement("game",
                new XElement("path", "./" + folder),
                new XElement("name", "Fictional " + folder)));
        ps3Doc.Save(ps3Xml);
        var unstructured = EsDeCatalogueImporter.Read(_root);
        foreach (var folder in new[] { "EmptyFolder", "ArbitraryFolder", "PartialFolder",
            "WrongMarkerType" })
        {
            Assert.IsFalse(unstructured.Games.Single(x =>
                x.RomRelativePath == "ROMS/ps3/" + folder).RomExists,
                "PS3_GAME structural markers are required, not directory existence.");
            Assert.IsTrue(unstructured.Diagnostics.Any(x =>
                x.Code == "ROM_MISSING" && x.Entry == "ROMS/ps3/" + folder));
        }

        // A nonexistent PS3 directory remains unavailable; the correction
        // must not simply promote every PS3 XML reference to "present".
        ps3Doc = XDocument.Load(ps3Xml);
        ps3Doc.Root!.Add(new XElement("game",
            new XElement("path", "./Fiction-PS3-Nonexistent"),
            new XElement("name", "Absent fictitious PS3 folder")));
        ps3Doc.Save(ps3Xml);
        var negative = EsDeCatalogueImporter.Read(_root);
        Assert.IsFalse(negative.Games.Single(x =>
            x.RomRelativePath == "ROMS/ps3/Fiction-PS3-Nonexistent").RomExists);
        Assert.IsTrue(negative.Diagnostics.Any(x =>
            x.Code == "ROM_MISSING" && x.Platform == "ps3"));

        // A standalone PS3 ISO OUTSIDE PS3_GAME remains an ordinary file
        // candidate; only internal PS3_GAME contents are excluded.
        Inert("ROMS/ps3/Fiction-PS3-Standalone.iso");
        Cover("ps3/Fiction-PS3-Standalone.png");
        // Standalone files retain File.Exists independently of PS3_GAME markers.
        ps3Doc = XDocument.Load(ps3Xml);
        ps3Doc.Root!.Add(new XElement("game",
            new XElement("path", "./Fiction-PS3-Standalone.iso"),
            new XElement("name", "Standalone PS3 ISO fictive")));
        ps3Doc.Save(ps3Xml);
        Assert.IsTrue(EsDeCatalogueImporter.Read(_root).Games.Single(x =>
            x.RomRelativePath == "ROMS/ps3/Fiction-PS3-Standalone.iso").RomExists);
        var roms = Native();
        var media = IndexedMedia();
        var mapping = VerifiedScopeMapping.FromExplicitSelections(
            roms, media, Aliases, "SYNTHETIC_PRESENCE_NOT_APPROVAL");
        var suggested = MediaLinkProjection.Reconcile(roms, media, mapping);
        var iso = roms.Observations.Single(x =>
            x.RelativePath == "ps3/Fiction-PS3-Standalone.iso");
        Assert.AreEqual(MediaLinkStatus.AMBIGUOUS,
            suggested.Single(x => x.RomObservationId == iso.ObservationId).Status,
            "A standalone ISO is an archive candidate: never auto-approve its cover.");
    }

    [TestMethod]
    public void REC_03_UntrustedMediaPathAndStaleScopeFailClosed()
    {
        var xml = Path.Combine(_root, "MEDIAS", "gb", "metadata", "es-de-gamelist.xml");
        var doc = XDocument.Load(xml);
        doc.Root!.Elements("game").First().Element("image")!.Value =
            "../../../../outside-the-selected-media/escape.png";
        doc.Save(xml);
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
        if (!OperatingSystem.IsWindows()) return;
        var roms = Native();
        var media = IndexedMedia();
        var mapping = VerifiedScopeMapping.FromExplicitSelections(roms, media, Aliases,
            "SYNTHETIC_INDEPENDENT_SELECTION");
        var stale = MediaLinkProjection.Reconcile(roms, media,
            mapping with { MediaScanId = "stale-run" });
        Assert.IsTrue(stale.All(x => x.Status == MediaLinkStatus.NOT_COMPARABLE),
            "A stale MEDIA run must never imply that a cover is absent or approved.");
    }

    [TestMethod]
    public void REC_04_Ps3MarkerSymlinksAreRefusedNotFollowed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var target = Path.Combine(_root, "ROMS", "ps3", "Fiction-PS3-Directory");
        var linked = Path.Combine(_root, "ROMS", "ps3", "LinkedFolder");
        try
        {
            Directory.CreateSymbolicLink(linked, target);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            Console.WriteLine("REC_04_REPARSE_CAPABILITY_UNAVAILABLE: no symlink created; " +
                ex.GetType().Name);
            return;
        }
        var xml = Path.Combine(_root, "MEDIAS", "ps3", "metadata",
            "es-de-gamelist.xml");
        var doc = XDocument.Load(xml);
        doc.Root!.Add(new XElement("game",
            new XElement("path", "./LinkedFolder"),
            new XElement("name", "Fictional linked folder")));
        doc.Save(xml);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            EsDeCatalogueImporter.Read(_root),
            "Even a link to a structurally valid fixture folder is rejected.");

        // Separately a linked marker must be rejected without opening it.
        Directory.Delete(linked);
        var marker = Path.Combine(target, "PS3_GAME", "PARAM.SFO");
        var inert = Inert("ROMS/ps3/SafeMarker.ps3");
        File.Move(marker, marker + ".ordinary");
        try
        {
            File.CreateSymbolicLink(marker, inert);
            Assert.ThrowsExactly<InvalidDataException>(() =>
                EsDeCatalogueImporter.Read(_root),
                "A linked PARAM.SFO must never be accepted as an ordinary marker.");
        }
        finally
        {
            File.Delete(marker);
            File.Move(marker + ".ordinary", marker);
        }
    }

    [TestMethod]
    public void REC_05_Ps3RomPathTraversalIsDenied()
    {
        var xml = Path.Combine(_root, "MEDIAS", "ps3", "metadata",
            "es-de-gamelist.xml");
        var doc = XDocument.Load(xml);
        doc.Root!.Elements("game").First().Element("path")!.Value =
            "../../../outside-the-authorized-ROMs/escaped.ps3";
        doc.Save(xml);
        Assert.ThrowsExactly<InvalidDataException>(() =>
            EsDeCatalogueImporter.Read(_root),
            "A PS3 XML path cannot escape the selected ROMS scope.");
    }

}
