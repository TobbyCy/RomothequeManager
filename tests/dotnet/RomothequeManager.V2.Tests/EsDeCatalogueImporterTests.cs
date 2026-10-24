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
        Assert.ThrowsExactly<XmlException>(() => session.Import());
        Assert.AreSame(accepted, session.Current);
    }

    [TestMethod]
    public void XmlMalformedAndDtdAreRejected()
    {
        Gamelist("<game><path>broken");
        Assert.ThrowsExactly<XmlException>(() => EsDeCatalogueImporter.Read(_root));
        Put("MEDIAS/gba/metadata/es-de-gamelist.xml",
            "<!DOCTYPE gameList [<!ENTITY e SYSTEM 'file:///secret'>]><gameList/>");
        Assert.ThrowsExactly<XmlException>(() => EsDeCatalogueImporter.Read(_root));
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
    public void MissingXmlIsFailureNotFalseEmptySuccess()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(_root));
    }
}
