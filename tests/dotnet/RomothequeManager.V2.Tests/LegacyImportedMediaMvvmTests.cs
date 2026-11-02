using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

/// <summary>LOT B-01..B-07: only generated disposable ES-DE XML and fake PNG media;
/// no user library, external device, network, scraper or real preview permission.</summary>
[TestClass]
public sealed class LegacyImportedMediaMvvmTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-lotb145-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "gba"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "metadata"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "covers"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private (ImportedCatalogueUiState State, EsDeGame Game) ImportFixture(
        string image = "../covers/primary.png",
        string thumbnail = "../covers/backup.png")
    {
        File.WriteAllText(Path.Combine(_root, "ROMS", "gba", "Alpha.gba"),
            "NONEXECUTABLE SYNTHETIC ROM PLACEHOLDER", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"),
            "<gameList><game><path>./Alpha.gba</path><name>Fixture Alpha</name>" +
            "<image>" + image + "</image><thumbnail>" + thumbnail +
            "</thumbnail></game></gameList>", new UTF8Encoding(false));
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        return (state, state.Snapshot!.Games.Single());
    }

    [TestMethod]
    public void B_01_OnlyAcceptedSessionGameAndMediaCanQueue_NoImplicitPreview()
    {
        var (state, game) = ImportFixture();
        using var vm = new ImportedCatalogueViewModel(state);
        var fake = new DeferredLoader();
        using var media = new LegacyImportedMediaViewModel(state, vm, fake);
        Assert.IsTrue(media.TryBegin(game, [game.Media[0]], LegacyImageSlot.Card, out var work));
        Assert.IsNotNull(work);
        Assert.AreSame(state.Session, work.Request.Session);
        Assert.AreSame(state.Snapshot, work.Request.Snapshot);
        Assert.IsTrue(work.Request.ExplicitConsent, "B01_ACCEPTED_EXPLICIT_IMPORT_FIXTURE_ONLY");
        Assert.AreEqual(1, media.Pending);

        var forged = new EsDeMedia(game.Media[0].Kind, game.Media[0].RelativePath, true);
        Assert.IsFalse(media.TryBegin(game, [forged], LegacyImageSlot.Card, out _),
            "B01_EQUAL_VALUES_ARE_NOT_AN_ACCEPTED_GAME_REFERENCE");
        var detachedGame = game with { Title = "Unknown fixture" };
        Assert.IsFalse(media.TryBegin(detachedGame, [game.Media[0]],
            LegacyImageSlot.Card, out _), "B01_GAME_MUST_BELONG_TO_SNAPSHOT");
        media.CancelOutstanding();
        Assert.AreEqual(0, media.Pending);
        media.Complete(work);
    }

    [TestMethod]
    public async Task B_04_CancelAndDisposeRejectDelayedResult_WithoutCallbacks()
    {
        var (state, game) = ImportFixture();
        using var importVm = new ImportedCatalogueViewModel(state);
        var fake = new DeferredLoader();
        var vm = new LegacyImportedMediaViewModel(state, importVm, fake);
        var notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        Assert.IsTrue(vm.TryBegin(game, [game.Media[0]], LegacyImageSlot.Card, out var work));
        var pending = vm.LoadAsync(work!);
        vm.CancelOutstanding();
        fake.Complete(LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Ready, "FAKE_LATE"));
        using var received = await pending;
        Assert.AreEqual(LegacyMediaPhase.Stale, received.Phase);
        Assert.IsFalse(vm.TryTransfer(work!, received, out var bitmap, out _));
        Assert.IsNull(bitmap);
        vm.Complete(work!);
        Assert.AreEqual(0L, vm.RetainedBytes);
        Assert.AreEqual(0, vm.Pending);
        vm.Dispose();
        var before = notifications;
        vm.Complete(work!);
        Assert.AreEqual(before, notifications, "B04_NO_POST_DISPOSE_CALLBACK");
    }

    [TestMethod]
    public async Task B_01_LoaderRefusesForgedAndUnconsentedMediaWithoutOpening()
    {
        var (state, game) = ImportFixture();
        using var loader = new LegacyImportedMediaLoader();
        var session = state.Session!;
        var snapshot = state.Snapshot!;
        var forged = new EsDeMedia("image", game.Media[0].RelativePath, true);
        using var denied = await loader.LoadAsync(
            new LegacyMediaRequest(session, snapshot, game, [forged],
                LegacyImageSlot.Card, ExplicitConsent: true), CancellationToken.None);
        Assert.AreEqual(LegacyMediaPhase.Refused, denied.Phase);
        using var notConsented = await loader.LoadAsync(
            new LegacyMediaRequest(session, snapshot, game, [game.Media[0]],
                LegacyImageSlot.Card, ExplicitConsent: false), CancellationToken.None);
        Assert.AreEqual(LegacyMediaPhase.Refused, notConsented.Phase);
        Assert.AreEqual(0, loader.NativeDecoderEntered);
    }

    [TestMethod]
    public async Task B_02_BudgetsRejectOversizedAndForgedPngBeforeNativeDecode()
    {
        var cover = Path.Combine(_root, "MEDIAS", "gba", "covers", "primary.png");
        File.WriteAllBytes(cover, new byte[LegacyImportedImageBudget.MaxCompressedBytes + 1]);
        var (state, game) = ImportFixture();
        using var loader = new LegacyImportedMediaLoader();
        using (var result = await loader.LoadAsync(
            new LegacyMediaRequest(state.Session!, state.Snapshot!, game,
                [game.Media[0]], LegacyImageSlot.Card, true), CancellationToken.None))
        {
            Assert.AreEqual(LegacyMediaPhase.Refused, result.Phase);
            Assert.AreEqual("MEDIA_TOO_LARGE_COMPRESSED", result.Reason);
            Assert.AreEqual(0, loader.NativeDecoderEntered);
        }

        // One deliberately fake IHDR CRC must not enter a native bitmap decoder.
        var bad = LegacyImportedImageBudgetTests.FakePng(64, 64);
        bad[29] ^= 0x40;
        File.WriteAllBytes(cover, bad);
        using var rejected = await loader.LoadAsync(
            new LegacyMediaRequest(state.Session!, state.Snapshot!, game,
                [game.Media[0]], LegacyImageSlot.Card, true), CancellationToken.None);
        Assert.AreEqual(LegacyMediaPhase.Refused, rejected.Phase);
        Assert.AreEqual(0, loader.NativeDecoderEntered);
    }

    [TestMethod]
    public async Task B_02_IndependentAxisAndAreaLimitsRefuseBeforeDecode()
    {
        var cover = Path.Combine(_root, "MEDIAS", "gba", "covers", "primary.png");
        File.WriteAllBytes(cover, LegacyImportedImageBudgetTests.FakePng(2049, 512));
        var (state, game) = ImportFixture();
        using var loader = new LegacyImportedMediaLoader();
        using (var denied = await loader.LoadAsync(
            new LegacyMediaRequest(state.Session!, state.Snapshot!, game,
                [game.Media[0]], LegacyImageSlot.Card, true), CancellationToken.None))
        {
            Assert.AreEqual("MEDIA_DIMENSIONS_EXCEEDED", denied.Reason);
            Assert.AreEqual(0, loader.NativeDecoderEntered);
        }
        File.WriteAllBytes(cover, LegacyImportedImageBudgetTests.FakePng(2048, 2048));
        using var area = await loader.LoadAsync(
            new LegacyMediaRequest(state.Session!, state.Snapshot!, game,
                [game.Media[0]], LegacyImageSlot.Card, true), CancellationToken.None);
        Assert.AreEqual("MEDIA_PIXELS_EXCEEDED", area.Reason);
        Assert.AreEqual(0, loader.NativeDecoderEntered);
    }

    [TestMethod]
    public async Task B_03_FallbackOnlyVisitsBothDeclaredAndIndependentlyAuthorized()
    {
        // The primary is a declared member but corrupt. The thumbnail is absent,
        // so no undeclared filesystem discovery, and no native decoder is entered.
        var cover = Path.Combine(_root, "MEDIAS", "gba", "covers", "primary.png");
        File.WriteAllBytes(cover, new byte[] { 1, 2, 3, 4, 5 });
        var (state, game) = ImportFixture();
        var candidates = ImportedCatalogueUiState.CoverCandidates(game);
        Assert.AreEqual(2, candidates.Count);
        using var loader = new LegacyImportedMediaLoader();
        using var result = await loader.LoadAsync(
            new LegacyMediaRequest(state.Session!, state.Snapshot!, game,
                candidates, LegacyImageSlot.Card, true), CancellationToken.None);
        Assert.AreEqual(LegacyMediaPhase.Refused, result.Phase);
        Assert.AreEqual(0, loader.NativeDecoderEntered,
            "B03_CORRUPT_PRIMARY_AND_MISSING_THUMBNAIL_NO_DECODE");
        Assert.IsNull(result.TakeBitmap());
        Assert.AreEqual(0L, result.RetainedBytes);
    }

    [TestMethod]
    public async Task B_04_QueueCapAndGenerationResetRefuseStaleWork()
    {
        var (state, game) = ImportFixture();
        using var importVm = new ImportedCatalogueViewModel(state);
        var fake = new DeferredLoader();
        using var vm = new LegacyImportedMediaViewModel(state, importVm, fake);
        var jobs = new List<LegacyMediaWork>();
        for (var i = 0; i < 50; ++i)
        {
            Assert.IsTrue(vm.TryBegin(game, [game.Media[0]], LegacyImageSlot.Card, out var job));
            jobs.Add(job!);
        }
        Assert.AreEqual(50, vm.Pending);
        Assert.IsFalse(vm.TryBegin(game, [game.Media[0]], LegacyImageSlot.Card, out _),
            "B04_QUEUE_MUST_REFUSE_51ST");
        vm.CancelOutstanding();
        Assert.AreEqual(0, vm.Pending);
        using var late = await vm.LoadAsync(jobs[0]);
        Assert.AreEqual(LegacyMediaPhase.Stale, late.Phase);
        foreach (var job in jobs) vm.Complete(job);
        Assert.AreEqual(0, vm.Pending);
        Assert.AreEqual(0L, vm.RetainedBytes);
        Assert.AreEqual(0, fake.NativeDecoderEntered);
    }

    [TestMethod]
    public async Task B_02_JpegHeaderBeyond256KiBRefusedBeforeDecoder()
    {
        var path = Path.Combine(_root, "MEDIAS", "gba", "covers", "primary.png");
        // APP segments place the first possible JPEG SOF beyond the strict
        // 256 KiB header scan. Extension is deliberately not trusted.
        using (var bytes = new MemoryStream())
        {
            bytes.Write(new byte[] { 0xFF, 0xD8 });
            for (var i = 0; i < 5; i++)
            {
                bytes.Write(new byte[] { 0xFF, 0xE1, 0xFF, 0xFF });
                bytes.Write(new byte[65533]);
            }
            bytes.Write(new byte[] { 0xFF, 0xC0, 0, 11, 8, 0, 64, 0, 64 });
            File.WriteAllBytes(path, bytes.ToArray());
        }
        var (state, game) = ImportFixture();
        using var loader = new LegacyImportedMediaLoader();
        using var result = await loader.LoadAsync(new LegacyMediaRequest(
            state.Session!, state.Snapshot!, game, [game.Media[0]],
            LegacyImageSlot.Card, true), CancellationToken.None);
        Assert.AreEqual(LegacyMediaPhase.Refused, result.Phase);
        Assert.AreEqual("MEDIA_UNSUPPORTED_OR_UNVERIFIED", result.Reason);
        Assert.AreEqual(0, loader.NativeDecoderEntered,
            "B02_UNBOUNDED_JPEG_METADATA_MUST_NOT_ENTER_DECODER");
    }

    [TestMethod]
    public async Task B_01_AcceptedRefreshRejectsOldSnapshotResult()
    {
        var (state, game) = ImportFixture();
        using var importVm = new ImportedCatalogueViewModel(state);
        var fake = new DeferredLoader();
        using var vm = new LegacyImportedMediaViewModel(state, importVm, fake);
        var old = state.Snapshot;
        Assert.IsTrue(vm.TryBegin(game, [game.Media[0]], LegacyImageSlot.Card, out var job));
        var pending = vm.LoadAsync(job!);

        Assert.IsTrue(state.TryReimport());
        Assert.AreNotSame(old, state.Snapshot, "B01_ACCEPTED_REFRESH_REPLACED_SNAPSHOT");
        Assert.AreSame(state.Snapshot, state.Session!.Current);
        fake.Complete(LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Ready, "LATE"));
        using var stale = await pending;
        Assert.AreEqual(LegacyMediaPhase.Stale, stale.Phase);
        Assert.IsFalse(vm.TryTransfer(job!, stale, out var adopted, out _),
            "B01_OLD_MEDIA_MUST_NOT_BE_ADOPTED_AFTER_B");
        Assert.IsNull(adopted);
        vm.Complete(job!);
        Assert.AreEqual(0L, vm.RetainedBytes);
    }

    private sealed class DeferredLoader : ILegacyImportedMediaLoader
    {
        private readonly TaskCompletionSource<LegacyMediaLoadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int NativeDecoderEntered => 0;
        public int PeakActiveDecodes => 0;

        public Task<LegacyMediaLoadResult> LoadAsync(LegacyMediaRequest request,
            CancellationToken token) => _completion.Task;

        public void Complete(LegacyMediaLoadResult result) => _completion.TrySetResult(result);

        public void Dispose() { }
    }
}
