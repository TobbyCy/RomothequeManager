using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;
using SkiaSharp;

namespace RomothequeManager.V2.Tests;

/// <summary>Real native MEDIAS permission path, only on fictional temporary files.</summary>
[TestClass]
public sealed class IdealMediaNativeTests
{
    private readonly List<string> _fixtures = new();

    private string NewRoot()
    {
        var temp = Environment.GetEnvironmentVariable("RUNNER_TEMP") ?? Path.GetTempPath();
        var root = Path.Combine(temp, "rm-native-medias-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "MEDIAS", "gba"));
        _fixtures.Add(root);
        return Path.Combine(root, "MEDIAS");
    }

    private static void Png(string path, int side, SKColor color)
    {
        using var bitmap = new SKBitmap(side, side);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
        File.WriteAllBytes(path, encoded.ToArray());
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in _fixtures)
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    [TestMethod]
    public async Task MEDIA_03_IndependentNativeIndexAndPreview_NoImplicitGrant()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        Png(Path.Combine(root, "gba", "fiction.png"), 3, SKColors.Red);
        var id = Guid.NewGuid().ToString("D");
        using var service = new IdealMediaApplicationService();
        Assert.IsTrue(service.PrepareMediaSelection(new MediaRootScope(root), id));
        Assert.AreEqual(InventoryScanStatus.Failed,
            (await service.StartIndexAsync()).ScanStatus);
        Assert.IsFalse(service.AuthorizeMediaIndex(false));
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        var scan = await service.StartIndexAsync();
        Assert.AreEqual(InventoryScanStatus.Complete, scan.ScanStatus);
        Assert.AreEqual(1, scan.Observations.Count);
        var item = scan.Observations.Single();
        Assert.AreEqual("IDEAL_NATIVE_MEDIA", item.SourceKind);
        Assert.IsTrue(item.HeaderValidated);
        var request = new MediaPreviewRequest(scan.RunId, id, item.MediaObservationId, 64);
        Assert.AreEqual(MediaPreviewStatus.Rejected, (await service.LoadPreviewAsync(request)).Status);
        Assert.IsFalse(service.AuthorizeMediaPreview(request, false));
        Assert.IsTrue(service.AuthorizeMediaPreview(request, true));
        using var result = await service.LoadPreviewAsync(request);
        Assert.AreEqual(MediaPreviewStatus.Ready, result.Status);
        Assert.IsNotNull(result.Image);
        Assert.IsTrue(result.Image.Pixels.Length is > 0 and <= (64 * 64 * 4));
        Assert.AreEqual(MediaPreviewStatus.Rejected, (await service.LoadPreviewAsync(request)).Status);
        Assert.AreEqual(InventoryScanStatus.Failed, (await service.StartIndexAsync()).ScanStatus);
    }

