using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.MvvmFoundation;
using SkiaSharp;

namespace RomothequeManager.V2.Tests;

/// <summary>Windows synthetic-only end-to-end native MEDIAS index and preview
/// with no ES-DE image entry. Never scans or references a personal collection.</summary>
[TestClass]
public sealed class IdealMediaGuiNativeTests
{
    private readonly List<string> _temporaryRoots = new();

    private (string root, string media) NewSyntheticRoot()
    {
        var temp = Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? Path.GetTempPath();
        var root = Path.Combine(temp, "rm-media-gui-" + Guid.NewGuid().ToString("N"));
        var rom = Path.Combine(root, "ROMS", "gba");
        var media = Path.Combine(root, "MEDIAS");
        Directory.CreateDirectory(rom);
        Directory.CreateDirectory(Path.Combine(media, "gba", "covers"));
        File.WriteAllText(Path.Combine(rom, "Fiction.gba"),
            "INERT SYNTHETIC TEST BYTES - NOT A ROM");
        _temporaryRoots.Add(root);
        return (root, media);
    }

    private static void Png(string path, SKColor color)
    {
        using var bitmap = new SKBitmap(16, 16);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(path, encoded.ToArray());
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in _temporaryRoots)
            if (Directory.Exists(root) &&
                Path.GetFileName(root).StartsWith("rm-media-gui-", StringComparison.Ordinal))
                Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task MEDIA_GUI_11_NativeXmlFreeRomAndSameNamedPngShowRealPreviewPixels()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, media) = NewSyntheticRoot();
        Png(Path.Combine(media, "gba", "covers", "Fiction.png"), SKColors.CadetBlue);
        var roms = new InventorySession(new InventoryScope(root,
            IdealPlatformFolderConventions.PlatformAliases)).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, roms.ScanStatus);
        Assert.IsFalse(Directory.EnumerateFiles(root, "*.xml",
            SearchOption.AllDirectories).Any(), "This canary must work without ES-DE XML.");

        using var vm = new IdealMediaDraftViewModel(
            new IdealMediaApplicationService(), productMediaUiEnabled: true);
        Assert.IsTrue(vm.SelectMediaRoot(media));
        Assert.IsNull(vm.Preview);
        Assert.IsFalse(vm.CanLoadPreview);
        Assert.IsTrue(vm.AuthorizeIndex());
        await vm.StartIndexAsync();
        var index = vm.CurrentMediaIndex;
        Assert.IsNotNull(index);
        Assert.AreEqual(InventoryScanStatus.Complete, index.ScanStatus);
        var scope = VerifiedScopeMapping.FromExplicitSelections(roms, index,
            new Dictionary<string, string> { ["gba"] = "gba" },
            "EXPLICIT_SYNTHETIC_ROMS_AND_MEDIAS_PICKS");
        vm.AttachExplicitEsDeEvidence(roms, imported: null, scope);
        var proposals = vm.CoverProposals;
        Assert.AreEqual(1, proposals.Count, "Cover must be discovered without <image>.");
        Assert.AreEqual(MediaUiState.CANDIDATE, proposals[0].State);
        Assert.IsFalse(vm.CanLoadPreview);
        Assert.IsTrue(vm.SelectCoverForPreview(proposals[0]));
        Assert.IsFalse(vm.CanLoadPreview, "Selecting a candidate is not a PREVIEW grant.");
        Assert.IsTrue(vm.AuthorizePreview());
        Assert.IsTrue(vm.CanLoadPreview);
        await vm.LoadPreviewAsync();
        Assert.AreEqual(MediaPreviewStatus.Ready, vm.Preview?.Status);
        var pixels = vm.Preview?.Image?.Pixels.ToArray();
        Assert.IsNotNull(pixels);
        Assert.IsTrue(pixels.Length >= 16 * 16 * 4);
        Assert.IsTrue(pixels.Any(x => x != 0), "Must show actual decoded image pixels.");
        vm.Cancel();
        Assert.IsNull(vm.Preview);
        Assert.IsFalse(vm.CanLoadPreview);
        Assert.AreEqual(0, vm.CoverProposals.Count);
    }

    [TestMethod]
    public async Task MEDIA_GUI_12_AlternateSameNameCoversStayAmbiguousUntilUserSelection()
    {
        if (!OperatingSystem.IsWindows()) return;
        var (root, media) = NewSyntheticRoot();
        Png(Path.Combine(media, "gba", "covers", "Fiction.png"), SKColors.Red);
        Directory.CreateDirectory(Path.Combine(media, "gba", "covers", "alternate"));
        Png(Path.Combine(media, "gba", "covers", "alternate", "Fiction.png"), SKColors.Green);
        var roms = new InventorySession(new InventoryScope(root,
            IdealPlatformFolderConventions.PlatformAliases)).Scan();
        using var vm = new IdealMediaDraftViewModel(
            new IdealMediaApplicationService(), productMediaUiEnabled: true);
        Assert.IsTrue(vm.SelectMediaRoot(media));
        Assert.IsTrue(vm.AuthorizeIndex());
        await vm.StartIndexAsync();
        var index = vm.CurrentMediaIndex;
        Assert.IsNotNull(index);
        vm.AttachExplicitEsDeEvidence(roms, null,
            VerifiedScopeMapping.FromExplicitSelections(roms, index,
                new Dictionary<string, string> { ["gba"] = "gba" },
                "EXPLICIT_SYNTHETIC_MAPPING"));
        Assert.AreEqual(2, vm.CoverProposals.Count);
        Assert.IsTrue(vm.CoverProposals.All(x =>
            x.State == MediaUiState.AMBIGUOUS && x.RequiresExplicitChoice));
        Assert.IsFalse(vm.CanAuthorizePreview);
        Assert.IsTrue(vm.SelectCoverForPreview(vm.CoverProposals[1]));
        Assert.IsTrue(vm.AuthorizePreview());
        await vm.LoadPreviewAsync();
        Assert.AreEqual(MediaPreviewStatus.Ready, vm.Preview?.Status);
    }

    [TestMethod]
    public void MEDIA_GUI_13_StandardDefaultConstructorCannotStartMediaOperations()
    {
        using var vm = new IdealMediaDraftViewModel(new IdealMediaApplicationService());
        Assert.IsFalse(vm.SecurityApproved);
        Assert.IsFalse(vm.CanChooseMedia);
        Assert.IsFalse(vm.AuthorizeIndex());
        Assert.IsFalse(vm.AuthorizePreview());
        Assert.IsNull(vm.Preview);
    }
}
