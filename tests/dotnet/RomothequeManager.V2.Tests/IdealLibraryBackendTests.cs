using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.RsA;
using RomothequeManager.V2.Persistence;

namespace RomothequeManager.V2.Tests;

/// <summary>Strictly synthetic Windows fixture and temporary SQLite tests:
/// no personal collection, no ROM bytes, no network, no Release or production app data.</summary>
[TestClass]
public sealed class IdealLibraryBackendTests
{
    private readonly List<string> _temporary = new();
    private string _db = "";

    [TestInitialize]
    public void Prepare()
    {
        var sqlite = Path.Combine(Path.GetTempPath(),
            "rm-persist01-fixture-" + Guid.NewGuid().ToString("N"));
        _temporary.Add(sqlite);
        _db = Path.Combine(sqlite, "ROMOTHEQUE_MANAGER", "data", "catalog.sqlite");
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var root in _temporary)
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private string Fixture(string romName = "A.fixture")
    {
        var folder = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"));
        _temporary.Add(folder);
        var roms = Path.Combine(folder, "ROMS", "gba");
        Directory.CreateDirectory(roms);
        File.WriteAllText(Path.Combine(roms, romName), "synthetic metadata fixture only");
        return folder;
    }

    private IdealLibraryBackendService Service(Action<string>? callback = null) =>
        new(new SqliteCatalogStore(_db), syntheticFixtureOnly: true, beforeChildOpenForTest: callback);

    private static InventorySelectionResult Select(IdealLibraryBackendService service, string path) =>
        service.PrepareSelection(new InventoryScope(path, MaxDepth: 4, MaxFiles: 32,
            MaxDirectories: 32, TimeLimit: TimeSpan.FromSeconds(20)));

    [TestMethod]
    public async Task IDEAL_01_SelectionIsNotConsent_GrantsAreOneShotAndOperationScoped()
    {
        var a = Fixture();
        await using var service = Service();
        var selected = Select(service, a);
        Assert.IsTrue(selected.Accepted);
        var noGrant = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual("REAL_LIBRARY_SCAN_HOLD", noGrant.SafeDiagnosticCode);
        Assert.IsNull(noGrant.Current);
        Assert.IsFalse(service.AuthorizeRomsRead(selected.SelectionVersion, false));
        Assert.IsFalse(service.AuthorizeRead(IdealReadOperation.MediasIndex,
            selected.SelectionVersion, true));
        Assert.IsFalse(service.AuthorizeRead(IdealReadOperation.MediaPreview,
            selected.SelectionVersion, true));
        Assert.IsFalse(service.AuthorizeRomsRead(selected.SelectionVersion - 1, true));
        Assert.IsTrue(service.AuthorizeRomsRead(selected.SelectionVersion, true));
        var first = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual(InventoryScanStatus.Complete, first.Current!.ScanStatus);
        Assert.AreEqual(1, first.Current.ObservedFileCount);
        Assert.AreEqual(InventoryAvailability.Online, first.Current.Availability);
        var replay = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual("REAL_LIBRARY_SCAN_HOLD", replay.SafeDiagnosticCode);
        Assert.AreEqual(first.Current.ScanId, replay.Current!.ScanId);
    }

