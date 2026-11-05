using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

/// <summary>Synthetic-only proof for the unexposed MEDIA GUI draft; no filesystem I/O.</summary>
[TestClass]
public sealed class IdealMediaDraftGuiTests
{
    private static FileObservation Rom(string id, string platform, string path) =>
        new(id, "TEST_LOCATION", platform, path, InventoryKind.PotentialRom,
            ".gba", null, null, DateTimeOffset.UtcNow, "SYNTHETIC_ROM",
            InventoryAvailability.Online, Array.Empty<string>());

    private static EsDeGame Game(string platform, string romPath, string title,
        params EsDeMedia[] declared) =>
        new("game-" + title, "variant-" + title, platform, title, romPath,
            true, null, Array.AsReadOnly(declared), Array.Empty<string>());

    private static EsDeCatalogueSnapshot Imported(params EsDeGame[] games) =>
        new("fixture-xml", "SYNTHETIC_ESDE", "fixture-fingerprint",
            Array.AsReadOnly(games), Array.Empty<EsDeDiagnostic>());

    [TestMethod]
    public async Task MEDIA_UI_01_DraftHasNoIndexPreviewOrPickerPermission()
    {
        var backend = new IdealMediaApplicationService();
        using var vm = new IdealMediaDraftViewModel(backend);

        Assert.IsFalse(vm.SecurityApproved);
        Assert.IsFalse(vm.CanChooseMedia);
        Assert.IsFalse(vm.AuthorizeIndexCommand.CanExecute(null));
        Assert.IsFalse(vm.StartIndexCommand.CanExecute(null));
        Assert.IsFalse(vm.AuthorizePreviewCommand.CanExecute(null));
        Assert.IsFalse(vm.LoadPreviewCommand.CanExecute(null));
        Assert.IsFalse(vm.SelectMediaRoot(@"C:\SYNTHETIC\MEDIAS"));
        Assert.IsFalse(vm.AuthorizeIndex());
        Assert.IsFalse(vm.AuthorizePreview());
        await vm.StartIndexAsync();
        await vm.LoadPreviewAsync();
        Assert.IsNull(backend.Current);
        Assert.IsNull(backend.LastAttempt);
        Assert.IsNull(vm.Preview);
        Assert.AreEqual(0, vm.ReferencedCovers.Count);
        Assert.IsTrue(vm.Status.Contains("Draft", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void MEDIA_UI_02_OnlyExactSourceDeclaredRomPathAndCoverSurvive()
    {
        var roms = new[] {
            Rom("obs1", "gba", "ROMS/gba/Fiction.gba"),
            Rom("obs2", "gba", "ROMS/gba/Other.gba")
        };
        var snapshot = Imported(
            Game("gba", "ROMS/gba/Fiction.gba", "Totally different display name",
                new EsDeMedia("image", "MEDIAS/gba/covers/Fiction.png", true),
                new EsDeMedia("video", "MEDIAS/gba/videos/Fiction.mp4", true),
                new EsDeMedia("thumbnail", "MEDIAS/../outside.png", true)),
            Game("gba", "ROMS/gba/Missing.gba", "Fiction",
                new EsDeMedia("image", "MEDIAS/gba/covers/NamedOnly.png", true)));
        var claims = EsDeDeclaredMediaClaims.Build(roms, snapshot);
        Assert.AreEqual(1, claims.Count);
        Assert.AreEqual("obs1", claims[0].RomObservationId);
        Assert.AreEqual("MEDIAS/gba/covers/Fiction.png", claims[0].MediaRelativePath);
        Assert.AreEqual("ESDE_IMPORTED_XML", claims[0].SourceKind);
        Assert.IsFalse(claims.Any(x => x.MediaRelativePath.Contains("outside")));
        Assert.IsFalse(claims.Any(x => x.MediaRelativePath.Contains("NamedOnly")));
    }

    [TestMethod]
    public void MEDIA_UI_03_DuplicateProvenanceAndWrongScopeAreFailClosed()
    {
        var roms = new[] {
            Rom("a", "gba", "ROMS/gba/Same.gba"),
            Rom("b", "gba", "ROMS/gba/Same.gba")
        };
        var xml = Imported(Game("gba", "ROMS/gba/Same.gba", "One",
            new EsDeMedia("image", "MEDIAS/gba/covers/Same.png", true)));
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(roms, xml).Count);

        var single = new[] { Rom("a", "gba", "ROMS/gba/Same.gba") };
        var duplicateXml = Imported(
            Game("gba", "ROMS/gba/Same.gba", "One",
                new EsDeMedia("image", "MEDIAS/gba/covers/Same.png", true)),
            Game("gba", "ROMS/gba/Same.gba", "Two",
                new EsDeMedia("image", "MEDIAS/gba/covers/Same.png", true)));
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(single, duplicateXml).Count);

        var wrongPlatform = Imported(Game("ps2", "ROMS/gba/Same.gba", "Same",
            new EsDeMedia("image", "MEDIAS/gba/covers/Same.png", true)));
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(single, wrongPlatform).Count);
    }

