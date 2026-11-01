using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

/// <summary>ARCH-MVVM-01: generated fixtures only, no ROM content or real media grants.</summary>
[TestClass]
public sealed class ImportedCatalogueMvvmBoundaryTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-mvvm145-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "gba"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "metadata"));
        WriteFixture(55);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void WriteFixture(int count)
    {
        var xml = new StringBuilder("<gameList>");
        for (var i = 0; i < count; i++)
        {
            var name = "Game" + i.ToString("D3");
            File.WriteAllText(Path.Combine(_root, "ROMS", "gba", name + ".gba"),
                "SYNTHETIC NONEXECUTABLE FIXTURE", Encoding.UTF8);
            xml.Append("<game><path>./").Append(name).Append(".gba</path><name>Title ")
                .Append(i.ToString("D3")).Append("</name></game>");
        }
        xml.Append("</gameList>");
        File.WriteAllText(Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"),
            xml.ToString(), new UTF8Encoding(false));
    }

    [TestMethod]
    public async Task MVVM_01_NoImplicitImportWithoutExplicitConsent()
    {
        var fake = new ControlledImportService();
        var state = new ImportedCatalogueUiState(fake);
        using var vm = new ImportedCatalogueViewModel(state, fake);
        Assert.IsFalse(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: false));
        Assert.IsFalse(vm.ImportCommand.CanExecute(
            new ConfirmedEsDeImportRequest(_root, ExplicitUserConsent: false)));
        Assert.IsTrue(vm.ImportCommand.CanExecute(
            new ConfirmedEsDeImportRequest(_root, ExplicitUserConsent: true)));
        Assert.AreEqual(0, fake.PrepareCalls, "MVVM01_NO_PARSE_BEFORE_NATIVE_CONSENT");
        Assert.IsNull(vm.Current);
        Assert.AreEqual(CatalogueMode.NoCatalog, state.Mode);
        Assert.IsFalse(vm.CanRefresh);
    }

    [TestMethod]
    public async Task MVVM_01_ExplicitImportUsesServiceAndNotifiesCurrent()
    {
        var fake = new ControlledImportService();
        var state = new ImportedCatalogueUiState(fake);
        using var vm = new ImportedCatalogueViewModel(state, fake);
        var names = new List<string?>();
        vm.PropertyChanged += (_, args) => names.Add(args.PropertyName);

        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        Assert.AreEqual(1, fake.PrepareCalls);
        Assert.AreEqual(55, vm.Current!.Games.Count);
        Assert.AreEqual("Complete", vm.ImportStatus);
        Assert.IsFalse(vm.IsBusy);
        Assert.IsTrue(vm.CanRefresh);
        CollectionAssert.Contains(names, nameof(ImportedCatalogueViewModel.Current));
        CollectionAssert.Contains(names, nameof(ImportedCatalogueViewModel.IsBusy));
        CollectionAssert.Contains(names, nameof(ImportedCatalogueViewModel.StateRevision));
        Assert.IsTrue(vm.RefreshCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task MVVM_02_FilterPagingSelectionAndRefreshPreserveIdentity()
    {
        var state = new ImportedCatalogueUiState();
        using var vm = new ImportedCatalogueViewModel(state);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        var accepted = vm.Current!;
        vm.Navigate(POCView.Collection);
        Assert.AreEqual(50, state.PageGames.Count);
        Assert.IsTrue(vm.NextPage());
        Assert.AreEqual(1, state.PageIndex);
        var lastPageGame = state.PageGames[0];
        Assert.IsTrue(vm.Open(lastPageGame.GameId));
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);

        Assert.IsTrue(await vm.RefreshAsync());
        Assert.AreEqual(accepted.LibraryId, vm.Current!.LibraryId);
        Assert.AreEqual(lastPageGame.GameId, state.SelectedGameId);
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Assert.AreEqual(1, state.PageIndex);
        vm.Navigate(POCView.Collection);
        vm.Search("Title 00");
        Assert.AreEqual(0, state.PageIndex);
        Assert.AreEqual(10, state.TotalCount);
        Assert.IsFalse(vm.NextPage());
        Assert.AreEqual(10, state.PageGames.Count);
    }

    [TestMethod]
    public async Task MVVM_01_CancelRefreshNeverAdoptsALateResult()
    {
        var fake = new ControlledImportService();
        var state = new ImportedCatalogueUiState(fake);
        using var vm = new ImportedCatalogueViewModel(state, fake);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        var accepted = vm.Current!;
        var pendingRefresh = new TaskCompletionSource<EsDeCatalogueSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        fake.OnRefresh = (_, _) => pendingRefresh.Task;

        var pending = vm.RefreshAsync();
        Assert.IsTrue(vm.IsBusy);
        Assert.IsFalse(vm.RefreshCommand.CanExecute(null));
        Assert.IsTrue(vm.CancelCommand.CanExecute(null));
        vm.CancelCommand.Execute(null);
        Assert.AreEqual("CancellationRequested", vm.ImportStatus);
        pendingRefresh.SetResult(accepted with { ContentFingerprint = "late-unadopted" });
        Assert.IsFalse(await pending);
        Assert.AreSame(accepted, vm.Current, "MVVM01_CANCEL_CURRENT_UNCHANGED");
        Assert.AreEqual("Cancelled", vm.ImportStatus);
        Assert.IsFalse(vm.IsBusy);
    }

    [TestMethod]
    public async Task MVVM_01_DisposeRejectsLateImportWithoutNotifications()
    {
        var staged = new EsDeImportSession(_root);
        staged.Import(); // fabricated, disposable test corpus only
        var deferred = new TaskCompletionSource<EsDeImportSession>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var fake = new ControlledImportService
        {
            OnPrepare = (_, _) => deferred.Task
        };
        var state = new ImportedCatalogueUiState(fake);
        var vm = new ImportedCatalogueViewModel(state, fake);
        var notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        var active = vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true);
        Assert.IsTrue(vm.IsBusy);
        vm.Dispose();
        var before = notifications;
        deferred.SetResult(staged);
        Assert.IsFalse(await active);
        Assert.AreEqual(before, notifications, "MVVM01_DISPOSE_NO_LATE_NOTIFICATIONS");
        Assert.IsNull(vm.Current);
        Assert.IsFalse(vm.CanImport);
    }

    [TestMethod]
    public async Task MVVM_01_FailedRefreshRedactsErrorsAndPreservesCurrent()
    {
        var fake = new ControlledImportService();
        var state = new ImportedCatalogueUiState(fake);
        using var vm = new ImportedCatalogueViewModel(state, fake);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        var accepted = vm.Current;
        vm.Navigate(POCView.Collection);
        vm.NextPage();
        var page = state.PageIndex;
        fake.OnRefresh = (_, _) => Task.FromException<EsDeCatalogueSnapshot>(
            new IOException("PRIVATE_MARKER_PATH_CANNOT_LEAK"));
        Assert.IsFalse(await vm.RefreshAsync());
        Assert.AreSame(accepted, vm.Current);
        Assert.AreEqual(page, state.PageIndex);
        Assert.AreEqual("Failed", vm.ImportStatus);
        Assert.IsFalse(state.ErrorMessage!.Contains("PRIVATE_MARKER_PATH_CANNOT_LEAK",
            StringComparison.Ordinal), "MVVM01_NO_PRIVATE_PATH_LEAK");
    }

    [TestMethod]
    public async Task MVVM_02_RefreshFailureAndWelcomeKeepAcceptedSnapshot()
    {
        var state = new ImportedCatalogueUiState();
        using var vm = new ImportedCatalogueViewModel(state);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        var before = vm.Current;
        vm.ShowWelcome();
        Assert.AreSame(before, vm.Current);
        Assert.AreEqual(CatalogueMode.NoCatalog, state.Mode);
        Assert.IsTrue(vm.ShowImported());
        Assert.AreSame(before, vm.Current);
        Assert.AreEqual(POCView.Dashboard, state.CurrentView);
    }

    private sealed class ControlledImportService : IEsDeImportApplicationService
    {
        private readonly EsDeImportApplicationService _real = new();
        public int PrepareCalls { get; private set; }
        public Func<string, CancellationToken, Task<EsDeImportSession>>? OnPrepare { get; set; }
        public Func<EsDeImportSession, CancellationToken, Task<EsDeCatalogueSnapshot>>? OnRefresh { get; set; }

        public Task<EsDeImportSession> PrepareAsync(string root, CancellationToken token)
        {
            PrepareCalls++;
            return OnPrepare is null ? _real.PrepareAsync(root, token) : OnPrepare(root, token);
        }

        public Task<EsDeCatalogueSnapshot> RefreshAsync(EsDeImportSession session, CancellationToken token) =>
            OnRefresh is null ? _real.RefreshAsync(session, token) : OnRefresh(session, token);
    }
}
