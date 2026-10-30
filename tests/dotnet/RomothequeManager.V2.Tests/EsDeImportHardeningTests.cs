using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

/// <summary>Adversarial ES-DE import tests using invented files in unique temporary roots only.</summary>
[TestClass]
public sealed class EsDeImportHardeningTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-n04-hardening-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "gba"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (!Directory.Exists(_root)) return;
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        Directory.Delete(_root, recursive: true);
    }

    private string Put(string relative, string contents = "SIMULATED_NON_EXECUTABLE")
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, contents, new UTF8Encoding(false));
        return file;
    }

    private static string Game(string rom = "./Example.gba", string metadata = "") =>
        "<game><path>" + rom + "</path><name>Épreuve &amp; contrôle</name>" +
        "<genre>RPG</genre><developer>Équipe Test</developer>" +
        "<releasedate>20010203T000000</releasedate>" + metadata + "</game>";

    private static string Convention(string kind) => kind switch
    {
        "local" => "ROMS/gba/gamelist.xml",
        "historical" => "MEDIAS/gba/metadata/es-de-gamelist.xml",
        "modern" => "ES-DE/gamelists/gba/gamelist.xml",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    [TestMethod]
    [DataRow("local")]
    [DataRow("historical")]
    [DataRow("modern")]
    public void ThreeXmlConventionsPreserveMetadataAndReadOnlySource(string kind)
    {
        var rom = Put("ROMS/gba/Example.gba");
        var xml = Put(Convention(kind), "<gameList>" + Game() + "</gameList>");
        var romHash = SHA256.HashData(File.ReadAllBytes(rom));
        var xmlHash = SHA256.HashData(File.ReadAllBytes(xml));
        var session = new EsDeImportSession(_root);
        var first = session.Import();
        var reference = first.Games.Single();
        Assert.AreEqual("Épreuve & contrôle", reference.Title);
        Assert.AreEqual("RPG", reference.Genre);
        Assert.AreEqual("Équipe Test", reference.Developer);
        Assert.AreEqual("20010203T000000", reference.ReleaseDate);
        Assert.IsTrue(reference.RomExists);
        Assert.AreEqual("ES_DE_READ_ONLY", first.SourceKind);
        var second = session.Import();
        Assert.AreEqual(first.LibraryId, second.LibraryId);
        Assert.AreEqual(first.ContentFingerprint, second.ContentFingerprint);
        Assert.AreEqual(reference.GameId, second.Games.Single().GameId);
        CollectionAssert.AreEqual(xmlHash, SHA256.HashData(File.ReadAllBytes(xml)));
        CollectionAssert.AreEqual(romHash, SHA256.HashData(File.ReadAllBytes(rom)));
    }

    [TestMethod]
    public void MissingMalformedDtdOversizedAndRootFailurePreserveLastGood()
    {
        Put("ROMS/gba/Example.gba");
        var xml = Put(Convention("historical"), "<gameList>" + Game() + "</gameList>");
        var session = new EsDeImportSession(_root);
        var accepted = session.Import();

        File.WriteAllText(xml, "<gameList><game><path>.", new UTF8Encoding(false));
        var malformed = Assert.ThrowsExactly<EsDeXmlReadException>(() => session.Import());
        Assert.AreEqual("gba", malformed.Platform);
        Assert.AreSame(accepted, session.Current);

        File.WriteAllText(xml, "<!DOCTYPE gameList [<!ENTITY ext SYSTEM 'file:///private'>]>" +
            "<gameList/>", new UTF8Encoding(false));
        Assert.ThrowsExactly<EsDeXmlReadException>(() => session.Import());
        Assert.AreSame(accepted, session.Current);

        File.WriteAllText(xml, "<gameList>" + new string(' ', 8 * 1024 * 1024 + 1) +
            "</gameList>", new UTF8Encoding(false));
        var oversized = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_XML_BYTES_LIMIT", oversized.Message);
        Assert.AreSame(accepted, session.Current);
        Assert.IsFalse(EsDeDiagnosticPresentation.ImportFailure(oversized)
            .Contains(_root, StringComparison.Ordinal));

        File.Delete(xml);
        Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreSame(accepted, session.Current);
        Assert.AreEqual("20010203T000000", session.Current!.Games.Single().ReleaseDate);

        Assert.ThrowsExactly<DirectoryNotFoundException>(() =>
            session.RelocateVerified(Path.Combine(_root, "ROMS")));
        Assert.AreSame(accepted, session.Current);
    }

    [TestMethod]
    public void PlaylistReadsBoundedBytesLinesAndUtf16Bom()
    {
        Put("ROMS/gba/Disc1.chd");
        Put("ROMS/gba/Disc2.chd");
        var playlist = Put("ROMS/gba/Two.m3u", "Disc1.chd\nDisc2.chd\n");
        Put(Convention("historical"), "<gameList>" + Game("./Two.m3u") + "</gameList>");
        var session = new EsDeImportSession(_root);
        var accepted = session.Import();
        Assert.AreEqual(2, accepted.Games.Single().DiscPaths.Count);

        File.WriteAllText(playlist, new string('A', 64 * 1024 + 1));
        var byteError = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_PLAYLIST_BYTES_LIMIT", byteError.Message);
        Assert.AreSame(accepted, session.Current);

        File.WriteAllText(playlist, string.Concat(Enumerable.Repeat("#only comment\n", 257)));
        var lineError = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_PLAYLIST_LINES_LIMIT", lineError.Message);
        Assert.AreSame(accepted, session.Current);

        var utf16 = Encoding.Unicode.GetPreamble().Concat(
            Encoding.Unicode.GetBytes("Disc1.chd\r\nDisc2.chd\r\n")).ToArray();
        File.WriteAllBytes(playlist, utf16);
        var reopened = session.Import();
        Assert.AreEqual(2, reopened.Games.Single().DiscPaths.Count);
        Assert.AreEqual(accepted.LibraryId, reopened.LibraryId);
        Assert.AreEqual(accepted.ContentFingerprint, reopened.ContentFingerprint);
    }

    [TestMethod]
    public void DuplicatePathsDifferingOnlyByCaseAreRejectedPortable()
    {
        Put("ROMS/gba/Example.gba");
        Put(Convention("historical"), "<gameList>" + Game() +
            Game("./example.gba") + "</gameList>");
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            EsDeCatalogueImporter.Read(_root));
        StringAssert.Contains(error.Message, "Duplicated ROM identity");
    }

    [TestMethod]
    public void DistinctPlatformFoldersDifferingByCaseAreRejectedWhenSupported()
    {
        Put("ROMS/gba/Example.gba");
        Put(Convention("historical"), "<gameList>" + Game() + "</gameList>");
        Put("ROMS/GBA/Example.gba");
        Put("ROMS/GBA/gamelist.xml", "<gameList><game><path>./Example.gba</path></game></gameList>");
        var names = Directory.GetDirectories(Path.Combine(_root, "ROMS")).Select(Path.GetFileName).ToArray();
        if (names.Distinct(StringComparer.Ordinal).Count() == 2)
        {
            var error = Assert.ThrowsExactly<InvalidDataException>(() =>
                EsDeCatalogueImporter.Read(_root));
            Assert.AreEqual("ESDE_PLATFORM_CASE_COLLISION", error.Message);
        }
        else
        {
            // A case-insensitive test host aliases both directories to one folder.
            Assert.AreEqual(1, names.Length);
        }
    }

    [TestMethod]
    public void CaseSensitiveSiblingMediaRootCannotEscapeOnLinux()
    {
        if (OperatingSystem.IsWindows()) return;
        Put("ROMS/gba/Example.gba");
        Put("medias/covers/outside.png", "OUTSIDE_APPROVED_MEDIA_DOMAIN");
        Put(Convention("historical"), "<gameList>" +
            Game(metadata: "<image>../../../medias/covers/outside.png</image>") + "</gameList>");
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            EsDeCatalogueImporter.Read(_root));
        StringAssert.Contains(error.Message, "escapes its explicitly authorized root");
    }

    [TestMethod]
    public void ModernMediaCannotReadConfigurationAndMissingRomNeverMeansDeletion()
    {
        Put("ES-DE/settings/private.xml", "INVENTED_SETTINGS_NOT_MEDIA");
        Put(Convention("modern"), "<gameList>" +
            Game("./NotPresent.gba", "<image>../../settings/private.xml</image>") + "</gameList>");
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
        Put(Convention("modern"), "<gameList>" +
            Game("./NotPresent.gba", "<image>../../downloaded_media/gba/covers/nope.png</image>") +
            "</gameList>");
        var imported = EsDeCatalogueImporter.Read(_root);
        Assert.AreEqual(1, imported.Games.Count);
        Assert.IsFalse(imported.Games.Single().RomExists);
        Assert.IsTrue(imported.Diagnostics.Any(d => d.Code == "ROM_MISSING"));
        Assert.IsTrue(imported.Diagnostics.Any(d => d.Code == "MEDIA_MISSING"));
    }

    [TestMethod]
    public void TwoThousandSyntheticReferencesCompleteWithinBoudedXmlAndLogMeasurements()
    {
        var sb = new StringBuilder("<gameList>");
        for (var i = 0; i < 2000; i++)
        {
            var name = "SYNTHETIC_GAME_" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture);
            sb.Append("<game><path>./").Append(name)
                .Append(".gba</path><name>").Append(name).Append("</name></game>");
        }
        sb.Append("</gameList>");
        var xml = Put(Convention("historical"), sb.ToString());
        Assert.IsTrue(new FileInfo(xml).Length < 8 * 1024 * 1024);
        var before = GC.GetTotalMemory(forceFullCollection: true);
        var clock = Stopwatch.StartNew();
        var imported = EsDeCatalogueImporter.Read(_root);
        clock.Stop();
        var after = GC.GetTotalMemory(forceFullCollection: false);
        Assert.AreEqual(2000, imported.Games.Count);
        Assert.AreEqual(2000, imported.Games.Select(x => x.GameId).Distinct().Count());
        Assert.AreEqual(2000, imported.Diagnostics.Count(x => x.Code == "ROM_MISSING"));
        Console.WriteLine("NEXT04_IMPORT_PERF_SYNTHETIC count=2000 elapsed_ms=" +
            clock.ElapsedMilliseconds + " managed_delta_bytes=" + (after - before) +
            " source_xml_bytes=" + new FileInfo(xml).Length);
    }

    [TestMethod]
    public void ExactEightMiBXmlIsAcceptedButNextByteIsRejected()
    {
        const int maxBytes = 8 * 1024 * 1024;
        const string open = "<gameList>";
        const string close = "</gameList>";
        var payload = open + new string(' ', maxBytes - open.Length - close.Length) + close;
        var xml = Put(Convention("historical"), payload);
        Assert.AreEqual(maxBytes, new FileInfo(xml).Length);
        // A well-formed but empty gamelist is distinct from a missing XML.
        var session = new EsDeImportSession(_root);
        var complete = session.Import();
        Assert.AreEqual(0, complete.Games.Count);
        File.AppendAllText(xml, " ", new UTF8Encoding(false));
        Assert.AreEqual(maxBytes + 1, new FileInfo(xml).Length);
        var limit = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_XML_BYTES_LIMIT", limit.Message);
        Assert.AreSame(complete, session.Current);
    }

    [TestMethod]
    public void Exact64KiBPlaylistIsAcceptedButNextByteIsRejected()
    {
        const int maxBytes = 64 * 1024;
        var playlist = Put("ROMS/gba/Bounded.m3u", "#" + new string(' ', maxBytes - 2) + "\n");
        Assert.AreEqual(maxBytes, new FileInfo(playlist).Length);
        Put(Convention("historical"), "<gameList>" + Game("./Bounded.m3u") + "</gameList>");
        var session = new EsDeImportSession(_root);
        var complete = session.Import();
        Assert.AreEqual(0, complete.Games.Single().DiscPaths.Count);
        File.AppendAllText(playlist, " ", new UTF8Encoding(false));
        Assert.AreEqual(maxBytes + 1, new FileInfo(playlist).Length);
        var limit = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_PLAYLIST_BYTES_LIMIT", limit.Message);
        Assert.AreSame(complete, session.Current);
    }


    [TestMethod]
    public void Exactly256PlaylistLinesAcceptedAnd257Rejected()
    {
        var playlist = Put("ROMS/gba/Lines.m3u",
            string.Concat(Enumerable.Repeat("# comment\n", 256)));
        Put(Convention("historical"), "<gameList>" + Game("./Lines.m3u") + "</gameList>");
        var session = new EsDeImportSession(_root);
        var accepted = session.Import();
        Assert.AreEqual(0, accepted.Games.Single().DiscPaths.Count);
        File.AppendAllText(playlist, "# extra\n", new UTF8Encoding(false));
        var error = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_PLAYLIST_LINES_LIMIT", error.Message);
        Assert.AreSame(accepted, session.Current);
    }

    [TestMethod]
    public void InvalidUtf8PlaylistBytesRejectWithoutChangingCurrent()
    {
        var playlist = Put("ROMS/gba/Invalid.m3u", "Header\n");
        Put(Convention("historical"), "<gameList>" + Game("./Invalid.m3u") + "</gameList>");
        var session = new EsDeImportSession(_root);
        var accepted = session.Import();
        File.WriteAllBytes(playlist, [0xC3, 0x28, 0x0A]);
        var error = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_PLAYLIST_ENCODING_UNSUPPORTED", error.Message);
        Assert.AreSame(accepted, session.Current);
    }


    private const string FictionalGame =
        "<gameList><game><path>./Invented.gba</path>" +
        "<name>Fixture Only</name><genre>Test</genre></game></gameList>";
    private const string KnownAlternative =
        "<alternativeEmulator><label>TEST EMULATOR (Synthetic)</label></alternativeEmulator>";

    [TestMethod]
    [DataRow("local")]
    [DataRow("historical")]
    [DataRow("modern")]
    public void PublicEsDeAlternativeEmulatorBeforeGameListImportsInAllConventions(string convention)
    {
        var rom = Put("ROMS/gba/Invented.gba");
        var xml = Put(Convention(convention),
            "<?xml version=\"1.0\"?>\n" + KnownAlternative + "\n" + FictionalGame);
        var romHash = SHA256.HashData(File.ReadAllBytes(rom));
        var xmlHash = SHA256.HashData(File.ReadAllBytes(xml));
        var session = new EsDeImportSession(_root);
        var imported = session.Import();
        Assert.AreEqual("ES_DE_READ_ONLY", imported.SourceKind);
        Assert.AreEqual(1, imported.Games.Count);
        Assert.AreEqual("Fixture Only", imported.Games.Single().Title);
        Assert.AreEqual("Test", imported.Games.Single().Genre);
        Assert.IsTrue(imported.Games.Single().RomExists);
        Assert.IsFalse(imported.Diagnostics.Any(d => d.Code == "IGNORED_ENTRY"));

        // The alternative emulator label is not a game, metadata or identity input.
        File.WriteAllText(xml, FictionalGame, new UTF8Encoding(false));
        var conventional = session.Import();
        Assert.AreEqual(imported.LibraryId, conventional.LibraryId);
        Assert.AreEqual(imported.Games.Single().GameId, conventional.Games.Single().GameId);
        Assert.AreEqual(imported.ContentFingerprint, conventional.ContentFingerprint);

        File.WriteAllText(xml, "<?xml version=\"1.0\"?>\n" +
            "<alternativeEmulator><label>ANOTHER FICTIONAL LABEL</label></alternativeEmulator>\n" +
            FictionalGame, new UTF8Encoding(false));
        var reimported = session.Import();
        Assert.AreEqual(conventional.ContentFingerprint, reimported.ContentFingerprint);
        Assert.AreEqual(conventional.Games.Single().GameId, reimported.Games.Single().GameId);
        CollectionAssert.AreEqual(romHash, SHA256.HashData(File.ReadAllBytes(rom)));
        Assert.IsFalse(xmlHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(xml))));
    }

    [TestMethod]
    public void MixedElevenSyntheticSystemsImportSixOrdinaryAndFiveAlternativePreambles()
    {
        const int systems = 11;
        for (var i = 0; i < systems; i++)
        {
            var platform = i == 0 ? "gba" : "synthetic" + i.ToString("D2");
            Put("ROMS/" + platform + "/Invented.gba");
            var xml = i < 5
                ? "<?xml version=\"1.0\"?>\n" + KnownAlternative + "\n" + FictionalGame
                : "<?xml version=\"1.0\"?>\n" + FictionalGame;
            Put("MEDIAS/" + platform + "/metadata/es-de-gamelist.xml", xml);
        }
        var result = EsDeCatalogueImporter.Read(_root);
        Assert.AreEqual(systems, result.Games.Count);
        Assert.AreEqual(systems, result.Games.Select(g => g.Platform).Distinct().Count());
        Assert.IsTrue(result.Games.All(g => g.RomExists));
        Assert.AreEqual(11, result.Games.Select(g => g.GameId).Distinct().Count());
        Assert.AreEqual(0, result.Diagnostics.Count(d => d.Code == "XML_MISSING"));
    }

    [TestMethod]
    public void KnownAlternativeEmulatorUtf8AndUtf16DeclarationsPreserveReadOnlyFiles()
    {
        Put("ROMS/gba/Invented.gba");
        var xml = Put(Convention("historical"), FictionalGame);
        var fixture = "<?xml version=\"1.0\" encoding=\"utf-16\"?>\n" +
            KnownAlternative + "\n" + FictionalGame;
        File.WriteAllBytes(xml, Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(fixture)).ToArray());
        var hash = SHA256.HashData(File.ReadAllBytes(xml));
        var imported = EsDeCatalogueImporter.Read(_root);
        Assert.AreEqual("Fixture Only", imported.Games.Single().Title);
        CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(xml)));

        File.WriteAllBytes(xml, Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(fixture.Replace("utf-16", "utf-8", StringComparison.Ordinal))).ToArray());
        var utf8Hash = SHA256.HashData(File.ReadAllBytes(xml));
        Assert.AreEqual("Fixture Only", EsDeCatalogueImporter.Read(_root).Games.Single().Title);
        CollectionAssert.AreEqual(utf8Hash, SHA256.HashData(File.ReadAllBytes(xml)));
    }

    [TestMethod]
    public void MalformedOrUnauthorizedAlternativePreamblesNeverReplaceCurrent()
    {
        Put("ROMS/gba/Invented.gba");
        var xml = Put(Convention("historical"), FictionalGame);
        var session = new EsDeImportSession(_root);
        var lastGood = session.Import();
        var invalid = new[]
        {
            "<alternativeEmulator/>" + FictionalGame,
            "<alternativeEmulator><label>   </label></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator><label>Test</label><label>Again</label></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator><label><game>Nested</game></label></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator id=\"surprise\"><label>Test</label></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator><label x=\"1\">Test</label></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator><name>Wrong</name></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator><label>Test</label>INJECTED</alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator xmlns=\"urn:untrusted\"><label>Test</label></alternativeEmulator>" + FictionalGame,
            "<alternativeEmulator><label>Test</label></alternativeEmulator><gameList/><gameList/>",
            "<alternativeEmulator><label>Test</label></alternativeEmulator><gameList/><foreign/>",
            KnownAlternative + KnownAlternative + FictionalGame,
            FictionalGame + KnownAlternative,
            "<otherRoot/>" + FictionalGame,
            KnownAlternative + "<otherRoot/>",
            KnownAlternative + "<gameList><game><path>./Invented.gba</path>",
            KnownAlternative + "<gameList/>FORBIDDEN_TEXT",
            KnownAlternative + "<gameList/><![CDATA[UNEXPECTED]]>",
            KnownAlternative + "<?unexpected test?>" + FictionalGame
        };
        for (var i = 0; i < invalid.Length; i++)
        {
            File.WriteAllText(xml, invalid[i], new UTF8Encoding(false));
            try
            {
                session.Import();
                Assert.Fail("Invalid synthetic preamble unexpectedly imported, fixture index " + i);
            }
            catch (EsDeXmlReadException)
            {
                Assert.AreSame(lastGood, session.Current);
            }
        }
    }

    [TestMethod]
    public void AlternativeEmulatorCannotBypassDtdEntityXmlBudgetOrTraversalProtections()
    {
        Put("ROMS/gba/Invented.gba");
        var xml = Put(Convention("historical"), FictionalGame);
        var session = new EsDeImportSession(_root);
        var good = session.Import();

        File.WriteAllText(xml, "<!DOCTYPE gameList [<!ENTITY test SYSTEM 'file:///invented'>]>" +
            KnownAlternative + "<gameList><game><path>./Invented.gba</path>" +
            "<name>&test;</name></game></gameList>", new UTF8Encoding(false));
        Assert.ThrowsExactly<EsDeXmlReadException>(() => session.Import());
        Assert.AreSame(good, session.Current);

        File.WriteAllText(xml, "<?xml version=\"1.0\"?>\n" + KnownAlternative +
            "\n<gameList><game><path>../../escape.gba</path></game></gameList>",
            new UTF8Encoding(false));
        Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreSame(good, session.Current);

        File.WriteAllText(xml, "<?xml version=\"1.0\"?>\n" + KnownAlternative +
            "\n<gameList>" + new string(' ', 8 * 1024 * 1024) + "</gameList>",
            new UTF8Encoding(false));
        var limit = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.AreEqual("ESDE_XML_BYTES_LIMIT", limit.Message);
        Assert.AreSame(good, session.Current);

        File.WriteAllText(xml, "<?xml version=\"1.0\"?>\n" + KnownAlternative +
            "\n<gameList><game><path>./Invented.gba</path>" +
            "<name>unescaped & invalid</name></game></gameList>", new UTF8Encoding(false));
        Assert.ThrowsExactly<EsDeXmlReadException>(() => session.Import());
        Assert.AreSame(good, session.Current);
    }

}
