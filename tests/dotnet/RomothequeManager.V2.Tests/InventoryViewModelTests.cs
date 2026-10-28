using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

// Contract-shaped fake; NO InventorySession, I/O, Avalonia, filesystem or ROM.
[TestClass]
public sealed class InventoryViewModelTests
{
    private static InventoryScope Scope(string path = @"C:\fixture-synthetic") => new(path);

    [TestMethod]
    public void F01_SelectionDoesNotStartScanAndRequiresExplicitConsent()
    {
        var fake = new FakeInventoryService();
        using var vm = new InventoryViewModel(fake, action => action());
        Assert.IsFalse(vm.StartInventoryCommand.CanExecute(null));
        Assert.IsFalse(vm.CanCancel);

        var selection = vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        Assert.IsTrue(selection.Accepted);
        Assert.AreEqual(0, fake.StartCalls);
        Assert.AreEqual(1, fake.PrepareCalls);
        Assert.IsFalse(vm.StartInventoryCommand.CanExecute(null));
        vm.ConsentConfirmed = true;
        Assert.IsTrue(vm.StartInventoryCommand.CanExecute(null));
        Assert.AreEqual(InventoryRunPhase.Ready, vm.Phase);
        Assert.IsFalse(vm.IsProgressIndeterminate);
    }

    [TestMethod]
    public async Task F02_StartIsSingleFlightAndIndeterminateUntilServiceCompletes()
    {
        var fake = new FakeInventoryService();
        using var vm = new InventoryViewModel(fake, action => action());
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        int canExecuteEvents = 0;
        vm.StartInventoryCommand.CanExecuteChanged += (_, _) => canExecuteEvents++;
        var running = vm.StartConfirmedAsync();
        Assert.AreEqual(1, fake.StartCalls);
        Assert.IsTrue(fake.LastRequest!.ExplicitUserConsent);
        Assert.IsTrue(vm.IsBusy);
        Assert.IsTrue(vm.IsProgressIndeterminate);
        Assert.IsFalse(vm.CanChangeFolder);
        Assert.IsFalse(vm.StartInventoryCommand.CanExecute(null));
        await vm.StartConfirmedAsync();
        Assert.AreEqual(1, fake.StartCalls);
        Assert.IsTrue(canExecuteEvents > 0);
        var done = Snapshot("complete", InventoryScanStatus.Complete, 1);
        fake.Complete(done, done);
        await running;
        Assert.IsFalse(vm.IsBusy);
        Assert.AreEqual("Total ROMS couvert : 1", vm.TotalFilesText);
    }

    [TestMethod]
    public async Task F03_CancelRequestIsNotCompletionAndPreservesLastComplete()
    {
        var fake = new FakeInventoryService();
        using var vm = new InventoryViewModel(fake, action => action());
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        var first = vm.StartConfirmedAsync();
        var complete = Snapshot("old", InventoryScanStatus.Complete, 1);
        fake.Complete(complete, complete);
        await first;

        vm.ConsentConfirmed = true;
        var second = vm.StartConfirmedAsync();
        Assert.IsTrue(vm.CancelInventoryCommand.CanExecute(null));
        Assert.IsTrue(vm.RequestCancellation());
        Assert.AreEqual(InventoryRunPhase.CancellationRequested, vm.Phase);
        Assert.IsTrue(vm.IsBusy); // must wait for the motor's controlled outcome
        Assert.IsFalse(vm.CancelInventoryCommand.CanExecute(null));
        var partial = Snapshot("cancelled", InventoryScanStatus.Partial, 1);
        fake.Complete(complete, partial);
        await second;
        Assert.AreSame(complete, vm.Current);
        Assert.AreSame(partial, vm.LastAttempt);
        Assert.AreSame(complete, vm.DisplayedSnapshot);
        vm.ShowLastAttemptCommand.Execute(null);
        Assert.AreSame(partial, vm.DisplayedSnapshot);
        Assert.IsTrue(vm.TotalFilesText.Contains("Inconnu", StringComparison.Ordinal));
        Assert.AreEqual("Jeux identifiés : Inconnu", vm.IdentifiedGamesText);
    }

