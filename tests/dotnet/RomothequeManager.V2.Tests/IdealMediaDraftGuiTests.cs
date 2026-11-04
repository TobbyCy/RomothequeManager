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
