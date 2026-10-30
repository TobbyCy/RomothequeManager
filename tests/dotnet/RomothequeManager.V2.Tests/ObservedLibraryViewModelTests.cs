using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class ObservedLibraryViewModelTests
{
    private static InventorySnapshot Snapshot(int count, InventoryScanStatus status =
        InventoryScanStatus.Complete, InventoryAvailability availability = InventoryAvailability.Online,
        InventoryFreshness freshness = InventoryFreshness.Fresh)
    {
        var stamp = DateTimeOffset.UtcNow;
        var rows = Enumerable.Range(0, count).Select(i => new FileObservation(
            "observed-" + i, "session-" + i, i % 2 == 0 ? "gba" : "UNKNOWN",
            "ROMS/" + (i % 2 == 0 ? "gba" : "UNKNOWN") + "/File" + i.ToString("D3") +
                (i % 2 == 0 ? ".gba" : ".zip"),
            i % 2 == 0 ? InventoryKind.PotentialRom : InventoryKind.ArchiveCandidate,
            i % 2 == 0 ? ".gba" : ".zip", i + 10L, stamp, stamp,
            "FILESYSTEM_OBSERVATION", availability, Array.Empty<string>())).ToArray();
        return new InventorySnapshot("test", "session", "scan", stamp, status,
            availability, freshness,
            new[] { new InventoryCoverage("ROMS", status) }, rows,
            Array.Empty<InventoryDiagnostic>(), Array.Empty<InventoryPlaylist>(),
            Array.Empty<PotentialDuplicateGroup>(), "fictional", count,
            status == InventoryScanStatus.Complete ? count : null, null);
    }

    [TestMethod]
    public async Task N05Ux01_MysteryWithoutXmlIsOnlyObservedFilesAndNoIdentities()
    {
        var service = new FakeService(Snapshot(120));
        using var inventory = new InventoryViewModel(service, action => action());
        using var vm = new ObservedLibraryViewModel(inventory, fixtureMode: true);
        Assert.IsFalse(vm.HasSnapshot);
        Assert.IsFalse(vm.CanInventoryFixture);
        Assert.IsTrue(inventory.PrepareSelectedFolder(new InventoryScope(@"C:\test-fixture")).Accepted);
        Assert.IsFalse(vm.CanInventoryFixture);
        inventory.ConsentConfirmed = true;
        Assert.IsTrue(vm.CanInventoryFixture);
        await inventory.StartConfirmedAsync();
        Assert.IsTrue(vm.HasSnapshot);
        Assert.AreEqual(120, vm.MatchingFileCount);
        Assert.AreEqual(50, vm.Rows.Count);
        Assert.IsTrue(vm.Summary.Contains("jeux identifiés : inconnu", StringComparison.Ordinal));
        Assert.IsTrue(vm.Rows.All(x => x.Observation.SourceKind == "FILESYSTEM_OBSERVATION"));
        Assert.IsTrue(vm.Rows.All(x => x.ProvisionalLabel.StartsWith("File", StringComparison.Ordinal)));
        vm.NextPageCommand.Execute(null);
        Assert.AreEqual(1, vm.PageIndex);
        Assert.AreEqual(50, vm.Rows.Count);
        vm.NextPageCommand.Execute(null);
        Assert.AreEqual(20, vm.Rows.Count);
        Assert.IsFalse(vm.NextPageCommand.CanExecute(null));
        vm.SearchText = "File001";
        Assert.AreEqual(0, vm.PageIndex);
        Assert.AreEqual(1, vm.MatchingFileCount);
        vm.SearchText = "";
        vm.PlatformFilter = "UNKNOWN";
        Assert.AreEqual(60, vm.MatchingFileCount);
        vm.ExtensionFilter = ".zip";
        Assert.AreEqual(60, vm.MatchingFileCount);
        vm.KindFilter = nameof(InventoryKind.PotentialRom);
        Assert.AreEqual(0, vm.MatchingFileCount);
        vm.KindFilter = "Tous";
        vm.PlatformFilter = "Toutes";
        vm.ExtensionFilter = "Toutes";
        vm.SelectedRow = vm.Rows[0];
        vm.OpenFileCommand.Execute(null);
        Assert.IsTrue(vm.IsFileDetail);
        Assert.IsNotNull(vm.SelectedRow);
        Assert.IsTrue(vm.MediaNotice.Contains("non associée", StringComparison.Ordinal));
        vm.ReturnCollectionCommand.Execute(null);
        Assert.IsTrue(vm.IsCollection);
        Assert.AreSame(service.CompleteSnapshot, inventory.Current);
        Assert.IsNull(service.CompleteSnapshot.ConfirmedGameCount);
    }

    [TestMethod]
    public async Task N05Ux02_DegradedAttemptDoesNotReplaceLastCompleteOrInventDeletion()
    {
        var complete = Snapshot(4);
        var service = new FakeService(complete);
        using var inventory = new InventoryViewModel(service, action => action());
        using var vm = new ObservedLibraryViewModel(inventory, fixtureMode: true);
        inventory.PrepareSelectedFolder(new InventoryScope(@"C:\test-fixture"));
        inventory.ConsentConfirmed = true;
        await inventory.StartConfirmedAsync();
        var partial = Snapshot(1, InventoryScanStatus.Partial,
            InventoryAvailability.Offline, InventoryFreshness.Stale);
        service.Emit(complete, partial);
        Assert.AreSame(complete, inventory.DisplayedSnapshot);
        Assert.AreEqual(4, vm.MatchingFileCount);
        inventory.ShowAttempt();
        Assert.AreSame(partial, inventory.DisplayedSnapshot);
        Assert.AreEqual(1, vm.MatchingFileCount);
        Assert.IsTrue(vm.Summary.Contains("total inconnu", StringComparison.Ordinal));
        Assert.IsTrue(vm.SnapshotStatus.Contains("Offline", StringComparison.Ordinal));
        Assert.IsTrue(vm.SnapshotStatus.Contains("Stale", StringComparison.Ordinal));
        Assert.IsFalse(vm.SnapshotStatus.Contains("DELETED", StringComparison.Ordinal));
        inventory.ShowComplete();
        Assert.AreSame(complete, inventory.DisplayedSnapshot);
        Assert.AreEqual(4, vm.MatchingFileCount);
    }

    [TestMethod]
    public async Task N05Ux03_ProductionCannotEnableScanByConsent()
    {
        var service = new FakeService(Snapshot(2));
        using var inventory = new InventoryViewModel(service, action => action());
        using var vm = new ObservedLibraryViewModel(inventory, fixtureMode: false);
        inventory.PrepareSelectedFolder(new InventoryScope(@"C:\test-fixture"));
        inventory.ConsentConfirmed = true;
        Assert.IsFalse(vm.CanInventoryFixture);
        Assert.IsTrue(vm.ModeNotice.Contains("désactivé", StringComparison.Ordinal));
        Assert.IsFalse(vm.HasSnapshot);
        await Task.CompletedTask;
    }

    private sealed class FakeService : IInventoryApplicationService
    {
        public InventorySnapshot CompleteSnapshot { get; }
        public InventoryApplicationState State { get; private set; } =
            new(0, InventoryRunPhase.NoSelection, null, null, null, null, true);
        public event EventHandler<InventoryApplicationState>? StateChanged;

        public FakeService(InventorySnapshot complete) => CompleteSnapshot = complete;

        public InventorySelectionResult PrepareSelection(InventoryScope selection)
        {
            State = new(State.SelectionVersion + 1, InventoryRunPhase.Ready,
                selection.LibraryRoot, null, null, null, true);
            StateChanged?.Invoke(this, State);
            return new(true, null, State.SelectionVersion);
        }

        public Task<InventoryApplicationState> StartConfirmedScanAsync(
            InventoryRunRequest request, CancellationToken cancellationToken = default)
        {
            State = State with { Phase = InventoryRunPhase.Completed,
                Current = CompleteSnapshot, LastAttempt = CompleteSnapshot };
            StateChanged?.Invoke(this, State);
            return Task.FromResult(State);
        }

        public void Emit(InventorySnapshot complete, InventorySnapshot partial)
        {
            State = State with { Phase = InventoryRunPhase.Completed,
                Current = complete, LastAttempt = partial };
            StateChanged?.Invoke(this, State);
        }

        public bool RequestCancellation() => false;
        public bool ClearSelection()
        {
            State = new(State.SelectionVersion + 1, InventoryRunPhase.NoSelection,
                null, null, null, null, true);
            StateChanged?.Invoke(this, State);
            return true;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
