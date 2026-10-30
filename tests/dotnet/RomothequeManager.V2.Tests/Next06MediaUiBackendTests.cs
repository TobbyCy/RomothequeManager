using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using SkiaSharp;

namespace RomothequeManager.V2.Tests;

// NEXT-06: only generated PNG/JPEG + inert ROM placeholders in test-owned temp paths.
[TestClass]
public sealed class Next06MediaUiBackendTests
{
    private string _base = "";
    private string _mediaRoot = "";
    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["gba"] = "gba" };
    private static readonly IReadOnlyDictionary<string, MediaKind> Roles =
        new Dictionary<string, MediaKind>(StringComparer.OrdinalIgnoreCase)
        { ["covers"] = MediaKind.Cover, ["logos"] = MediaKind.Logo };

    [TestInitialize]
    public void Setup()
    {
        _base = Path.Combine(Path.GetTempPath(), "rm-next06-fixture-" + Guid.NewGuid().ToString("N"));
        _mediaRoot = Path.Combine(_base, "MEDIAS");
        Directory.CreateDirectory(_mediaRoot);
        Directory.CreateDirectory(Path.Combine(_base, "ROMS"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Path.GetFileName(_base).StartsWith("rm-next06-fixture-", StringComparison.Ordinal) &&
            Directory.Exists(_base)) Directory.Delete(_base, recursive: true);
    }

    private static string Write(string root, string relative, byte[] content)
    {
        var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
        return file;
    }

    private string Rom(string relative) =>
        Write(_base, "ROMS/" + relative, "SYNTHETIC_INERT_NO_ROM"u8.ToArray());

    private string Media(string relative, byte[]? content = null) =>
        Write(_mediaRoot, relative, content ?? GenerateImage(SKEncodedImageFormat.Png));

    private static byte[] GenerateImage(SKEncodedImageFormat format, int width = 24, int height = 16)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.OrangeRed);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 85);
        return data.ToArray();
    }

    private static byte[] HeaderOnlyPng(uint width, uint height)
    {
        var bytes = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20, 4), height);
        bytes[24] = 8; bytes[25] = 2;
        uint crc = 0xffffffff;
        foreach (var value in bytes.AsSpan(12, 17))
        {
            crc ^= value;
            for (var n = 0; n < 8; n++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(29, 4), ~crc);
        return bytes;
    }

    private InventorySnapshot Roms() => new InventorySession(new InventoryScope(_base, Aliases)).Scan();

    private MediaIndexSnapshot Index() => new MediaIndexApplicationService(
        s => s.MediaRoot == _mediaRoot).ScanConfirmed(
            new MediaRootScope(_mediaRoot, Aliases, Roles), explicitConsent: true);

    private static VerifiedScopeMapping Map(InventorySnapshot roms, MediaIndexSnapshot media) =>
        VerifiedScopeMapping.FromExplicitSelections(roms, media,
            new Dictionary<string, string> { ["gba"] = "gba" }, "EXPLICIT_SYNTHETIC_MAPPING");

    private static MediaPreviewRequest Request(MediaIndexSnapshot media,
        string observationId, int max = 512) =>
        new(media.RunId, media.MediaRootRef, observationId, max);

    [TestMethod]
    public async Task MU_01_RealPngAndJpegDecodeFromMediaWithoutEsDeXml()
    {
        Rom("gba/Fiction.gba");
        Media("gba/covers/Fiction.png");
        Media("gba/covers/Fiction.jpg", GenerateImage(SKEncodedImageFormat.Jpeg));
        var snapshot = Index();
        Assert.AreEqual(2, snapshot.Observations.Count);
        Assert.IsTrue(snapshot.Observations.All(x => x.HeaderValidated));
        var preview = new MediaPreviewService(_mediaRoot, snapshot);
        foreach (var observation in snapshot.Observations)
        {
            using var result = await preview.LoadAsync(Request(snapshot, observation.MediaObservationId));
            Assert.AreEqual(MediaPreviewStatus.Ready, result.Status, result.DiagnosticCode);
            Assert.AreEqual(24, result.SourceWidth);
            Assert.AreEqual(16, result.SourceHeight);
            Assert.AreEqual(24 * 16 * 4, result.Image!.Pixels.Length);
            Assert.AreEqual("FILESYSTEM_MEDIA", result.SourceKind);
        }
        Assert.IsFalse(Directory.EnumerateFiles(_base, "*.xml", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    public void MU_02_UnlinkedCandidatesAndExplicitReferencesAreNotApprovedCovers()
    {
        Rom("gba/Provisional.gba");
        Media("gba/covers/Provisional.png");
        Media("gba/logos/Orphan.png");
        var roms = Roms();
        var media = Index();
        var svc = new MediaPresentationService();
        var ui = svc.Build(roms, media, Map(roms, media));
        Assert.AreEqual(MediaUiState.CANDIDATE, ui.Rows.Single().State);
        Assert.AreEqual(MediaUiState.FOUND_UNLINKED, ui.UnlinkedMedias.Single().State);
        Assert.AreEqual(MediaKind.Logo, ui.UnlinkedMedias.Single().Kind);
        var reference = new ExplicitMediaReference(roms.Observations.Single().ObservationId,
            "MEDIAS/gba/covers/Provisional.png", "FIXTURE_EXPLICIT_PATH", "CLAIM_ONLY");
        var claimed = svc.Build(roms, media, Map(roms, media), [reference]);
        Assert.AreEqual(MediaUiState.REFERENCED, claimed.Rows.Single().State);
        Assert.IsTrue(claimed.Rows.Single().SourceProvenance.Any(x => x.Contains("CLAIM_ONLY")));
        Assert.IsFalse(claimed.Rows.Single().Evidence.Any(x => x.Contains("APPROVED")));
        Assert.IsFalse(ui.Rows is MediaUiRow[]);
        Assert.IsTrue(((IList<MediaUiRow>)ui.Rows).IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MediaUiRow>)ui.Rows).Clear());
    }

    [TestMethod]
    public void MU_03_HomonymsAndTwoCoversAreAmbiguousWithAllOptions()
    {
        Rom("gba/A/Fiction.gba"); Rom("gba/B/Fiction.gba");
        Media("gba/covers/Fiction.png");
        Media("gba/covers/Fiction.jpg", GenerateImage(SKEncodedImageFormat.Jpeg));
        var roms = Roms();
        var media = Index();
        var ui = new MediaPresentationService().Build(roms, media, Map(roms, media));
        Assert.AreEqual(2, ui.Rows.Count);
        Assert.IsTrue(ui.Rows.All(x => x.State == MediaUiState.AMBIGUOUS));
        Assert.IsTrue(ui.Rows.All(x => x.CandidateMediaObservationIds.Count == 2));
        Assert.IsTrue(ui.Rows.All(x => x.Conflicts.Count > 0));
    }

    [TestMethod]
    public void MU_04_MissingOnlyWhenCoverageCompleteAndOfflineNotComparable()
    {
        Rom("gba/Missing.gba");
        var roms = Roms();
        var media = Index();
        var mapping = Map(roms, media);
        var svc = new MediaPresentationService();
        Assert.AreEqual(MediaUiState.ABSENT_IN_COMPLETE_SCOPE,
            svc.Build(roms, media, mapping).Rows.Single().State);
        var partial = media with { ScanStatus = InventoryScanStatus.Partial };
        Assert.AreEqual(MediaUiState.NOT_COMPARABLE,
            svc.Build(roms, partial, mapping with { MediaScanId = partial.RunId }).Rows.Single().State);
        var offline = media with { Availability = InventoryAvailability.Offline };
        Assert.AreEqual(MediaUiState.NOT_COMPARABLE,
            svc.Build(roms, offline, mapping).Rows.Single().State);
        Assert.AreEqual(MediaUiState.NOT_COMPARABLE,
            svc.Build(roms, media, mapping with { MediaScanId = "stale-run" }).Rows.Single().State);
    }

    [TestMethod]
    public async Task MU_05_CorruptTruncatedOversizedAndWebpNeverPreviewReady()
    {
        Media("gba/covers/HeaderOnly.png", HeaderOnlyPng(24, 16));
        Media("gba/covers/Oversized.png", HeaderOnlyPng(4096, 4096));
        Media("gba/covers/Unrecognized.webp", "RIFF1234WEBP"u8.ToArray());
        var media = Index();
        var decoder = new MediaPreviewService(_mediaRoot, media);
        var headerOnly = media.Observations.Single(x => x.RelativePath.EndsWith("HeaderOnly.png"));
        Assert.IsTrue(headerOnly.HeaderValidated);
        using (var result = await decoder.LoadAsync(Request(media, headerOnly.MediaObservationId)))
            Assert.AreEqual(MediaPreviewStatus.DecodeFailed, result.Status);
        var huge = media.Observations.Single(x => x.RelativePath.EndsWith("Oversized.png"));
        using (var result = await decoder.LoadAsync(Request(media, huge.MediaObservationId)))
            Assert.AreEqual(MediaPreviewStatus.Rejected, result.Status);
        var webp = media.Observations.Single(x => x.RelativePath.EndsWith(".webp"));
        using (var result = await decoder.LoadAsync(Request(media, webp.MediaObservationId)))
            Assert.AreEqual(MediaPreviewStatus.Unsupported, result.Status);
        using (var result = await decoder.LoadAsync(Request(media, headerOnly.MediaObservationId, 1024)))
            Assert.AreEqual(MediaPreviewStatus.Rejected, result.Status);
    }

    [TestMethod]
    public async Task MU_06_CancelReadStaleGenerationAndPixelDisposal()
    {
        Media("gba/covers/Cancel.png");
        var media = Index();
        var observation = media.Observations.Single();
        using var cancel = new CancellationTokenSource();
        var reader = new MediaPreviewService(_mediaRoot, media,
            readChunkForTest: () => cancel.Cancel());
        using (var stopped = await reader.LoadAsync(Request(media, observation.MediaObservationId),
                   cancel.Token))
            Assert.AreEqual(MediaPreviewStatus.Cancelled, stopped.Status);

        MediaPreviewService? changing = null;
        changing = new MediaPreviewService(_mediaRoot, media,
            decodeStartedForTest: () => changing!.ReplaceSyntheticSnapshot(
                media with { RunId = "NEW_SYNTHETIC_GENERATION" }));
        using (var stale = await changing.LoadAsync(Request(media, observation.MediaObservationId)))
            Assert.AreEqual(MediaPreviewStatus.Stale, stale.Status);

        var stable = new MediaPreviewService(_mediaRoot, media);
        var valid = await stable.LoadAsync(Request(media, observation.MediaObservationId));
        Assert.AreEqual(MediaPreviewStatus.Ready, valid.Status);
        var owner = valid.Image!;
        Assert.IsTrue(owner.Pixels.Length > 0);
        valid.Dispose();
        Assert.AreEqual(0, owner.Pixels.Length);
    }

    [TestMethod]
    public async Task MU_07_PublicAlwaysFailsClosedAndForgedPathsAreRejected()
    {
        var source = Media("gba/covers/Provenance.png");
        var before = SHA256.HashData(File.ReadAllBytes(source));
        var media = Index();
        var id = media.Observations.Single().MediaObservationId;
        using (var denied = await new MediaPreviewService().LoadAsync(Request(media, id)))
            Assert.AreEqual("REAL_MEDIA_PREVIEW_HOLD", denied.DiagnosticCode);
        var fixture = new MediaPreviewService(_mediaRoot, media);
        using (var missing = await fixture.LoadAsync(Request(media, "unknown-id")))
            Assert.AreEqual(MediaPreviewStatus.Rejected, missing.Status);
        using (var stale = await fixture.LoadAsync(new MediaPreviewRequest(
            "wrong-run", media.MediaRootRef, id, 100)))
            Assert.AreEqual(MediaPreviewStatus.Stale, stale.Status);
        var forged = media with
        {
            Observations = Array.AsReadOnly(new[]
            {
                media.Observations.Single() with { RelativePath = "MEDIAS/../OUTSIDE.png" }
            })
        };
        using (var refused = await new MediaPreviewService(_mediaRoot, forged)
                   .LoadAsync(Request(forged, id)))
            Assert.AreEqual(MediaPreviewStatus.Rejected, refused.Status);
        Assert.ThrowsExactly<ArgumentException>(() => new MediaPreviewService(
            Path.Combine(_base, "ROMS"), media));
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(source)));

        var outside = Write(_base, "OUTSIDE/Generated.png", GenerateImage(SKEncodedImageFormat.Png));
        var linked = false;
        try
        {
            File.Delete(source);
            File.CreateSymbolicLink(source, outside);
            linked = true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or
            PlatformNotSupportedException)
        {
            Console.WriteLine("MU07_SYMLINK_SUBTEST_NOT_EXECUTED_HOST_DENIED");
        }
        if (linked)
        {
            using var linkedResult = await fixture.LoadAsync(Request(media, id));
            Assert.AreEqual(MediaPreviewStatus.Rejected, linkedResult.Status);
        }
    }

    [TestMethod]
    public async Task MU_08_ExplicitMemoryArithmeticAndConcurrentDecodeHaveBoundedOutputs()
    {
        var source = Media("gba/covers/Measurable.png",
            GenerateImage(SKEncodedImageFormat.Png, 1024, 1024));
        var media = Index();
        var id = media.Observations.Single().MediaObservationId;
        var original = SHA256.HashData(File.ReadAllBytes(source));
        var svc = new MediaPreviewService(_mediaRoot, media);
        var timer = Stopwatch.StartNew();
        var requests = Enumerable.Range(0, 3).Select(_ =>
            svc.LoadAsync(Request(media, id, 512))).ToArray();
        var results = await Task.WhenAll(requests);
        timer.Stop();
        try
        {
            foreach (var result in results)
            {
                Assert.AreEqual(MediaPreviewStatus.Ready, result.Status, result.DiagnosticCode);
                Assert.IsTrue(result.Image!.Width <= 512 && result.Image.Height <= 512);
                Assert.AreEqual(result.Image.Width * result.Image.Height * 4,
                    result.Image.Pixels.Length);
                Assert.IsTrue(result.BufferBytesUpperBound <= 32 * 1024 * 1024);
                Assert.IsNotNull(result.PrivateBytesDelta);
            }
            Console.WriteLine("MU08_SYNTHETIC elapsed_ms=" + timer.ElapsedMilliseconds +
                " private_delta_bytes=[" + string.Join(",",
                    results.Select(r => r.PrivateBytesDelta)) + "]" +
                " explicit_buffer_bound_bytes=" + results.Max(r => r.BufferBytesUpperBound));
        }
        finally { foreach (var result in results) result.Dispose(); }
        CollectionAssert.AreEqual(original, SHA256.HashData(File.ReadAllBytes(source)));
    }
}
