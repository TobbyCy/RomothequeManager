using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

/// <summary>ESDE-FP-V2 #142: invented XML in isolated temporary folders only.</summary>
[TestClass]
public sealed class EsDeFingerprintV2Tests
{
    private readonly List<string> _roots = new();

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in _roots)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private string Fixture(string title, string? description)
    {
        var root = Path.Combine(Path.GetTempPath(), "rm-fp-v2-" + Guid.NewGuid().ToString("N"));
        _roots.Add(root);
        var roms = Path.Combine(root, "ROMS", "gba");
        var medias = Path.Combine(root, "MEDIAS", "gba", "metadata");
        Directory.CreateDirectory(roms);
        Directory.CreateDirectory(medias);
        File.WriteAllText(Path.Combine(roms, "Invented.gba"), "FIXTURE_CONTENT", new UTF8Encoding(false));
        static string X(string s) => System.Security.SecurityElement.Escape(s)!;
        var desc = description is null ? "" : "<desc>" + X(description) + "</desc>";
        File.WriteAllText(Path.Combine(medias, "es-de-gamelist.xml"),
            "<gameList><game><path>./Invented.gba</path><name>" + X(title) +
            "</name>" + desc + "</game></gameList>", new UTF8Encoding(false));
        return root;
    }

    // The V1 evidence concatenation incorrectly equates the two projections:
    // "A|B|C" == "A|B|C". The hash algorithm itself is not colliding.
    [TestMethod]
    public void FP_01_V1_DelimiterCollisionRequiresDistinctV2Fingerprints()
    {
        var left = EsDeCatalogueImporter.Read(Fixture("A|B", "C"));
        var right = EsDeCatalogueImporter.Read(Fixture("A", "B|C"));
        Assert.AreEqual(left.Games.Single().GameId, right.Games.Single().GameId);
        Assert.AreNotEqual(left.Games.Single().Title, right.Games.Single().Title);
        Assert.AreNotEqual(left.ContentFingerprint, right.ContentFingerprint,
            "Different TLV projections must not share a fingerprint.");
    }

    [TestMethod]
    public void FP_14_CollidingV1EvidenceMustNotPassRelocateVerified()
    {
        var original = Fixture("A|B", "C");
        var changed = Fixture("A", "B|C");
        var session = new EsDeImportSession(original);
        var accepted = session.Import();
        Assert.ThrowsExactly<InvalidDataException>(() => session.RelocateVerified(changed));
        Assert.AreSame(accepted, session.Current);
        var reimported = session.Import();
        Assert.AreEqual(accepted.LibraryId, reimported.LibraryId);
        Assert.AreEqual(accepted.ContentFingerprint, reimported.ContentFingerprint);
    }

    [TestMethod]
    public void FP_11_FingerprintMustCarryVersionAndFullSha256()
    {
        var catalogue = EsDeCatalogueImporter.Read(Fixture("Invented", null));
        StringAssert.StartsWith(catalogue.ContentFingerprint, "esde-fp-v2:sha256:");
        var digest = catalogue.ContentFingerprint["esde-fp-v2:sha256:".Length..];
        Assert.AreEqual(64, digest.Length);
        Assert.IsTrue(digest.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'));
    }
}
