using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.RsA;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

/// <summary>UI-RSA01..08: disposable Windows fixtures only. No personal filesystem,
/// public scanner permission, media preview, SQLite write or production activation.</summary>
[TestClass]
public sealed class Next05RsAUiTests
{
    private string _base = "";
    private string _roms = "";
    private string _rootId = "";

    [TestInitialize]
    public void CreateFixture()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "UI_RSA_WINDOWS_NATIVE_REQUIRED");
        _base = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"));
        _roms = Path.Combine(_base, "ROMS");
        _rootId = Guid.NewGuid().ToString("D");
        Directory.CreateDirectory(Path.Combine(_roms, "gba"));
        File.WriteAllText(Path.Combine(_roms, "gba", "Fictitious.gba"),
            "SYNTHETIC_METADATA_ONLY");
    }

    [TestCleanup]
    public void RemoveFixture()
    {
        if (_base.Length > 0 && Directory.Exists(_base))
            Directory.Delete(_base, recursive: true);
    }

    private RsAUiViewModel Model(RsAFixtureRomSession? scanner = null) =>
        new(action => action(), scanner);

    [TestMethod]
    public void UI_RSA_01_NoAutomaticScanAtStartupOrAfterViewNavigation()
    {
        using var vm = Model();
        using var inventory = new InventoryViewModel(new InventoryApplicationService(),
            action => action());
        using var observed = new ObservedLibraryViewModel(inventory, fixtureMode: true);
        observed.AttachRsAForCi(vm);
        Assert.IsTrue(observed.HasRsA);
        Assert.IsFalse(observed.HasSnapshot);
        Assert.IsNull(vm.CurrentComplete);
        Assert.IsNull(vm.LastAttempt);
        Assert.AreEqual(RsAUiPhase.Idle, vm.Phase);
        Assert.IsFalse(vm.CanStart);
        observed.ShowCollectionCommand.Execute(null);
        observed.ShowDashboardCommand.Execute(null);
        Assert.IsNull(vm.CurrentComplete);
        Assert.IsFalse(observed.CanInventoryFixture);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());
    }

    [TestMethod]
    public void UI_RSA_02_SelectionAloneIsNotConsentAndNewSelectionRevokesIt()
    {
        using var vm = Model();
        Assert.IsTrue(vm.SelectFixture(_roms, _rootId));
        Assert.AreEqual(RsAUiPhase.AwaitingConsent, vm.Phase);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsFalse(vm.CanStart);
        Assert.IsNull(vm.LastAttempt);
        vm.ConsentConfirmed = true;
        Assert.IsTrue(vm.CanStart);
        Assert.IsTrue(vm.SelectFixture(_roms, _rootId));
        Assert.IsFalse(vm.CanStart);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsNull(vm.CurrentComplete);
    }

    [TestMethod]
    public async Task UI_RSA_03_StartWithOneShotConsentScansMetadataOnly()
    {
        using var vm = Model();
        Assert.IsTrue(vm.SelectFixture(_roms, _rootId));
        await vm.StartConfirmedAsync();
        Assert.AreEqual(RsAUiPhase.Refused, vm.Phase);
        Assert.IsNull(vm.CurrentComplete);
        vm.ConsentConfirmed = true;
        await vm.StartConfirmedAsync();
        Assert.AreEqual(RsAUiPhase.Complete, vm.Phase);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsFalse(vm.CanStart);
        Assert.AreEqual(InventoryScanStatus.Complete, vm.CurrentComplete?.ScanStatus);
        Assert.AreEqual(1, vm.CurrentComplete?.ObservedFileCount);
        Assert.IsTrue(vm.CurrentComplete!.Observations.All(x =>
            x.SourceKind == "RS_A_HANDLE_METADATA_FIXTURE"));
        Assert.AreSame(vm.CurrentComplete, vm.LastAttempt);
    }

    [TestMethod]
    public async Task UI_RSA_04_CancelRevokesGrantAndPreservesPreviousComplete()
    {
        using var vm = Model();
        Assert.IsTrue(vm.SelectFixture(_roms, _rootId));
        vm.ConsentConfirmed = true;
        await vm.StartConfirmedAsync();
        var known = vm.CurrentComplete;
        Assert.IsNotNull(known);
        vm.ConsentConfirmed = true;
        var running = vm.StartConfirmedAsync();
        Assert.IsTrue(vm.IsBusy);
        Assert.IsTrue(vm.RequestCancellation());
        await running;
        Assert.AreSame(known, vm.CurrentComplete);
        Assert.IsTrue(vm.LastAttempt?.ScanStatus is InventoryScanStatus.Failed or
            InventoryScanStatus.Partial or InventoryScanStatus.Complete);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsFalse(vm.CanCancel);
        Assert.IsFalse(vm.CanStart);
    }

    [TestMethod]
    public void UI_RSA_05_TraversalAndUnapprovedRootsAreRefusedByScanner()
    {
        using var vm = Model();
        Assert.IsFalse(vm.SelectFixture(Path.Combine(_base, "ROMS", "..", "OUTSIDE"),
            _rootId));
        Assert.AreEqual(RsAUiPhase.Refused, vm.Phase);
        Assert.IsFalse(vm.CanStart);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.IsNull(vm.LastAttempt);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());
    }

    [TestMethod]
    public async Task UI_RSA_06_OfflineStaleRetainsHistoryWithoutReauthorization()
    {
        using var vm = Model();
        Assert.IsTrue(vm.SelectFixture(_roms, _rootId));
        vm.ConsentConfirmed = true;
        await vm.StartConfirmedAsync();
        var complete = vm.CurrentComplete;
        vm.MarkOfflineForCi();
        Assert.AreEqual(RsAUiPhase.Offline, vm.Phase);
        Assert.IsTrue(vm.IsOffline);
        Assert.IsFalse(vm.CanStart);
        Assert.IsFalse(vm.ConsentConfirmed);
        Assert.AreSame(complete, vm.CurrentComplete);
        StringAssert.Contains(vm.SnapshotLabel, "OFFLINE / STALE");
        Assert.IsFalse(vm.ProgressLabel.Contains("total : 0", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task UI_RSA_07_PartialDoesNotReplaceCurrentAndDoesNotTouchMediaOrSqlite()
    {
        // Budget belongs to the backend. A second file makes the scan partial,
        // not a false complete or a deletion; there are no media/storage calls here.
        var backend = new RsAFixtureRomSession(new RsABudget(MaxFiles: 1));
        using var vm = Model(backend);
        Assert.IsTrue(vm.SelectFixture(_roms, _rootId));
        vm.ConsentConfirmed = true;
        await vm.StartConfirmedAsync();
        var complete = vm.CurrentComplete;
        Assert.IsNotNull(complete);
        File.WriteAllText(Path.Combine(_roms, "gba", "Second.gba"), "FICTION_ONLY");
        vm.ConsentConfirmed = true;
        await vm.StartConfirmedAsync();
        Assert.AreEqual(RsAUiPhase.Partial, vm.Phase);
        Assert.AreEqual(InventoryScanStatus.Partial, vm.LastAttempt?.ScanStatus);
        Assert.AreSame(complete, vm.CurrentComplete);
        Assert.IsFalse(Directory.Exists(Path.Combine(_base, "MEDIAS")));
        Assert.IsFalse(Directory.Exists(Path.Combine(_base, "ROMOTHEQUE_MANAGER")));
    }

    [TestMethod]
    public void UI_RSA_08_StandardPublicCompositionHasNoRsAPermission()
    {
        using var inventory = new InventoryViewModel(new InventoryApplicationService(),
            action => action());
        using var publicVm = new ObservedLibraryViewModel(inventory, fixtureMode: false);
        Assert.IsFalse(publicVm.HasRsA);
        Assert.IsFalse(publicVm.CanInventoryFixture);
        Assert.IsNull(publicVm.RsA);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());
    }
}
