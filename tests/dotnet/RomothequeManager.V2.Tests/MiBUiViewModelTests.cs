using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory.Media.MiB;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class MiBUiViewModelTests
{
    private sealed class FakeService : IMiBFixtureMediaApplicationService
    {
        public MiBProjectionState State { get; private set; } =
            new(0, MiBPhase.NoSelection, false, null, null, false, null);
        public event EventHandler<MiBProjectionState>? StateChanged;
        public int StartCount, ConsentCount, CancelCount;
        public TaskCompletionSource<MiBProjectionState>? Pending;
        private void Set(MiBProjectionState state) { State = state; StateChanged?.Invoke(this, state); }
        public MiBSelectionResult SelectFixtureRoot(string root)
        {
            Set(new(State.SelectionVersion + 1, MiBPhase.Ready, false, null, null, false, null));
            return new(true, State.SelectionVersion, null);
        }
        public bool ConfirmMediaIndexConsent(long version, bool action)
        {
            if (!action || version != State.SelectionVersion) return false;
            ConsentCount++;
            Set(State with { Phase = MiBPhase.Ready, SafeNoticeCode = null });
            return true;
        }
        public Task<MiBProjectionState> StartConfirmedAsync(long version, CancellationToken token = default)
        {
            StartCount++;
            Set(State with { Phase = MiBPhase.Running, ProgressIsIndeterminate = true });
            Pending = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return Pending.Task;
        }
        public bool RequestCancellation()
        {
            CancelCount++;
            Set(State with { Phase = MiBPhase.CancellationRequested });
            return true;
        }
        public bool ClearSelection() { SelectFixtureRoot("B"); return true; }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public void Finish(MiBProjectionState result) { Set(result); Pending?.TrySetResult(result); }
        public void FinishLate(MiBProjectionState result) { Pending?.TrySetResult(result); }
        public void EmitStale(MiBProjectionState result) => StateChanged?.Invoke(this, result);
        public void PublishState(MiBProjectionState result) => Set(result);
    }

    private static MiBRunView Run(string id) =>
        new(id, "mia-media-fixture", DateTimeOffset.UtcNow,
            RomothequeManager.V2.Inventory.InventoryScanStatus.Complete,
            RomothequeManager.V2.Inventory.InventoryAvailability.Online,
            RomothequeManager.V2.Inventory.InventoryFreshness.Fresh,
            Array.Empty<RomothequeManager.V2.Inventory.InventoryCoverage>(),
            1, 1, Array.Empty<MiBMediaRow>(), Array.Empty<string>(), false);

    [TestMethod]
    public async Task UI_MI_B01_SelectIsNotConsentOrStart()
    {
        var service = new FakeService();
        await using var vm = new MiBUiViewModel(service, "fixture", a => a());
        Assert.IsFalse(vm.CanStart);
        Assert.IsTrue(vm.Select());
        Assert.IsFalse(vm.CanStart);
        Assert.AreEqual(0, service.ConsentCount);
        Assert.IsTrue(vm.Confirm());
        Assert.IsTrue(vm.CanStart);
        Assert.AreEqual(0, service.StartCount);
    }

    [TestMethod]
    public async Task UI_MI_B02_StartConsumesConsentAndProgressIsUnknown()
    {
        var service = new FakeService();
        await using var vm = new MiBUiViewModel(service, "fixture", a => a());
        vm.Select(); vm.Confirm();
        var work = vm.StartAsync();
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsTrue(vm.ProgressIndeterminate);
        Assert.IsTrue(vm.ProgressLabel.Contains("total inconnu"));
        Assert.IsFalse(vm.CanStart);
        var result = service.State with { Phase = MiBPhase.Completed, ProgressIsIndeterminate = false,
            CurrentComplete = Run("mia-run1"), LastAttempt = Run("mia-run1") };
        service.Finish(result);
        await work;
        Assert.IsNotNull(vm.CurrentComplete);
        Assert.AreEqual(1, service.StartCount);
    }

    [TestMethod]
    public async Task UI_MI_B03_CancelRetainsCurrentAndShowsAttempt()
    {
        var service = new FakeService();
        await using var vm = new MiBUiViewModel(service, "fixture", a => a());
        vm.Select(); vm.Confirm();
        var first = vm.StartAsync();
        var current = Run("mia-current");
        service.Finish(service.State with { Phase = MiBPhase.Completed, CurrentComplete = current,
            LastAttempt = current, ProgressIsIndeterminate = false });
        await first;
        vm.Confirm();
        var second = vm.StartAsync();
        Assert.IsTrue(vm.Cancel());
        Assert.AreEqual(1, service.CancelCount);
        var failed = Run("mia-cancelled");
        service.Finish(service.State with { Phase = MiBPhase.Completed, CurrentComplete = current,
            LastAttempt = failed, SafeNoticeCode = "MI_B_CANCELLED", ProgressIsIndeterminate = false });
        await second;
        Assert.AreEqual("mia-current", vm.CurrentComplete!.RunRef);
        Assert.AreEqual("mia-cancelled", vm.LastAttempt!.RunRef);
        vm.ShowAttemptCommand.Execute(null);
        Assert.AreEqual("mia-cancelled", vm.Displayed!.RunRef);
    }

    [TestMethod]
    public async Task UI_MI_B04_SelectionBRemovesAAndRejectsLateA()
    {
        var service = new FakeService();
        await using var vm = new MiBUiViewModel(service, "fixture", a => a());
        vm.Select(); vm.Confirm();
        var running = vm.StartAsync();
        var versionA = service.State.SelectionVersion;
        // Backend allows selection during in-flight operations; VM must never display A as B.
        service.SelectFixtureRoot("B");
        service.FinishLate(new(versionA, MiBPhase.Completed, false, Run("mia-A"), Run("mia-A"), false, null));
        await running;
        service.EmitStale(new(versionA, MiBPhase.Completed, false, Run("mia-A"), Run("mia-A"), false, null));
        Assert.IsNull(vm.CurrentComplete);
        Assert.IsNull(vm.LastAttempt);
    }

    [TestMethod]
    public async Task UI_MI_B05_OfflineStaleNeverAutoGrants()
    {
        var service = new FakeService();
        await using var vm = new MiBUiViewModel(service, "fixture", a => a());
        var archived = Run("mia-archive") with {
            Availability = RomothequeManager.V2.Inventory.InventoryAvailability.Offline,
            SourceFreshness = RomothequeManager.V2.Inventory.InventoryFreshness.Stale,
            IsHistoricalFixture = true
        };
        service.PublishState(new(0, MiBPhase.NoSelection, false,
            archived, null, true, "MI_B_HISTORY_OFFLINE"));
        Assert.IsFalse(vm.CanStart);
        Assert.IsFalse(vm.CanConfirm);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsNotNull(vm.Displayed);
        Assert.IsTrue(vm.AvailabilityLabel.Contains("Offline"));
        Assert.IsTrue(vm.AvailabilityLabel.Contains("Stale"));
        Assert.AreEqual(0, service.ConsentCount);
        Assert.AreEqual(0, service.StartCount);
    }
}