    [TestMethod]
    public void MEDIA_UI_06_RsARelativePathNormalizesToExactLibraryIdentity()
    {
        var roms = new[] { Rom("real-rsa", "gba", "gba/Fiction.gba") };
        var xml = Imported(Game("gba", "ROMS/gba/Fiction.gba",
            "Title deliberately unrelated to file name",
            new EsDeMedia("image",
                "MEDIAS/gba/covers/Artwork-Different-From-Rom.png", true)));
        var claims = EsDeDeclaredMediaClaims.Build(roms, xml);
        Assert.AreEqual(1, claims.Count);
        Assert.AreEqual("real-rsa", claims[0].RomObservationId);
        Assert.AreEqual("MEDIAS/gba/covers/Artwork-Different-From-Rom.png",
            claims[0].MediaRelativePath);
        Assert.AreEqual("ESDE_IMPORTED_XML", claims[0].SourceKind);
        StringAssert.Contains(claims[0].Provenance, "fixture-fingerprint");
    }

    [TestMethod]
    public void MEDIA_UI_07_RsAPlatformPathScopeAndAmbiguityNeverCreateFalseClaims()
    {
        var rsa = new[] { Rom("rsa", "gba", "gba/Fiction.gba") };
        EsDeGame With(string platform, string romPath, string title = "Same XML Title") =>
            Game(platform, romPath, title,
                new EsDeMedia("image", "MEDIAS/gba/covers/OtherArtwork.png", true));

        // Game title must never substitute for exact (platform, canonical path).
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            rsa, Imported(With("ps2", "ROMS/gba/Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            rsa, Imported(With("gba", "ROMS/gba/Other.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            rsa, Imported(With("gba", "ROMS/ps2/Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            rsa, Imported(With("gba", "ROMS/gba/../Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            rsa, Imported(With("gba", "C:/outside/Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            new[] { Rom("wrong-platform-folder", "gba", "ps2/Fiction.gba") },
            Imported(With("gba", "ROMS/gba/Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            new[] { Rom("traversal", "gba", "gba/../Fiction.gba") },
            Imported(With("gba", "ROMS/gba/Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            new[] { Rom("outside", "gba", "MEDIAS/gba/Fiction.gba") },
            Imported(With("gba", "ROMS/gba/Fiction.gba"))).Count);

        // Both representations canonicalize to the same identity; the adapter
        // cannot pick one arbitrarily when two physical observations collide.
        var mixed = new[] {
            Rom("rsa", "gba", "gba/Fiction.gba"),
            Rom("legacy", "gba", "ROMS/gba/Fiction.gba")
        };
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            mixed, Imported(With("gba", "ROMS/gba/Fiction.gba"))).Count);
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(
            rsa, Imported(With("gba", "ROMS/gba/Fiction.gba"),
                With("gba", "ROMS/gba/Fiction.gba", "Another display title"))).Count);
    }

    [TestMethod]
    public void MEDIA_UI_05_UnsafeOrNonMediaEsDePathsCannotBecomeClaims()
    {
        var roms = new[] { Rom("rom", "gba", "ROMS/gba/Safe.gba") };
        var xml = Imported(Game("gba", "ROMS/gba/Safe.gba", "Safe",
            new EsDeMedia("image", "MEDIAS/gba/../secret.png", true),
            new EsDeMedia("image", "MEDIAS//nested.png", true),
            new EsDeMedia("image", "C:/private.png", true),
            new EsDeMedia("image", "/MEDIAS/gba/absolute.png", true),
            new EsDeMedia("image", "MEDIAS/gba/cover.png:stream", true)));
        Assert.AreEqual(0, EsDeDeclaredMediaClaims.Build(roms, xml).Count);
    }

    [TestMethod]
    public void MEDIA_UI_04_CancellationAndDisposalNeverGrantRights()
    {
        var backend = new IdealMediaApplicationService();
        var vm = new IdealMediaDraftViewModel(backend);
        vm.Cancel();
        Assert.IsFalse(vm.AuthorizeIndex());
        Assert.IsFalse(vm.AuthorizePreview());
        Assert.IsNull(backend.Current);
        vm.Dispose();
        vm.Dispose();
        Assert.IsFalse(vm.CanChooseMedia);
        Assert.IsNull(vm.Preview);
    }
}