    [TestMethod]
    public async Task F04_MysteryObservationsAreNotGameIdentity()
    {
        var fake = new FakeInventoryService();
        using var vm = new InventoryViewModel(fake, action => action());
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        var running = vm.StartConfirmedAsync();
        var mystery = Snapshot("mystery", InventoryScanStatus.Complete, 32);
        fake.Complete(mystery, mystery);
        await running;
        Assert.AreEqual(32, vm.Observations.Count);
        Assert.AreEqual("Fichiers observés : 32", vm.ObservedFilesText);
        Assert.AreEqual("Jeux identifiés : Inconnu", vm.IdentifiedGamesText);
        Assert.IsFalse(vm.Observations.Any(x => x.RelativePath.Contains("demo01-", StringComparison.Ordinal)));
        Assert.AreEqual("FILESYSTEM_OBSERVATION", vm.SourceLabel);
        var selected = vm.Observations.First();
        vm.SelectObservationCommand.Execute(selected);
        Assert.AreSame(selected, vm.SelectedObservation);
    }

    [TestMethod]
    public async Task F05_NewSelectionRejectsLatePreviousEventsAndClearsObservation()
    {
        var fake = new FakeInventoryService();
        var pending = new Queue<Action>();
        using var vm = new InventoryViewModel(fake, action => pending.Enqueue(action));
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        var stale = fake.State;
        var running = vm.StartConfirmedAsync();
        var complete = Snapshot("a", InventoryScanStatus.Complete, 1);
        fake.Complete(complete, complete);
        await running;
        while (pending.Count != 0) pending.Dequeue()();
        vm.SelectObservationCommand.Execute(vm.Observations.Single());
        Assert.IsNotNull(vm.SelectedObservation);
        var next = vm.PrepareSelectedFolder(@"C:\new-fixture", Scope(@"C:\new-fixture"));
        Assert.IsTrue(next.Accepted);
        Assert.IsNull(vm.SelectedObservation);
        fake.Publish(stale); // queued event from a previous library
        while (pending.Count != 0) pending.Dequeue()();
        Assert.AreEqual(@"C:\new-fixture", vm.SelectedLocalRoot);
        Assert.IsNull(vm.DisplayedSnapshot);
        Assert.AreEqual("Jeux identifiés : Inconnu", vm.IdentifiedGamesText);
    }

    [TestMethod]
    public async Task F06_FailedAttemptAndUnavailableRootNeverFabricateZero()
    {
        var fake = new FakeInventoryService();
        using var vm = new InventoryViewModel(fake, action => action());
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        var running = vm.StartConfirmedAsync();
        var failed = Snapshot("failed", InventoryScanStatus.Failed, 0);
        fake.Complete(null, failed);
        await running;
        Assert.AreEqual("Total ROMS : Inconnu (couverture non complète)", vm.TotalFilesText);
        Assert.AreEqual("Jeux identifiés : Inconnu", vm.IdentifiedGamesText);
        Assert.AreEqual(InventoryAvailability.Offline.ToString(), vm.AvailabilityText);
        Assert.IsTrue(vm.Diagnostics.Any(d => d.Code == "ROOT_UNAVAILABLE"));
    }

