using System.Security.Cryptography;
using System.Text;
using System.Xml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

// Only temporary, invented files. Never points to a user's collection.
[TestClass]
public sealed class EsDeCatalogueImporterTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-next01a-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "gba"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "metadata"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root))
        {
            foreach (var file in Directory.GetFiles(_root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_root, recursive: true);
        }
    }

    private string Put(string relative, string content = "synthetic fixture")
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private void Gamelist(string body) => Put("MEDIAS/gba/metadata/es-de-gamelist.xml",
        "<gameList>" + body + "</gameList>");

    [TestMethod]
    public void PreparedLibraryReadsHomonymsMultiDiscFolderAndCover()
    {
        Put("ROMS/gba/Edition-A.gba");
        Put("ROMS/gba/Edition-B.gba");
        Put("ROMS/gba/Disc-1.chd");
        Put("ROMS/gba/Disc-2.chd");
        Put("ROMS/gba/Compilation.m3u", "Disc-1.chd\nDisc-2.chd\n");
        Put("MEDIAS/gba/covers/first.png");
        Gamelist("""
            <game><path>./Edition-A.gba</path><name>Nom identique</name>
              <desc>Édition A</desc><image>../covers/first.png</image></game>
            <game><path>./Edition-B.gba</path><name>Nom identique</name>
              <image>../covers/absent.png</image></game>
            <game><path>./Compilation.m3u</path><name>Deux disques</name></game>
            <folder><path>./dossier</path><name>Dossier</name></folder>
            """);

        var snapshot = EsDeCatalogueImporter.Read(_root);
        Assert.AreEqual("ES_DE_READ_ONLY", snapshot.SourceKind);
        Assert.AreEqual(3, snapshot.Games.Count);
        Assert.AreEqual(3, snapshot.Games.Select(g => g.GameId).Distinct().Count());
        Assert.AreEqual(2, snapshot.Games.Count(g => g.Title == "Nom identique"));
        Assert.IsTrue(snapshot.Games.All(g => g.RomExists));
        Assert.AreEqual(2, snapshot.Games.Single(g => g.Title == "Deux disques").DiscPaths.Count);
        Assert.IsTrue(snapshot.Games.Single(g => g.Description == "Édition A").Media.Single().Exists);
        Assert.IsFalse(snapshot.Games.Single(g => g.RomRelativePath.EndsWith("Edition-B.gba")).Media.Single().Exists);
        CollectionAssert.IsSubsetOf(new[] { "MEDIA_MISSING", "FOLDER_ENTRY" },
            snapshot.Diagnostics.Select(d => d.Code).ToArray());
    }

    [TestMethod]
    public void ReimportIdenticalIsIdempotentAndSourcesStayUnchanged()
    {
        var rom = Put("ROMS/gba/Example.gba", "fake ROM text");
        var xml = Put("MEDIAS/gba/metadata/es-de-gamelist.xml",
            "<gameList><game><path>./Example.gba</path><name>Exemple</name></game></gameList>");
        var romHash = SHA256.HashData(File.ReadAllBytes(rom));
        var xmlHash = SHA256.HashData(File.ReadAllBytes(xml));
        var session = new EsDeImportSession(_root);
        var before = session.Import();
        var after = session.Import();
        Assert.AreEqual(before.ContentFingerprint, after.ContentFingerprint);
        Assert.AreEqual(before.Games.Single().GameId, after.Games.Single().GameId);
        CollectionAssert.AreEqual(romHash, SHA256.HashData(File.ReadAllBytes(rom)));
        CollectionAssert.AreEqual(xmlHash, SHA256.HashData(File.ReadAllBytes(xml)));
    }

    [TestMethod]
    public void FailedSecondImportKeepsLastValidSnapshot()
    {
        Put("ROMS/gba/Example.gba");
        Gamelist("<game><path>./Example.gba</path><name>Exemple</name></game>");
        var session = new EsDeImportSession(_root);
        var accepted = session.Import();
        Gamelist("<game><path>broken");
        Assert.ThrowsExactly<EsDeXmlReadException>(() => session.Import());
        Assert.AreSame(accepted, session.Current);
    }

    [TestMethod]
    public void XmlMalformedAndDtdAreRejected()
    {
        Gamelist("<game><path>broken");
        var malformed = Assert.ThrowsExactly<EsDeXmlReadException>(() => EsDeCatalogueImporter.Read(_root));
        Assert.AreEqual("gba", malformed.Platform);
        Assert.IsTrue(malformed.InnerException is XmlException);
        Assert.IsTrue(malformed.LineNumber > 0);
        Assert.IsFalse(EsDeDiagnosticPresentation.ImportFailure(malformed).Contains(_root, StringComparison.Ordinal));

        Put("MEDIAS/gba/metadata/es-de-gamelist.xml",
            "<!DOCTYPE gameList [<!ENTITY e SYSTEM 'file:///secret'>]><gameList/>");
        var dtd = Assert.ThrowsExactly<EsDeXmlReadException>(() => EsDeCatalogueImporter.Read(_root));
        Assert.AreEqual("gba", dtd.Platform);
        Assert.IsTrue(dtd.InnerException is XmlException);
    }

    [TestMethod]
    public void TraversalAbsoluteNetworkAndMediaEscapeAreRejected()
    {
        foreach (var path in new[] { "../../outside.gba", "C:/secret.gba", "//server/share/game.gba",
                     "https://example.invalid/game.gba" })
        {
            Gamelist("<game><path>" + path + "</path></game>");
            Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
        }
        Put("ROMS/gba/Safe.gba");
        Gamelist("<game><path>./Safe.gba</path><image>../../../../secret.png</image></game>");
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
    }

    [TestMethod]
    public void MissingRomDoesNotCountAsDeletionOrEraseEntry()
    {
        Gamelist("<game><path>./Unplugged.gba</path><name>Hors ligne</name></game>");
        var snapshot = EsDeCatalogueImporter.Read(_root);
        Assert.AreEqual(1, snapshot.Games.Count);
        Assert.IsFalse(snapshot.Games[0].RomExists);
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "ROM_MISSING"));
    }

    [TestMethod]
    public void LocalGamelistConventionIsSupportedButTwoSourcesAreAmbiguous()
    {
        Put("ROMS/gba/Local.gba");
        var local = Put("ROMS/gba/gamelist.xml",
            "<gameList><game><path>./Local.gba</path><name>Local</name></game></gameList>");
        Assert.AreEqual(1, EsDeCatalogueImporter.Read(_root).Games.Count);
        Gamelist("<game><path>./Local.gba</path></game>");
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
        Assert.IsTrue(File.Exists(local));
    }

    [TestMethod]
    public void MovingSameSyntheticLibraryPreservesIdentity()
    {
        Put("ROMS/gba/Same.gba");
        Gamelist("<game><path>./Same.gba</path><name>Stable</name></game>");
        var original = EsDeCatalogueImporter.Read(_root);
        var moved = Path.Combine(Path.GetTempPath(), "rm-next01a-move-" + Guid.NewGuid().ToString("N"));
        Directory.Move(_root, moved);
        try
        {
            var relocated = EsDeCatalogueImporter.Read(moved);
            Assert.AreEqual(original.Games.Single().GameId, relocated.Games.Single().GameId);
            Assert.AreEqual(original.ContentFingerprint, relocated.ContentFingerprint);
        }
        finally
        {
            Directory.Move(moved, _root);
        }
    }

    [TestMethod]
    public void ReadOnlyXmlWorksWithoutMutation()
    {
        Put("ROMS/gba/Safe.gba");
        Gamelist("<game><path>./Safe.gba</path><name>Lecture seule</name></game>");
        var xml = Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        var hash = SHA256.HashData(File.ReadAllBytes(xml));
        File.SetAttributes(xml, FileAttributes.ReadOnly);
        Assert.AreEqual(1, EsDeCatalogueImporter.Read(_root).Games.Count);
        CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(xml)));
    }

    [TestMethod]
    public void SessionLibraryIdSurvivesAddedGameWhileFingerprintChanges()
    {
        Put("ROMS/gba/First.gba");
        Gamelist("<game><path>./First.gba</path><name>Premier</name></game>");
        var session = new EsDeImportSession(_root);
        var before = session.Import();
        Put("ROMS/gba/Second.gba");
        Gamelist("""
            <game><path>./First.gba</path><name>Premier</name></game>
            <game><path>./Second.gba</path><name>Deuxième</name></game>
            """);
        var after = session.Import();
        Assert.AreEqual(before.LibraryId, after.LibraryId);
        Assert.AreNotEqual(before.ContentFingerprint, after.ContentFingerprint);
        Assert.AreEqual(2, after.Games.Count);
    }

    [TestMethod]
    public void ExplicitVerifiedRelocationPreservesSessionIdentityAndMediaAccess()
    {
        Put("ROMS/gba/First.gba");
        Put("MEDIAS/gba/covers/first.png", "synthetic cover");
        Gamelist("<game><path>./First.gba</path><image>../covers/first.png</image></game>");
        var session = new EsDeImportSession(_root);
        var first = session.Import();
        var relocatedRoot = Path.Combine(Path.GetTempPath(), "rm-next01a-relocated-" + Guid.NewGuid().ToString("N"));
        Directory.Move(_root, relocatedRoot);
        try
        {
            var moved = session.RelocateVerified(relocatedRoot);
            Assert.AreEqual(first.LibraryId, moved.LibraryId);
            Assert.AreEqual(first.ContentFingerprint, moved.ContentFingerprint);
            var media = moved.Games.Single().Media.Single();
            Assert.IsTrue(session.TryOpenMedia(media, out var stream));
            using (stream) Assert.IsFalse(stream!.CanWrite);
        }
        finally
        {
            Directory.Move(relocatedRoot, _root);
        }
    }

    [TestMethod]
    public void RelocationRefusesChangedContentAndKeepsExistingState()
    {
        Put("ROMS/gba/First.gba");
        Gamelist("<game><path>./First.gba</path></game>");
        var session = new EsDeImportSession(_root);
        var initial = session.Import();
        var anotherRoot = Path.Combine(Path.GetTempPath(), "rm-next01a-other-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(anotherRoot, "ROMS", "gba"));
        Directory.CreateDirectory(Path.Combine(anotherRoot, "MEDIAS", "gba", "metadata"));
        File.WriteAllText(Path.Combine(anotherRoot, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"),
            "<gameList><game><path>./Different.gba</path></game></gameList>");
        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() => session.RelocateVerified(anotherRoot));
            Assert.AreSame(initial, session.Current);
        }
        finally
        {
            Directory.Delete(anotherRoot, recursive: true);
        }
    }

    [TestMethod]
    public void MediaIsRevalidatedWhenOpenedNotTrustedFromImport()
    {
        Put("ROMS/gba/First.gba");
        var coverPath = Put("MEDIAS/gba/covers/first.png", "fake cover bytes");
        Gamelist("""
            <game><path>./First.gba</path><image>../covers/first.png</image>
            <thumbnail>../covers/missing.png</thumbnail></game>
            """);
        var session = new EsDeImportSession(_root);
        var current = session.Import();
        var cover = current.Games.Single().Media.Single(x => x.Kind == "image");
        var missing = current.Games.Single().Media.Single(x => x.Kind == "thumbnail");
        Assert.IsTrue(session.TryOpenMedia(cover, out var opened));
        using (opened) Assert.IsFalse(opened!.CanWrite);
        Assert.IsFalse(session.TryOpenMedia(missing, out var absent));
        Assert.IsNull(absent);
        Assert.IsFalse(session.TryOpenMedia(new EsDeMedia("image", "../escape.png", true), out var forged));
        Assert.IsNull(forged);
        File.Delete(coverPath);
        Assert.IsFalse(session.TryOpenMedia(cover, out var removed));
        Assert.IsNull(removed);
    }

    [TestMethod]
    public void MediaChangedToReparsePointIsNotOpened()
    {
        Put("ROMS/gba/First.gba");
        var coverPath = Put("MEDIAS/gba/covers/first.png", "fake cover");
        var otherPath = Put("other/target.png", "outside media root");
        Gamelist("<game><path>./First.gba</path><image>../covers/first.png</image></game>");
        var session = new EsDeImportSession(_root);
        var cover = session.Import().Games.Single().Media.Single();
        File.Delete(coverPath);
        try
        {
            File.CreateSymbolicLink(coverPath, otherPath);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException or PlatformNotSupportedException)
        {
            // CI host cannot create links: the implementation still refuses existing reparse points.
            return;
        }
        Assert.IsFalse(session.TryOpenMedia(cover, out var stream));
        Assert.IsNull(stream);
    }


    [TestMethod]
    public void Next04MetadataIsOptionalStableAndFingerprintReflectsEdits()
    {
        Put("ROMS/gba/Exemple.gba");
        var xml = Put("MEDIAS/gba/metadata/es-de-gamelist.xml",
            "<gameList><game><path>./Exemple.gba</path><name>École &amp; rêves</name>" +
            "<desc>Un &amp; deux</desc><genre>Action / RPG</genre>" +
            "<developer>Équipe &amp; Co</developer>" +
            "<releasedate>19951221T000000</releasedate></game></gameList>");
        var originalBytes = SHA256.HashData(File.ReadAllBytes(xml));
        var session = new EsDeImportSession(_root);
        var first = session.Import();
        var game = first.Games.Single();
        Assert.AreEqual("École & rêves", game.Title);
        Assert.AreEqual("Un & deux", game.Description);
        Assert.AreEqual("Action / RPG", game.Genre);
        Assert.AreEqual("Équipe & Co", game.Developer);
        Assert.AreEqual("19951221T000000", game.ReleaseDate);
        Assert.AreEqual("ES_DE_READ_ONLY", first.SourceKind);
        CollectionAssert.AreEqual(originalBytes, SHA256.HashData(File.ReadAllBytes(xml)));
        var again = session.Import();
        Assert.AreEqual(first.LibraryId, again.LibraryId);
        Assert.AreEqual(game.GameId, again.Games.Single().GameId);
        Assert.AreEqual(first.ContentFingerprint, again.ContentFingerprint);

        Gamelist("<game><path>./Exemple.gba</path><name>École &amp; rêves</name>" +
            "<desc>Un &amp; deux</desc><genre>Stratégie</genre>" +
            "<developer>Équipe &amp; Co</developer><releasedate>19951221T000000</releasedate></game>");
        var edited = session.Import();
        Assert.AreEqual(first.LibraryId, edited.LibraryId);
        Assert.AreEqual(game.GameId, edited.Games.Single().GameId);
        Assert.AreEqual("Stratégie", edited.Games.Single().Genre);
        Assert.AreNotEqual(first.ContentFingerprint, edited.ContentFingerprint);
    }

    [TestMethod]
    public void Next04AbsentAndMalformedOptionalMetadataNeverInventsValuesOrDropsGame()
    {
        Put("ROMS/gba/Safe.gba");
        Gamelist("<game><path>./Safe.gba</path><name>Sans métadonnées</name></game>");
        var legacy = EsDeCatalogueImporter.Read(_root);
        Assert.IsNull(legacy.Games.Single().Genre);
        Assert.IsNull(legacy.Games.Single().Developer);
        Assert.IsNull(legacy.Games.Single().ReleaseDate);
        Assert.IsFalse(legacy.Diagnostics.Any(d => d.Code is "METADATA_FIELD_INVALID" or "RELEASE_DATE_UNSUPPORTED"));

        Gamelist("<game><path>./Safe.gba</path><name>Sans métadonnées</name>" +
            "<genre> </genre><developer>A</developer><developer>B</developer>" +
            "<releasedate>20250230T000000</releasedate></game>");
        var after = EsDeCatalogueImporter.Read(_root);
        Assert.AreEqual(1, after.Games.Count);
        Assert.AreEqual(legacy.Games.Single().GameId, after.Games.Single().GameId);
        Assert.AreEqual("Sans métadonnées", after.Games.Single().Title);
        Assert.IsNull(after.Games.Single().Genre);
        Assert.IsNull(after.Games.Single().Developer);
        Assert.IsNull(after.Games.Single().ReleaseDate);
        Assert.AreEqual(1, after.Diagnostics.Count(d => d.Code == "METADATA_FIELD_INVALID"));
        Assert.AreEqual(1, after.Diagnostics.Count(d => d.Code == "RELEASE_DATE_UNSUPPORTED"));
    }

    [TestMethod]
    public void Next04XmlUtf16BomAndCharactersAreParsedWithoutLocaleDependence()
    {
        Put("ROMS/gba/Safe.gba");
        var xml = Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        var content = "<?xml version=\"1.0\" encoding=\"utf-16\"?>" +
            "<gameList><game><path>./Safe.gba</path>" +
            "<name>Étoile &amp; Cœur 🎮</name><genre>Rôle</genre>" +
            "<releasedate>2000-02-29</releasedate></game></gameList>";
        var bytes = Encoding.Unicode.GetPreamble()
            .Concat(Encoding.Unicode.GetBytes(content)).ToArray();
        File.WriteAllBytes(xml, bytes);
        var original = SHA256.HashData(File.ReadAllBytes(xml));
        var imported = EsDeCatalogueImporter.Read(_root);
        var game = imported.Games.Single();
        Assert.AreEqual("Étoile & Cœur 🎮", game.Title);
        Assert.AreEqual("Rôle", game.Genre);
        Assert.AreEqual("2000-02-29", game.ReleaseDate);
        CollectionAssert.AreEqual(original, SHA256.HashData(File.ReadAllBytes(xml)));
    }

    [TestMethod]
    public void Next04WellFormedButUnsupportedRootIsNotCalledMalformedXml()
    {
        Put("ROMS/gba/Safe.gba");
        Gamelist("<game><path>./Safe.gba</path></game>");
        var session = new EsDeImportSession(_root);
        var previous = session.Import();
        Put("MEDIAS/gba/metadata/es-de-gamelist.xml",
            "<otherList><game><path>./Safe.gba</path></game></otherList>");
        var unsupported = Assert.ThrowsExactly<InvalidDataException>(() => session.Import());
        Assert.IsFalse(unsupported is EsDeXmlReadException);
        Assert.AreSame(previous, session.Current);
        var displayed = EsDeDiagnosticPresentation.ImportFailure(unsupported);
        Assert.IsTrue(displayed.Contains("structure", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(displayed.Contains(_root, StringComparison.Ordinal));
    }

    [TestMethod]
    public void MissingXmlIsFailureNotFalseEmptySuccess()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
    }
}
