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
        Assert.IsTrue(service.RequestCancellation());
        Assert.AreEqual(InventoryRunPhase.CancellationRequested, service.State.Phase);
        Assert.IsFalse(service.ClearSelection());
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