    [TestMethod]
    public async Task F07_ServiceExceptionDoesNotCreateFalseSnapshotOrLeakPath()
    {
        var fake = new FakeInventoryService { ThrowOnStart = true };
        using var vm = new InventoryViewModel(fake, action => action());
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        await vm.StartConfirmedAsync();
        Assert.IsNull(vm.Current);
        Assert.IsNull(vm.LastAttempt);
        Assert.IsTrue(vm.StatusMessage.Contains("SERVICE_FAULT", StringComparison.Ordinal));
        Assert.IsFalse(vm.StatusMessage.Contains("sensitive", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void F08_RejectedSelectionPreservesCurrentContextAndDisposeStopsEvents()
    {
        var fake = new FakeInventoryService();
        var vm = new InventoryViewModel(fake, action => action());
        vm.PrepareSelectedFolder(@"C:\fixture-synthetic", Scope());
        fake.RejectNextSelection = true;
        var rejected = vm.PrepareSelectedFolder(@"\\remote\share", Scope());
        Assert.IsFalse(rejected.Accepted);
        Assert.AreEqual(@"C:\fixture-synthetic", vm.SelectedLocalRoot);
        var events = 0;
        vm.PropertyChanged += (_, _) => events++;
        vm.Dispose();
        var old = events;
        fake.Publish(fake.State with { Phase = InventoryRunPhase.Faulted });
        Assert.AreEqual(old, events);
        Assert.IsFalse(vm.StartInventoryCommand.CanExecute(null));
    }

    private static InventorySnapshot Snapshot(string name, InventoryScanStatus scanStatus, int count)
    {
        var now = DateTimeOffset.UtcNow;
        var observations = Enumerable.Range(0, count).Select(i => new FileObservation(
            "obs-" + name + "-" + i, "location-" + name + "-" + i, "UNKNOWN",
            "ROMS/unknown/f" + i + ".bin", InventoryKind.ArchiveCandidate, ".bin",
            10, now, now, "FILESYSTEM_OBSERVATION", InventoryAvailability.Online,
            Array.Empty<string>())).ToArray();
        var diagnostics = scanStatus == InventoryScanStatus.Failed
            ? new[] { new InventoryDiagnostic("ROOT_UNAVAILABLE", InventorySeverity.Error,
                "ROMS", "Synthetic unavailable root", "FILESYSTEM_OBSERVATION", now) }
            : Array.Empty<InventoryDiagnostic>();
        return new InventorySnapshot(
            "next-02-inventory/0.1-candidate", "library-" + name, name, now, scanStatus,
            scanStatus == InventoryScanStatus.Failed ? InventoryAvailability.Offline : InventoryAvailability.Online,
            InventoryFreshness.Unknown,
            new[] { new InventoryCoverage("ROMS", scanStatus) }, observations,
            diagnostics, Array.Empty<InventoryPlaylist>(), Array.Empty<PotentialDuplicateGroup>(),
            "synthetic-" + name, count, scanStatus == InventoryScanStatus.Complete ? count : null, null);
    }

    private sealed class FakeInventoryService : IInventoryApplicationService
    {
        private TaskCompletionSource<InventoryApplicationState>? _pending;
        public InventoryApplicationState State { get; private set; } =
            new(0, InventoryRunPhase.NoSelection, null, null, null, null, false);
        public event EventHandler<InventoryApplicationState>? StateChanged;
        public int PrepareCalls { get; private set; }
        public int StartCalls { get; private set; }
        public InventoryRunRequest? LastRequest { get; private set; }
        public bool RejectNextSelection { get; set; }
        public bool ThrowOnStart { get; set; }

        public InventorySelectionResult PrepareSelection(string path, InventoryScope scope)
        {
            PrepareCalls++;
            if (RejectNextSelection)
            {
                RejectNextSelection = false;
                return new(false, "ROOT_REJECTED", State.SelectionVersion);
            }
            State = new(State.SelectionVersion + 1, InventoryRunPhase.Ready,
                path, null, null, null, false);
            Publish(State);
            return new(true, null, State.SelectionVersion);
        }

        public Task<InventoryApplicationState> StartConfirmedScanAsync(
            InventoryRunRequest request, CancellationToken cancellationToken = default)
        {
            StartCalls++;
            LastRequest = request;
            if (ThrowOnStart) throw new InvalidOperationException(@"C:\sensitive\root");
            _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            State = State with { Phase = InventoryRunPhase.Running, ProgressIsIndeterminate = true };
            Publish(State);
            return _pending.Task;
        }

        public bool RequestCancellation()
        {
            if (State.Phase != InventoryRunPhase.Running) return false;
            State = State with { Phase = InventoryRunPhase.CancellationRequested };
            Publish(State);
            return true;
        }

        public void Complete(InventorySnapshot? current, InventorySnapshot attempt)
        {
            State = State with { Phase = InventoryRunPhase.Completed, Current = current,
                LastAttempt = attempt, ProgressIsIndeterminate = false };
            Publish(State);
            _pending!.TrySetResult(State);
        }

        public void Publish(InventoryApplicationState state) => StateChanged?.Invoke(this, state);

        public bool ClearSelection()
        {
            State = new(State.SelectionVersion + 1, InventoryRunPhase.NoSelection,
                null, null, null, null, false);
            Publish(State);
            return true;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
