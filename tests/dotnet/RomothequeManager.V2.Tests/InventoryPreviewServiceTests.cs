using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

// Preview ONLY: nonexistent paths and invented observation data.
// Does not instantiate an InventorySession, call filesystem APIs or use Avalonia.
[TestClass]
public sealed class InventoryPreviewServiceTests
{
    private const string UncreatedPath = @"C:\UNCREATED_NEXT03_UI_ONLY";

    [TestMethod]
    public async Task P01_FolderSelectionDoesNotScanAndConsentIsRequired()
    {
        await using var service = new InventoryPreviewService();
        var selected = service.PrepareSelection(new InventoryScope(UncreatedPath));
        Assert.IsTrue(selected.Accepted);
        Assert.AreEqual(InventoryRunPhase.Ready, service.State.Phase);
        Assert.IsNull(service.State.LastAttempt);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await service.StartConfirmedScanAsync(new(selected.SelectionVersion, false)));
        Assert.AreEqual(InventoryRunPhase.Ready, service.State.Phase);
        Assert.IsNull(service.State.Current);
    }

    [TestMethod]
    public async Task P02_CancelPreservesPreviousCompleteAndDoesNotFabricateTotal()
    {
        await using var service = new InventoryPreviewService();
        var sel = service.PrepareSelection(new InventoryScope(UncreatedPath));
        var first = service.StartConfirmedScanAsync(new(sel.SelectionVersion, true));
        var concurrentWasRejected = false;
        try { await service.StartConfirmedScanAsync(new(sel.SelectionVersion, true)); }
        catch (InvalidOperationException) { concurrentWasRejected = true; }
        Assert.IsTrue(concurrentWasRejected, "Single-flight must refuse a second run");
        var complete = await first;
        Assert.AreEqual(InventoryScanStatus.Complete, complete.Current?.ScanStatus);
        Assert.AreEqual(3, complete.Current?.ObservedFileCount);
        Assert.IsTrue(complete.Current!.Observations.All(x => x.SourceKind == "NEXT03_UI_SIMULATION"));
        var second = service.StartConfirmedScanAsync(new(sel.SelectionVersion, true));
        // Verify refusal while Running. After cancellation, the asynchronous
        // simulation may complete before the next assertion on a busy runner.
        Assert.AreEqual(InventoryRunPhase.Running, service.State.Phase);
        Assert.IsFalse(service.ClearSelection(), "Running simulation must keep its selection");
        Assert.IsTrue(service.RequestCancellation());
        var partial = await second;
        Assert.AreSame(complete.Current, partial.Current);
        Assert.AreEqual(InventoryScanStatus.Partial, partial.LastAttempt?.ScanStatus);
        Assert.IsNull(partial.LastAttempt?.TotalFileCount);
        Assert.IsTrue(partial.LastAttempt!.Diagnostics.Any(x => x.Code == "CANCELLED"));
    }

    [TestMethod]
    public async Task P03_OfflineAndStaleAreDistinctFromScanStatus()
    {
        await using var service = new InventoryPreviewService();
        var selected = service.PrepareSelection(new InventoryScope(UncreatedPath));
        Assert.IsTrue(service.SelectPreviewOutcome(InventoryPreviewOutcome.Offline));
        var offlineRun = service.StartConfirmedScanAsync(new(selected.SelectionVersion, true));
        var offline = await offlineRun;
        Assert.AreEqual(InventoryScanStatus.Failed, offline.LastAttempt?.ScanStatus);
        Assert.AreEqual(InventoryAvailability.Offline, offline.LastAttempt?.Availability);
        Assert.IsNull(offline.LastAttempt?.TotalFileCount);
        Assert.IsTrue(service.SelectPreviewOutcome(InventoryPreviewOutcome.Stale));
        var stale = await service.StartConfirmedScanAsync(new(selected.SelectionVersion, true));
        Assert.AreEqual(InventoryScanStatus.Complete, stale.Current?.ScanStatus);
        Assert.AreEqual(InventoryFreshness.Stale, stale.Current?.Freshness);
        Assert.IsNull(stale.Current?.ConfirmedGameCount);
    }

    [TestMethod]
    public async Task P05_DifferentLibraryMustWaitForCancelledSimulation()
    {
        await using var service = new InventoryPreviewService();
        var first = service.PrepareSelection(new InventoryScope(UncreatedPath));
        var running = service.StartConfirmedScanAsync(new(first.SelectionVersion, true));
        var rejected = service.PrepareSelection(new InventoryScope(@"C:\UNCREATED_OTHER_UI_ONLY"));
        Assert.IsFalse(rejected.Accepted);
        Assert.AreEqual("SIMULATION_BUSY", rejected.SafeDiagnosticCode);
        Assert.AreEqual(UncreatedPath, service.State.SelectedLocalRoot);
        // Test the busy-state guard before requesting cancellation: the
        // cancellation continuation may legitimately complete immediately.
        Assert.IsFalse(service.ClearSelection(), "Running simulation must refuse clearing selection");
        Assert.IsTrue(service.RequestCancellation());
        var completed = await running;
        Assert.AreEqual(InventoryScanStatus.Partial, completed.LastAttempt?.ScanStatus);
        var second = service.PrepareSelection(new InventoryScope(@"C:\UNCREATED_OTHER_UI_ONLY"));
        Assert.IsTrue(second.Accepted);
        Assert.IsTrue(second.SelectionVersion > first.SelectionVersion);
        Assert.IsNull(service.State.LastAttempt);
        Assert.IsNull(service.State.Current);
        Assert.AreEqual(@"C:\UNCREATED_OTHER_UI_ONLY", service.State.SelectedLocalRoot);
    }

    [TestMethod]
    public async Task P06_RejectsNetworkAndObsoleteConsentVersionsWithoutDiskReads()
    {
        await using var service = new InventoryPreviewService();
        var refused = service.PrepareSelection(new InventoryScope(@"\\remote\share"));
        Assert.IsFalse(refused.Accepted);
        Assert.AreEqual(InventoryRunPhase.NoSelection, service.State.Phase);
        var first = service.PrepareSelection(new InventoryScope(UncreatedPath));
        Assert.IsTrue(first.Accepted);
        var second = service.PrepareSelection(new InventoryScope(@"C:\UNCREATED_OTHER_UI_ONLY"));
        Assert.IsTrue(second.Accepted);
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await service.StartConfirmedScanAsync(new(first.SelectionVersion, true)));
        Assert.AreEqual(InventoryRunPhase.Ready, service.State.Phase);
        Assert.IsNull(service.State.LastAttempt);
    }

    [TestMethod]
    public async Task P07_FailedPreviewHasUnknownTotalAndSimulationProvenance()
    {
        await using var service = new InventoryPreviewService();
        var selected = service.PrepareSelection(new InventoryScope(UncreatedPath));
        Assert.IsTrue(service.SelectPreviewOutcome(InventoryPreviewOutcome.Failed));
        var ended = await service.StartConfirmedScanAsync(new(selected.SelectionVersion, true));
        Assert.AreEqual(InventoryScanStatus.Failed, ended.LastAttempt?.ScanStatus);
        Assert.IsNull(ended.LastAttempt?.TotalFileCount);
        Assert.IsNull(ended.LastAttempt?.ConfirmedGameCount);
        Assert.AreEqual("NEXT03_UI_SIMULATION", ended.LastAttempt?.Diagnostics.Single().SourceKind);
    }

    [TestMethod]
    public void P04_ViewModelHasNoAvaloniaReferencesOrScanner()
    {
        var vmType = typeof(InventoryViewModel);
        Assert.IsFalse(vmType.GetInterfaces().Any(x =>
            x.FullName?.Contains("Avalonia", StringComparison.Ordinal) == true));
        Assert.IsFalse(vmType.GetFields(System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance).Any(x =>
            x.FieldType == typeof(InventorySession)));
    }
}
