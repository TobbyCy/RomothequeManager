using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.MvvmFoundation;
using RomothequeManager.V2.Persistence;

namespace RomothequeManager.V2.Tests;

/// <summary>UI05 synthetic-only tests. No actual ROM, XML, SSD, bitmap, or source write.</summary>
[TestClass]
public sealed class Next05SqliteUiTests
{
    private string _fixture = "";
    private string _db = "";
    private readonly string _libraryId = Guid.NewGuid().ToString("D");
    private readonly string _romRootId = Guid.NewGuid().ToString("D");
    private readonly string _mediaRootId = Guid.NewGuid().ToString("D");

    [TestInitialize]
    public void Prepare()
    {
        _fixture = Path.Combine(Path.GetTempPath(),
            "rm-persist01-fixture-" + Guid.NewGuid().ToString("N"));
        _db = Path.Combine(_fixture, "ROMOTHEQUE_MANAGER", "data", "catalog.sqlite");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Path.GetFileName(_fixture).StartsWith("rm-persist01-fixture-",
            StringComparison.Ordinal) && Directory.Exists(_fixture))
            Directory.Delete(_fixture, recursive: true);
    }

    private SqliteCatalogStore OpenStore() => new(_db);

    private void Seed(ICatalogStore store)
    {
        store.AddLibrary(new CatalogLibrary(_libraryId, "IDEAL · bibliothèque préparée fictive"));
        store.AddStorageRoot(new CatalogStorageRoot(_romRootId, _libraryId, "ROMS", "ROMS"));
        store.AddStorageRoot(new CatalogStorageRoot(_mediaRootId, _libraryId, "MEDIAS", "MEDIAS"));
        store.SaveAttempt(_romRootId, Rom());
        store.SaveAttempt(_mediaRootId, Media());
    }

    private static InventorySnapshot Rom(string observationId = "scan-temporary-1",
        InventoryScanStatus status = InventoryScanStatus.Complete)
    {
        var stamp = DateTimeOffset.UtcNow;
        var entry = new FileObservation(observationId, "loc-ephemeral", "gba",
            "ROMS/gba/Fiction.gba", InventoryKind.PotentialRom, ".gba", 12,
            stamp, stamp, "FILESYSTEM_OBSERVATION", InventoryAvailability.Online,
            Array.Empty<string>());
        return new InventorySnapshot("next-02-inventory/0.1-candidate", "fixture-ideal",
            "run-ephemeral", stamp, status, InventoryAvailability.Online,
            InventoryFreshness.Fresh, new[] { new InventoryCoverage("ROMS", status) },
            new[] { entry }, Array.Empty<InventoryDiagnostic>(),
            Array.Empty<InventoryPlaylist>(), Array.Empty<PotentialDuplicateGroup>(),
            "fictional-cover", 1, status == InventoryScanStatus.Complete ? 1 : null, null);
    }

    private static MediaIndexSnapshot Media()
    {
        var stamp = DateTimeOffset.UtcNow;
        var rows = new[]
        {
            new MediaObservation("ephemeral-media-1", "temporary-media-root",
                "gba/covers/Fiction.png", "gba", MediaKind.Cover, ".png",
                MediaImageFormat.Png, true, 1000, 20, 15, stamp, stamp,
                InventoryAvailability.Online, "FILESYSTEM_MEDIA", Array.Empty<string>()),
            new MediaObservation("ephemeral-media-2", "temporary-media-root",
                "gba/covers/Alternative.jpg", "gba", MediaKind.Cover, ".jpg",
                MediaImageFormat.Jpeg, true, 1000, 20, 15, stamp, stamp,
                InventoryAvailability.Online, "FILESYSTEM_MEDIA", Array.Empty<string>())
        };
        return new MediaIndexSnapshot("run-ephemeral-media", "temporary-media-root",
            stamp, InventoryScanStatus.Complete, InventoryAvailability.Online,
            InventoryFreshness.Fresh,
            new[] { new InventoryCoverage("MEDIAS", InventoryScanStatus.Complete) },
            rows, Array.Empty<InventoryDiagnostic>(), rows.Length, rows.Length);
    }

    [TestMethod]
    public void UI05_01_HistoricalRestorationUsesExistingCollectionViewAndNeverOpensSources()
    {
        using (var seed = OpenStore()) Seed(seed);
        using var inventory = new InventoryViewModel(new InventoryApplicationService(),
            action => action());
        using var vm = new ObservedLibraryViewModel(inventory, fixtureMode: true);
        vm.AttachRestoredForCi(new RestoredCatalogSession(OpenStore(),
            _romRootId, _mediaRootId, fixtureWriteAllowed: true));
        Assert.IsTrue(vm.HasRestoredSession);
        Assert.IsFalse(vm.CanInventoryFixture);
        Assert.IsTrue(vm.SnapshotStatus.Contains("OFFLINE / STALE", StringComparison.Ordinal));
        Assert.AreEqual(1, vm.MatchingFileCount);
        Assert.IsTrue(vm.Summary.Contains("jeux identifiés : inconnu", StringComparison.Ordinal));
        vm.ShowCollectionCommand.Execute(null);
        Assert.AreEqual(1, vm.Rows.Count);
        vm.SelectedRow = vm.Rows[0];
        vm.OpenFileCommand.Execute(null);
        Assert.IsTrue(vm.IsFileDetail);
        Assert.AreEqual(2, vm.RestoredSession!.Candidates.Count);
        Assert.IsNull(vm.RestoredSession.SelectedCandidate);
        Assert.AreEqual("Aucune jaquette associée manuellement.",
            vm.RestoredSession.CurrentChoiceLabel);
        Assert.IsFalse(vm.RestoredSession.SaveCommand.CanExecute(null));
        // Selecting a candidate never triggers an automatic decision.
        vm.RestoredSession.SelectedCandidate = vm.RestoredSession.Candidates[0];
        Assert.IsFalse(vm.RestoredSession.SaveCommand.CanExecute(null));
        using var check = OpenStore();
        Assert.AreEqual(0, check.LoadLastKnown(_romRootId).Decisions.Count);
    }

    [TestMethod]
    public void UI05_02_ExplicitCoverDecisionSurvivesIndependentStoreReopenAndTransientIds()
    {
        using (var seed = OpenStore()) Seed(seed);
        string originalChoice;
        using (var first = new RestoredCatalogSession(OpenStore(), _romRootId,
            _mediaRootId, fixtureWriteAllowed: true))
        {
            first.SelectRom(first.DisplayedInventory!.Observations.Single());
            first.SelectedCandidate = first.Candidates.Single(x => x.Format == "Png");
            Assert.IsFalse(first.SaveCommand.CanExecute(null));
            first.Confirmation = true;
            Assert.IsTrue(first.SaveCommand.CanExecute(null));
            first.SaveCommand.Execute(null);
            StringAssert.Contains(first.SaveStatus, "enregistrée");
            Assert.IsFalse(first.Confirmation);
            originalChoice = first.CurrentChoiceLabel;
            StringAssert.Contains(originalChoice, "Fiction.png");
        }
        using (var second = new RestoredCatalogSession(OpenStore(), _romRootId,
            _mediaRootId, fixtureWriteAllowed: true))
        {
            // New run-scoped observation ID, same explicitly retained root/path.
            second.SelectRom(Rom(observationId: "different-run-scoped-id")
                .Observations.Single());
            Assert.AreEqual(originalChoice, second.CurrentChoiceLabel);
            Assert.AreEqual(2, second.Candidates.Count);
            second.SelectedCandidate = second.Candidates.Single(x => x.Format == "Jpeg");
            second.Confirmation = true;
            second.SaveCommand.Execute(null);
            StringAssert.Contains(second.SaveStatus, "enregistrée");
            StringAssert.Contains(second.CurrentChoiceLabel, "Alternative.jpg");
            second.Confirmation = true;
            second.RevokeCommand.Execute(null);
            Assert.AreEqual("Aucune jaquette associée manuellement.",
                second.CurrentChoiceLabel);
        }
        using var reopened = OpenStore();
        var decisions = reopened.LoadLastKnown(_romRootId).Decisions;
        Assert.AreEqual(2, decisions.Count);
        Assert.AreEqual(decisions[0].DecisionId, decisions[1].SupersedesId);
        Assert.IsNotNull(decisions[1].RevokedAtUtc);
        Assert.IsNull(decisions[0].RevokedAtUtc); // superseded != deletion
        using var final = new RestoredCatalogSession(OpenStore(), _romRootId, _mediaRootId);
        final.SelectRom(final.DisplayedInventory!.Observations.Single());
        Assert.AreEqual("Aucune jaquette associée manuellement.", final.CurrentChoiceLabel);
        Assert.IsFalse(final.FixtureWriteAllowed);
        Assert.IsFalse(final.CanSave);
    }

    [TestMethod]
    public void UI05_03_DegradedAttemptDoesNotReplaceRestoredCompleteOrDecisions()
    {
        using (var seed = OpenStore())
        {
            Seed(seed);
            seed.SaveAttempt(_romRootId, Rom("run-partial", InventoryScanStatus.Partial));
        }
        using var restored = new RestoredCatalogSession(OpenStore(), _romRootId,
            _mediaRootId, fixtureWriteAllowed: true);
        Assert.AreEqual(InventoryScanStatus.Complete, restored.DisplayedInventory!.ScanStatus);
        Assert.IsTrue(restored.StatusLabel.Contains("OFFLINE / STALE", StringComparison.Ordinal));
        restored.ShowAttempt();
        Assert.IsTrue(restored.ShowingAttempt);
        Assert.AreEqual(InventoryScanStatus.Partial, restored.DisplayedInventory!.ScanStatus);
        restored.SelectRom(restored.DisplayedInventory.Observations.Single());
        restored.SelectedCandidate = restored.Candidates.First();
        restored.Confirmation = true;
        Assert.IsFalse(restored.SaveCommand.CanExecute(null));
        restored.ShowComplete();
        Assert.AreEqual(InventoryScanStatus.Complete, restored.DisplayedInventory!.ScanStatus);
    }

    [TestMethod]
    public void UI05_04_ProductionWriteDeniedAndWrongLibraryRootsRefused()
    {
        using (var seed = OpenStore()) Seed(seed);
        using var readonlySession = new RestoredCatalogSession(OpenStore(),
            _romRootId, _mediaRootId);
        readonlySession.SelectRom(readonlySession.DisplayedInventory!.Observations.Single());
        readonlySession.SelectedCandidate = readonlySession.Candidates.First();
        readonlySession.Confirmation = true;
        Assert.IsFalse(readonlySession.SaveCommand.CanExecute(null));
        Assert.IsFalse(readonlySession.RevokeCommand.CanExecute(null));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            new RestoredCatalogSession(OpenStore(), _mediaRootId, _romRootId));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            new SqliteCatalogStore());
    }
}