    [TestMethod]
    public async Task MEDIA_04_NativePreviewRejectsChangedHeaderWithoutScanningOtherRoots()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        var path = Path.Combine(root, "gba", "fiction.png");
        Png(path, 3, SKColors.Red);
        var id = Guid.NewGuid().ToString("D");
        using var service = new IdealMediaApplicationService();
        Assert.IsTrue(service.PrepareMediaSelection(new MediaRootScope(root), id));
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        var scan = await service.StartIndexAsync();
        Assert.AreEqual(InventoryScanStatus.Complete, scan.ScanStatus);
        var req = new MediaPreviewRequest(scan.RunId, id,
            scan.Observations.Single().MediaObservationId, 64);
        Assert.IsTrue(service.AuthorizeMediaPreview(req, true));
        Png(path, 5, SKColors.Blue);
        using var result = await service.LoadPreviewAsync(req);
        Assert.AreEqual(MediaPreviewStatus.Stale, result.Status);
        Assert.IsNull(result.Image);
    }

    [TestMethod]
    public async Task MEDIA_05_SizeRefusals_CancelAndAtoBKeepNoGrant()
    {
        if (!OperatingSystem.IsWindows()) return;
        var a = NewRoot();
        var b = NewRoot();
        File.WriteAllBytes(Path.Combine(a, "gba", "oversized.png"), new byte[1024]);
        Png(Path.Combine(b, "gba", "fiction.png"), 2, SKColors.Green);
        using var service = new IdealMediaApplicationService();
        var aid = Guid.NewGuid().ToString("D");
        var bid = Guid.NewGuid().ToString("D");
        Assert.IsTrue(service.PrepareMediaSelection(new MediaRootScope(a,
            MaxImageBytes: 64, MaxHeaderBytes: 64), aid));
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        var index = await service.StartIndexAsync();
        Assert.AreEqual(InventoryScanStatus.Complete, index.ScanStatus);
        Assert.IsFalse(index.Observations.Single().HeaderValidated);
        Assert.IsFalse(service.AuthorizeMediaPreview(new MediaPreviewRequest(index.RunId,
            aid, index.Observations.Single().MediaObservationId, 128), true));
        Assert.IsTrue(service.PrepareMediaSelection(new MediaRootScope(b), bid));
        Assert.IsNull(service.Current);
        Assert.AreEqual(InventoryScanStatus.Failed, (await service.StartIndexAsync()).ScanStatus);
        using var token = new CancellationTokenSource();
        token.Cancel();
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        Assert.AreNotEqual(InventoryScanStatus.Complete,
            (await service.StartIndexAsync(token.Token)).ScanStatus);
        Assert.IsNull(service.Current);
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        var newScan = await service.StartIndexAsync();
        Assert.AreEqual(InventoryScanStatus.Complete, newScan.ScanStatus);
        Assert.IsTrue(newScan.Observations.Single().RelativePath.EndsWith("fiction.png"));
    }

    [TestMethod]
    public async Task MEDIA_06_DisposeAndCancelRevokeIndexAndPreviewBeforeAnyImplicitRead()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        Png(Path.Combine(root, "gba", "fiction.png"), 2, SKColors.Blue);
        var id = Guid.NewGuid().ToString("D");
        using var service = new IdealMediaApplicationService();
        Assert.IsFalse(service.AuthorizeMediaIndex(true),
            "An affirmative flag without a selected MEDIAS root is not a grant.");
        Assert.IsTrue(service.PrepareMediaSelection(new MediaRootScope(root), id));
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        Assert.IsFalse(service.RequestCancellation(),
            "Cancelling an issued but not started grant does not invent an active run.");
        Assert.AreEqual(InventoryScanStatus.Failed,
            (await service.StartIndexAsync()).ScanStatus,
            "A revoked INDEX grant cannot be replayed.");
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        var index = await service.StartIndexAsync();
        Assert.AreEqual(InventoryScanStatus.Complete, index.ScanStatus);
        var request = new MediaPreviewRequest(index.RunId, id,
            index.Observations.Single().MediaObservationId, 128);
        Assert.IsTrue(service.AuthorizeMediaPreview(request, true));
        service.Dispose();
        Assert.IsNull(service.Current);
        Assert.IsFalse(service.AuthorizeMediaIndex(true));
        Assert.IsFalse(service.AuthorizeMediaPreview(request, true));
        Assert.AreEqual(InventoryScanStatus.Failed,
            (await service.StartIndexAsync()).ScanStatus);
        using var denied = await service.LoadPreviewAsync(request);
        Assert.AreEqual(MediaPreviewStatus.Rejected, denied.Status);
        Assert.IsNull(denied.Image);
    }

    [TestMethod]
    public async Task MEDIA_07_UntrustedRootAndCancelledPreviewDoNotExposePixels()
    {
        if (!OperatingSystem.IsWindows()) return;
        var root = NewRoot();
        var id = Guid.NewGuid().ToString("D");
        using var service = new IdealMediaApplicationService();
        Assert.IsFalse(service.PrepareMediaSelection(new MediaRootScope(
            @"\\server\share\MEDIAS"), id));
        Assert.IsFalse(service.PrepareMediaSelection(new MediaRootScope(
            @"\\?\C:\untrusted\MEDIAS"), id));
        Assert.IsFalse(service.PrepareMediaSelection(new MediaRootScope(
            root + @"\..\OTHER\MEDIAS"), id));
        Assert.AreEqual(InventoryScanStatus.Failed,
            (await service.StartIndexAsync()).ScanStatus);
        Png(Path.Combine(root, "gba", "fiction.png"), 2, SKColors.Green);
        Assert.IsTrue(service.PrepareMediaSelection(new MediaRootScope(root), id));
        Assert.IsTrue(service.AuthorizeMediaIndex(true));
        var index = await service.StartIndexAsync();
        Assert.AreEqual(InventoryScanStatus.Complete, index.ScanStatus);
        var request = new MediaPreviewRequest(index.RunId, id,
            index.Observations.Single().MediaObservationId, 64);
        Assert.IsTrue(service.AuthorizeMediaPreview(request, true));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        using var rejected = await service.LoadPreviewAsync(request, cancelled.Token);
        Assert.AreNotEqual(MediaPreviewStatus.Ready, rejected.Status);
        Assert.IsNull(rejected.Image);
        Assert.AreEqual(MediaPreviewStatus.Rejected,
            (await service.LoadPreviewAsync(request)).Status,
            "The cancelled PREVIEW capability cannot be replayed.");
    }

}
