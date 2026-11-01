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
    private static EsDeGame Game() =>
        new("esde-fixture", "variant-fixture", "gba", "Invented",
            "ROMS/gba/Invented.gba", true, null, Array.Empty<EsDeMedia>(), Array.Empty<string>());

    private static void MustDiffer(EsDeGame left, EsDeGame right)
    {
        var a = EsDeFingerprintV2.CanonicalBytes(new[] { left });
        var b = EsDeFingerprintV2.CanonicalBytes(new[] { right });
        Assert.IsFalse(a.SequenceEqual(b), "Different contract projections must produce different prehash bytes.");
        Assert.AreNotEqual(EsDeFingerprintV2.Compute(new[] { left }),
            EsDeFingerprintV2.Compute(new[] { right }));
    }

    [TestMethod]
    public void FP_02_ColonCommaPipesAndMediaFieldsCannotCrossBoundaries()
    {
        MustDiffer(Game() with { Title = "A,B:C|D", Description = "E" },
            Game() with { Title = "A", Description = "B:C|D,E" });
        MustDiffer(Game() with { Media = new[] { new EsDeMedia("image:cover", "MEDIAS/gba/A.png", true) } },
            Game() with { Media = new[] { new EsDeMedia("image", "cover:MEDIAS/gba/A.png", true) } });
    }

    [TestMethod]
    public void FP_03_NewlinesTabsBackslashesAndQuotesStayInsideLengthPrefixedFields()
    {
        MustDiffer(Game() with { Title = "A\r\nB", Description = "C|D" },
            Game() with { Title = "A\nB", Description = "C|D" });
        MustDiffer(Game() with { Description = "X\tY\\Z\"Q" },
            Game() with { Description = "X\nY\\Z\"Q" });
    }

    [TestMethod]
    public void FP_04_NullEmptyAndSpaceAreThreeDistinctValues()
    {
        MustDiffer(Game() with { Description = null }, Game() with { Description = "" });
        MustDiffer(Game() with { Genre = "" }, Game() with { Genre = " " });
        MustDiffer(Game() with { Developer = null }, Game() with { Developer = "" });
        MustDiffer(Game() with { ReleaseDate = "" }, Game() with { ReleaseDate = null });
    }

    [TestMethod]
    public void FP_05_UnicodeExactCodepointsMultiByteLengthAndInvalidSurrogates()
    {
        MustDiffer(Game() with { Title = "é" }, Game() with { Title = "e\u0301" });
        MustDiffer(Game() with { Title = "École 🎮" }, Game() with { Title = "École 🧪" });
        var invalid = Game() with { Title = new string((char)0xD800, 1) };
        var error = Assert.ThrowsExactly<InvalidDataException>(() =>
            EsDeFingerprintV2.CanonicalBytes(new[] { invalid }));
        Assert.AreEqual("ESDE_FPV2_INVALID_UTF8", error.Message);
    }

    [TestMethod]
    public void FP_06_ListsAreCardinalityPrefixedAndPreserveDuplicates()
    {
        MustDiffer(Game(), Game() with { DiscPaths = new[] { "" } });
        MustDiffer(Game() with { DiscPaths = new[] { "ROMS/gba/A.chd" } },
            Game() with { DiscPaths = new[] { "ROMS/gba/A.chd", "ROMS/gba/A.chd" } });
        var media = new EsDeMedia("image", "MEDIAS/gba/fake.png", false);
        MustDiffer(Game() with { Media = new[] { media } },
            Game() with { Media = new[] { media, media } });
    }

    [TestMethod]
    public void FP_07_GamesAndMediaOrderCanonicalButPlaylistOrderRemainsSignificant()
    {
        var first = Game();
        var second = Game() with { GameId = "esde-2", VariantId = "variant-2",
            RomRelativePath = "ROMS/gba/Second.gba" };
        CollectionAssert.AreEqual(EsDeFingerprintV2.CanonicalBytes(new[] { first, second }),
            EsDeFingerprintV2.CanonicalBytes(new[] { second, first }));
        var img = new EsDeMedia("image", "MEDIAS/gba/z.png", true);
        var thumb = new EsDeMedia("thumbnail", "MEDIAS/gba/a.png", false);
        CollectionAssert.AreEqual(
            EsDeFingerprintV2.CanonicalBytes(new[] { first with { Media = new[] { img, thumb } } }),
            EsDeFingerprintV2.CanonicalBytes(new[] { first with { Media = new[] { thumb, img } } }));
        MustDiffer(first with { DiscPaths = new[] { "D1", "D2" } },
            first with { DiscPaths = new[] { "D2", "D1" } });
    }

    [TestMethod]
    public void FP_08_MediaRepeatedRecordsAndEquivalentSortingKeysRemainDistinctWhenNeeded()
    {
        var a = new EsDeMedia("image", "MEDIAS/gba/cover.png", false);
        var b = new EsDeMedia("image", "MEDIAS/gba/cover.png", true);
        MustDiffer(Game() with { Media = new[] { a, a } },
            Game() with { Media = new[] { a, b } });
        var x = EsDeFingerprintV2.CanonicalBytes(new[] { Game() with { Media = new[] { b, a, a } } });
        var y = EsDeFingerprintV2.CanonicalBytes(new[] { Game() with { Media = new[] { a, b, a } } });
        CollectionAssert.AreEqual(x, y);
    }

    [TestMethod]
    public void FP_09_AllContractFieldsAreCoveredWithoutChangingHistoricalIds()
    {
        var game = Game();
        var mutated = new EsDeGame[]
        {
            game with { GameId = "different" }, game with { VariantId = "different" },
            game with { Platform = "snes" }, game with { Title = "another" },
            game with { RomRelativePath = "ROMS/gba/another.gba" },
            game with { RomExists = false }, game with { Description = "extra" },
            game with { Genre = "RPG" }, game with { Developer = "Studio" },
            game with { ReleaseDate = "20000101T000000" },
            game with { Media = new[] { new EsDeMedia("image", "MEDIAS/gba/a.png", false) } },
            game with { DiscPaths = new[] { "ROMS/gba/D1.chd" } }
        };
        foreach (var change in mutated) MustDiffer(game, change);
        Assert.AreNotEqual(EsDeFingerprintV2.Compute(new[] { game }, "ES_DE_READ_ONLY"),
            EsDeFingerprintV2.Compute(new[] { game }, "OTHER_SOURCE_KIND"));
    }

    [TestMethod]
    public void FP_10_IndependentGoldenVectorsPinExactPreimageBytesAndDigest()
    {
        // Independently generated with Python 3 stdlib struct.pack + hashlib.sha256,
        // not by invoking the C# encoder. The generator and layout are in
        // docs/developer/esde-fp-v2-golden.md.
        const string emptyB64 = "Uk9NT1RIRVFVRTpFU0RFLUZQOlYyAAABAQEAAAAPRVNfREVfUkVBRF9PTkxZAAIDAAAAAA==";
        const string emptySha = "8b2c99a55eb9a53f439a59621e839297c11528e42dbd8dc385e2ca48972cd810";
        CollectionAssert.AreEqual(Convert.FromBase64String(emptyB64),
            EsDeFingerprintV2.CanonicalBytes(Array.Empty<EsDeGame>()));
        Assert.AreEqual("esde-fp-v2:sha256:" + emptySha,
            EsDeFingerprintV2.Compute(Array.Empty<EsDeGame>()));

        const string unicodeB64 =
            "Uk9NT1RIRVFVRTpFU0RFLUZQOlYyAAABAQEAAAAPRVNfREVfUkVBRF9PTkxZAAIDAAAAAQAAAT0AAQEBAAAADGVzZGUtZml4dHVyZQACAQEAAAAPdmFyaWFudC1maXh0dXJlAAMBAQAAAANnYmEABAEBAAAADcOJY29sZXxBCvCfjq4ABQEBAAAAFVJPTVMvZ2JhL0ludmVudGVkLmdiYQAGAgEABwEAAAgBAQAAAAAACQEBAAAAC1N0dWRpbzpUZXN0AAoBAAALAwAAAAIAAAAxAAEBAQAAAAVpbWFnZQACAQEAAAAYTUVESUFTL2diYS9jb3ZlcnMvw6AucG5nAAMCAQAAADcAAQEBAAAACXRodW1ibmFpbAACAQEAAAAaTUVESUFTL2diYS9jb3ZlcnMvbm9uZS5wbmcAAwIAAAwDAAAAAgEAAAATUk9NUy9nYmEvRGlzYzoxLmNoZAEAAAATUk9NUy9nYmEvRGlzYywyLmNoZA==";
        const string unicodeSha = "a0b6e68fd63483448635d4080f5c7dcd82c92f916ce7966bbc599c4b1b99f6d1";
        var rich = new EsDeGame("esde-fixture", "variant-fixture", "gba",
            "École|A\n🎮", "ROMS/gba/Invented.gba", true, null,
            new[] {
                new EsDeMedia("thumbnail", "MEDIAS/gba/covers/none.png", false),
                new EsDeMedia("image", "MEDIAS/gba/covers/à.png", true)
            }, new[] { "ROMS/gba/Disc:1.chd", "ROMS/gba/Disc,2.chd" })
        { Genre = "", Developer = "Studio:Test", ReleaseDate = null };
        CollectionAssert.AreEqual(Convert.FromBase64String(unicodeB64),
            EsDeFingerprintV2.CanonicalBytes(new[] { rich }));
        Assert.AreEqual("esde-fp-v2:sha256:" + unicodeSha,
            EsDeFingerprintV2.Compute(new[] { rich }));
        CollectionAssert.AreEqual(EsDeFingerprintV2.CanonicalBytes(new[] { rich }),
            EsDeFingerprintV2.CanonicalBytes(new[] { rich }));
    }

    [TestMethod]
    public void FP_11_VersionValidationRejectsLegacyUnknownCaseAndMalformedDigests()
    {
        foreach (var malformed in new[] {
            new string('a', 24),
            "esde-fp-v1:sha256:" + new string('a', 64),
            "esde-fp-v3:sha256:" + new string('a', 64),
            "ESDE-FP-V2:SHA256:" + new string('a', 64),
            "esde-fp-v2:sha256:" + new string('A', 64),
            "esde-fp-v2:sha256:" + new string('f', 63),
            "esde-fp-v2:sha256:" + new string('g', 64) })
            Assert.IsFalse(EsDeFingerprintV2.IsSupportedV2Fingerprint(malformed));
        Assert.IsTrue(EsDeFingerprintV2.IsSupportedV2Fingerprint(
            EsDeFingerprintV2.Compute(new[] { Game() })));
    }

    [TestMethod]
    public void FP_12_LegacyCurrentFailClosedBeforeCandidateRootAccess()
    {
        var session = new EsDeImportSession(Fixture("Version V2", null));
        var original = session.Import();
        var legacy = original with { ContentFingerprint = new string('a', 24) };
        typeof(EsDeImportSession).GetProperty(nameof(EsDeImportSession.Current))!
            .SetValue(session, legacy); // synthetic V1 state, not a migration.
        var rejected = Assert.ThrowsExactly<InvalidDataException>(() =>
            session.RelocateVerified(Path.Combine(Path.GetTempPath(), "NO_NEW_ROOT_" + Guid.NewGuid().ToString("N"))));
        StringAssert.Contains(rejected.Message, "unsupported fingerprint version");
        Assert.AreSame(legacy, session.Current);
        var explicitRebaseline = session.Import(); // explicit original root only
        Assert.AreEqual(original.LibraryId, explicitRebaseline.LibraryId);
        Assert.AreEqual(original.Games.Single().GameId, explicitRebaseline.Games.Single().GameId);
        Assert.AreEqual(original.Games.Single().VariantId, explicitRebaseline.Games.Single().VariantId);
        Assert.IsTrue(EsDeFingerprintV2.IsSupportedV2Fingerprint(explicitRebaseline.ContentFingerprint));
    }

    [TestMethod]
    public void FP_13_VerifiedEquivalentRelocationPreservesLibraryGameAndVariantIds()
    {
        var root = Fixture("Same", "Same description");
        var another = Fixture("Same", "Same description");
        var session = new EsDeImportSession(root);
        var original = session.Import();
        var accepted = session.RelocateVerified(another);
        Assert.AreNotSame(original, accepted);
        Assert.AreSame(accepted, session.Current);
        Assert.AreEqual(original.LibraryId, accepted.LibraryId);
        Assert.AreEqual(original.Games.Single().GameId, accepted.Games.Single().GameId);
        Assert.AreEqual(original.Games.Single().VariantId, accepted.Games.Single().VariantId);
        Assert.AreEqual(original.ContentFingerprint, accepted.ContentFingerprint);
        Assert.AreEqual(accepted.ContentFingerprint, session.Import().ContentFingerprint);
    }

    [TestMethod]
    public void FP_15_InvalidRelocationDoesNotAdoptNewRootOrReplaceCurrent()
    {
        var session = new EsDeImportSession(Fixture("Unchanged", null));
        var original = session.Import();
        Assert.ThrowsExactly<DirectoryNotFoundException>(() =>
            session.RelocateVerified(Path.Combine(Path.GetTempPath(), "NO_NEW_ROOT_" + Guid.NewGuid().ToString("N"))));
        Assert.AreSame(original, session.Current);
        Assert.AreEqual(original.ContentFingerprint, session.Import().ContentFingerprint);
    }

    [TestMethod]
    public void FP_16_LibraryIdentityExcludedFromFingerprintAndEmptyCatalogueSupported()
    {
        var bytes = EsDeFingerprintV2.CanonicalBytes(Array.Empty<EsDeGame>());
        Assert.AreEqual(52, bytes.Length); // independently measured golden empty root
        var sameGame = Game();
        var sameData = EsDeFingerprintV2.Compute(new[] { sameGame });
        var snapshotA = new EsDeCatalogueSnapshot("library-one", "ES_DE_READ_ONLY",
            sameData, new[] { sameGame }, Array.Empty<EsDeDiagnostic>());
        var snapshotB = snapshotA with {
            LibraryId = "library-two",
            Diagnostics = new[] { new EsDeDiagnostic("SIMULATED", "gba", "fixture", "no provenance") }
        };
        Assert.AreEqual(snapshotA.ContentFingerprint, snapshotB.ContentFingerprint);
        Assert.AreNotEqual(snapshotA.LibraryId, snapshotB.LibraryId);
    }

}