    [TestMethod]
    public async Task IDEAL_02_AtoB_RevokesOldConsentAndPreventsCrossLibraryAdoption()
    {
        var a = Fixture("A.fixture");
        var b = Fixture("B.fixture");
        await using var service = Service();
        var first = Select(service, a);
        Assert.IsTrue(first.Accepted);
        Assert.IsTrue(service.AuthorizeRomsRead(first.SelectionVersion, true));
        var second = Select(service, b);
        Assert.IsTrue(second.Accepted);
        Assert.IsTrue(second.SelectionVersion > first.SelectionVersion);
        Assert.IsNull(service.State.Current);
        Assert.IsFalse(service.AuthorizeRomsRead(first.SelectionVersion, true));
        Assert.AreEqual("CONSENT_OR_SELECTION_REQUIRED",
            (await service.StartConfirmedScanAsync(
                new InventoryRunRequest(first.SelectionVersion, true))).SafeDiagnosticCode);
        Assert.IsTrue(service.AuthorizeRomsRead(second.SelectionVersion, true));
        var result = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(second.SelectionVersion, true));
        Assert.AreEqual(InventoryScanStatus.Complete, result.Current!.ScanStatus);
        Assert.IsTrue(result.Current.Observations.Single().RelativePath.EndsWith("B.fixture"));
        Assert.IsFalse(result.Current.Observations.Any(o => o.RelativePath.Contains("A.fixture")));
    }

    [TestMethod]
    public async Task IDEAL_03_CancelDuringRunning_DoubleStartAndLateResultKeepCurrent()
    {
        var a = Fixture("A.fixture");
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        var hold = false;
        await using var service = Service(relative =>
        {
            if (!hold || !relative.EndsWith("A.fixture", StringComparison.Ordinal)) return;
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(10)))
                throw new InvalidOperationException("SYNTHETIC_CALLBACK_TIMEOUT");
        });
        var selected = Select(service, a);
        Assert.IsTrue(selected.Accepted);
        Assert.IsTrue(service.AuthorizeRomsRead(selected.SelectionVersion, true));
        var good = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.IsNotNull(good.Current);
        var oldCurrent = good.Current.ScanId;
        hold = true;
        Assert.IsTrue(service.AuthorizeRomsRead(selected.SelectionVersion, true));
        var running = service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)));
        var second = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual(InventoryRunPhase.Running, second.Phase);
        Assert.IsFalse(service.ClearSelection());
        Assert.IsTrue(service.RequestCancellation());
        release.Set();
        var result = await running.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.AreNotEqual(InventoryScanStatus.Complete, result.LastAttempt!.ScanStatus);
        Assert.AreEqual(oldCurrent, result.Current!.ScanId);
        Assert.IsFalse(service.RequestCancellation());
    }

    [TestMethod]
    public async Task IDEAL_04_CloseReopenOffline_NoSourceReadsOrRestoredGrant()
    {
        var a = Fixture();
        string rootId;
        await using (var service = Service())
        {
            var selected = Select(service, a);
            Assert.IsTrue(selected.Accepted);
            Assert.IsTrue(service.AuthorizeRomsRead(selected.SelectionVersion, true));
            var scan = await service.StartConfirmedScanAsync(
                new InventoryRunRequest(selected.SelectionVersion, true));
            Assert.AreEqual(InventoryScanStatus.Complete, scan.Current!.ScanStatus);
            rootId = service.LoadSavedLibraries().Single().StorageRoot.StorageRootId;
        }
        // Physically remove the fixture: restoring its SQLite history must not
        // attempt even a metadata preflight or reactivate an old grant.
        Directory.Delete(a, recursive: true);
        await using var reopened = Service();
        Assert.AreEqual(1, reopened.LoadSavedLibraries().Count);
        Assert.IsTrue(reopened.RestoreSaved(rootId));
        Assert.IsNull(reopened.State.SelectedLocalRoot);
        Assert.AreEqual(InventoryAvailability.Offline, reopened.State.Current!.Availability);
        Assert.AreEqual(InventoryFreshness.Stale, reopened.State.Current.Freshness);
        Assert.IsFalse(reopened.AuthorizeRomsRead(reopened.State.SelectionVersion, true));
        var denied = await reopened.StartConfirmedScanAsync(
            new InventoryRunRequest(reopened.State.SelectionVersion, true));
        Assert.AreEqual("NO_EXPLICIT_SELECTION", denied.SafeDiagnosticCode);
        Assert.AreEqual(InventoryScanStatus.Complete, denied.Current!.ScanStatus);
    }

    [TestMethod]
    public async Task IDEAL_05_OutOfScopePathAndNoSelectionNeverRead()
    {
        var a = Fixture();
        var foreign = Path.Combine(Path.GetTempPath(), "unselected-" + Guid.NewGuid().ToString("N"));
        _temporary.Add(foreign);
        Directory.CreateDirectory(Path.Combine(foreign, "ROMS"));
        await using var service = Service();
        var invalid = Select(service, foreign);
        Assert.IsFalse(invalid.Accepted);
        Assert.IsFalse(service.AuthorizeRomsRead(invalid.SelectionVersion, true));
        var rejected = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(invalid.SelectionVersion, true));
        Assert.AreEqual("NO_EXPLICIT_SELECTION", rejected.SafeDiagnosticCode);
        var valid = Select(service, a);
        Assert.IsTrue(valid.Accepted);
        Assert.IsFalse(service.AuthorizeRomsRead(valid.SelectionVersion, false));
        Assert.IsTrue(service.ClearSelection());
        Assert.IsFalse(service.AuthorizeRomsRead(valid.SelectionVersion, true));
    }

    [TestMethod]
    public async Task IDEAL_07_WindowsNativeProductionPermission_UsesOnlyFictionalRoms()
    {
        // This is the production permission path, not syntheticFixtureOnly:true.
        // Only the SQLite constructor is test-injected so that LocalAppData is
        // never touched by this native Windows proof.
        if (!OperatingSystem.IsWindows()) return;
        // GitHub Windows RUNNER_TEMP is a disposable test-only directory outside
        // LocalAppData, which production intentionally refuses as a ROMS root.
        var testRoot = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (string.IsNullOrWhiteSpace(testRoot))
            testRoot = Path.GetTempPath();
        var applicationData = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
        var normalizedTemp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(testRoot));
        if (normalizedTemp.Equals(applicationData, StringComparison.OrdinalIgnoreCase) ||
            normalizedTemp.StartsWith(applicationData + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            Assert.Inconclusive("IDEAL_NATIVE_REQUIRES_DISPOSABLE_OUTSIDE_APPDATA_ROOT");
        var fixture = Path.Combine(normalizedTemp,
            "rm-ideal-native-fixture-" + Guid.NewGuid().ToString("N"));
        _temporary.Add(fixture);
        var fictionalRomDir = Path.Combine(fixture, "ROMS", "gba");
        Directory.CreateDirectory(fictionalRomDir);
        File.WriteAllText(Path.Combine(fictionalRomDir, "NativeFiction.gba"),
            "synthetic metadata fixture only");
        await using var service = new IdealLibraryBackendService(
            new SqliteCatalogStore(_db), syntheticFixtureOnly: false);
        var selected = Select(service, fixture);
        Assert.IsTrue(selected.Accepted);
        Assert.IsFalse(service.AuthorizeRomsRead(selected.SelectionVersion, false));
        Assert.IsFalse(service.AuthorizeRomsRead(selected.SelectionVersion - 1, true));
        Assert.IsFalse(service.AuthorizeRead(
            IdealReadOperation.MediasIndex, selected.SelectionVersion, true));
        Assert.IsFalse(service.AuthorizeRead(
            IdealReadOperation.MediaPreview, selected.SelectionVersion, true));
        var denied = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual("REAL_LIBRARY_SCAN_HOLD", denied.SafeDiagnosticCode);
        Assert.IsNull(denied.Current);

        // Native authorization binds the opened ROMS handle's volume/FileID.
        // The scanner opens only directory handles and file attributes.
        Assert.IsTrue(service.AuthorizeRomsRead(selected.SelectionVersion, true));
        var completed = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual(InventoryRunPhase.Completed, completed.Phase);
        Assert.AreEqual(InventoryScanStatus.Complete, completed.Current!.ScanStatus);
        Assert.AreEqual(1, completed.Current.ObservedFileCount);
        Assert.IsTrue(completed.Current.Observations.Single()
            .RelativePath.EndsWith("NativeFiction.gba", StringComparison.Ordinal));
        Assert.IsTrue(completed.Current.Observations.All(o =>
            o.SourceKind == "RS_A_HANDLE_METADATA_SELECTED"));

        // One-shot capability cannot be replayed; the Complete is retained.
        var replay = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Assert.AreEqual("REAL_LIBRARY_SCAN_HOLD", replay.SafeDiagnosticCode);
        Assert.AreEqual(completed.Current.ScanId, replay.Current!.ScanId);
        Assert.IsTrue(service.ClearSelection());
        Assert.IsFalse(service.AuthorizeRomsRead(selected.SelectionVersion, true));
    }

    [TestMethod]
    public void IDEAL_06_LocalPathPolicyRejectsNetworkDeviceAndTraversal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var valid = Fixture();
        var roms = Path.Combine(valid, "ROMS");
        Assert.AreEqual(Path.GetFullPath(roms),
            RsAAnchoredReader.ValidateSelectedLocalRomsPath(roms));
        foreach (var bad in new[]
        {
            @"\\server\share\ROMS", @"\\?\C:\fixture\ROMS",
            @"\\.\C:\fixture\ROMS", @"C:\fixture\..\ROMS",
            Path.Combine(valid, "ROMS", "gba"), valid
        })
        {
            var refused = false;
            try { RsAAnchoredReader.ValidateSelectedLocalRomsPath(bad); }
            catch (Exception) { refused = true; }
            Assert.IsTrue(refused, "Unsafe selected root must be refused.");
        }
    }
}
