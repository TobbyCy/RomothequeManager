using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

/// <summary>
/// Causal regression for #147 P1: a cancelled ES-DE refresh cannot change the
/// media-opening authority of the previously accepted session.
/// All ROM and media files are temporary, non-executable synthetic fixtures.
/// </summary>
[TestClass]
public sealed class EsDeRefreshAtomicityCausalTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-mvvm145-causal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "gba"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "metadata"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "covers"));
        for (var i = 0; i < 52; i++)
            File.WriteAllText(Path.Combine(_root, "ROMS", "gba", $"Game{i:D3}.gba"),
                "SYNTHETIC NONEXECUTABLE ROM PLACEHOLDER", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "MEDIAS", "gba", "covers", "A.png"),
            "SYNTHETIC A MEDIA REFERENCE ONLY; NO BITMAP DECODING", Encoding.UTF8);
        File.WriteAllText(Path.Combine(_root, "MEDIAS", "gba", "covers", "B.png"),
            "SYNTHETIC B MEDIA REFERENCE ONLY; NO BITMAP DECODING", Encoding.UTF8);
        WriteGamelist("A.png");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private void WriteGamelist(string cover)
    {
        var xml = new StringBuilder("<gameList>");
        for (var i = 0; i < 52; i++)
        {
            xml.Append("<game><path>./Game").Append(i.ToString("D3")).Append(".gba</path>")
                .Append("<name>Title ").Append(i.ToString("D3")).Append("</name>");
            if (i == 0)
                xml.Append("<image>../covers/").Append(cover).Append("</image>");
            xml.Append("</game>");
        }
        xml.Append("</gameList>");
        File.WriteAllText(Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"),
            xml.ToString(), new UTF8Encoding(false));
    }

    /// <summary>
    /// RED baseline is the real importer mutating Session.Current to B before
    /// VM observes cancellation. GREEN requires an isolated candidate/commit.
    /// The synchronization barrier is after real ES-DE parsing, before VM adoption.
    /// </summary>
    [TestMethod]
    public async Task ESDE_REFRESH_CANCEL_ATOMIC_A_B_RealComponents_And_MediaAuthority()
    {
        var service = new BarrierAfterActualRefreshService();
        var state = new ImportedCatalogueUiState(service);
        using var vm = new ImportedCatalogueViewModel(state, service);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));

        var acceptedA = state.Snapshot!;
        var session = state.Session!;
        Assert.AreSame(acceptedA, session.Current, "A must be the accepted session Current");
        var coverA = acceptedA.Games[0].Media.Single(x => x.Kind == "image");
        Assert.IsTrue(state.TryOpenMedia(coverA, out var initialA));
        using (initialA) Assert.IsFalse(initialA!.CanWrite);

        // Select an unaffected game from page two; its selection/page must survive.
        vm.Navigate(POCView.Collection);
        Assert.AreEqual(50, state.PageGames.Count);
        Assert.IsTrue(vm.NextPage());
        var selection = state.PageGames[0].GameId;
        Assert.IsTrue(vm.Open(selection));
        Assert.AreEqual(1, state.PageIndex);
        var libraryId = acceptedA.LibraryId;

        // B is a different XML projection of the SAME explicit root and SAME identity.
        // Keep A media physically on disk to test membership, not file disappearance.
        WriteGamelist("B.png");
        var job = vm.RefreshAsync();
        try
        {
            await service.AfterBackendRefresh.Task.WaitAsync(TimeSpan.FromSeconds(45));
            Assert.IsTrue(vm.IsBusy, "Refresh must await the after-parse barrier");
            vm.Cancel();
        }
        finally
        {
            service.Release();
        }
        Assert.IsFalse(await job, "The cancelled attempt must not be adopted");

        var coverB = service.LastRefreshedSnapshot!.Games[0].Media.Single(x => x.Kind == "image");
        var canOpenA = state.TryOpenMedia(coverA, out var aAfter);
        aAfter?.Dispose();
        var canOpenB = state.TryOpenMedia(coverB, out var bAfter);
        bAfter?.Dispose();
        Console.WriteLine($"ESDE_CAUSAL_A_B_CANCEL stateA={ReferenceEquals(state.Snapshot, acceptedA)} " +
            $"sessionA={ReferenceEquals(session.Current, acceptedA)} " +
            $"sessionContainsB={session.Current!.Games[0].Media.Any(m => m.RelativePath == coverB.RelativePath)} " +
            $"openA={canOpenA} openB={canOpenB} status={vm.ImportStatus}");

        Assert.AreSame(acceptedA, state.Snapshot, "Cancelled refresh must preserve UI A");
        Assert.AreSame(acceptedA, session.Current,
            "P1_ESDE_REFRESH_CANCEL_MUTATES_ACCEPTED_SESSION: session/media authority must remain A");
        Assert.IsTrue(canOpenA, "A must remain authorized and readable after Cancel");
        Assert.IsFalse(canOpenB, "B cannot become authorized without an explicit commit");
        Assert.AreEqual(libraryId, state.Snapshot!.LibraryId);
        Assert.AreEqual(selection, state.SelectedGameId);
        Assert.AreEqual(1, state.PageIndex);
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);

        // Recovery oracle: the next non-cancelled refresh may atomically accept B.
        Assert.IsTrue(await vm.RefreshAsync());
        var acceptedB = state.Snapshot!;
        Assert.AreSame(acceptedB, session.Current, "Committed B must share media authority");
        Assert.AreEqual(libraryId, acceptedB.LibraryId, "Preserve ES-DE session identity");
        Assert.AreEqual(selection, state.SelectedGameId);
        Assert.AreEqual(1, state.PageIndex);
        var newB = acceptedB.Games[0].Media.Single(x => x.Kind == "image");
        Assert.IsTrue(state.TryOpenMedia(newB, out var approvedB));
        using (approvedB) Assert.IsFalse(approvedB!.CanWrite);
        Assert.IsFalse(state.TryOpenMedia(coverA, out var staleA));
        staleA?.Dispose();
    }

    /// <summary>Dispose rejects a prepared B even if parsing has completed.</summary>
    [TestMethod]
    public async Task ESDE_REFRESH_DISPOSE_RealComponents_RejectsBWithoutLateNotifications()
    {
        var service = new BarrierAfterActualRefreshService();
        var state = new ImportedCatalogueUiState(service);
        var vm = new ImportedCatalogueViewModel(state, service);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        var acceptedA = state.Snapshot!;
        var session = state.Session!;
        var coverA = acceptedA.Games[0].Media.Single(x => x.Kind == "image");
        WriteGamelist("B.png");

        var notifications = 0;
        vm.PropertyChanged += (_, _) => notifications++;
        var job = vm.RefreshAsync();
        try
        {
            await service.AfterBackendRefresh.Task.WaitAsync(TimeSpan.FromSeconds(45));
            var snapshotB = service.LastRefreshedSnapshot!;
            Assert.AreEqual(acceptedA.LibraryId, snapshotB.LibraryId);
            vm.Dispose();
            var notificationsAfterDispose = notifications;
            service.Release();
            Assert.IsFalse(await job);
            Assert.AreEqual(notificationsAfterDispose, notifications,
                "No ViewModel callback after Dispose");
            Assert.AreSame(acceptedA, state.Snapshot);
            Assert.AreSame(acceptedA, session.Current);
            Assert.IsTrue(state.TryOpenMedia(coverA, out var a));
            using (a) Assert.IsFalse(a!.CanWrite);
            var coverB = snapshotB.Games[0].Media.Single(x => x.Kind == "image");
            Assert.IsFalse(state.TryOpenMedia(coverB, out var b));
            b?.Dispose();
        }
        finally
        {
            service.Release();
            vm.Dispose();
        }
    }

    /// <summary>A failed real XML refresh cannot change the accepted media authority.</summary>
    [TestMethod]
    public async Task ESDE_REFRESH_ERROR_RealParser_PreservesAAndRedactsFailure()
    {
        var state = new ImportedCatalogueUiState();
        using var vm = new ImportedCatalogueViewModel(state);
        Assert.IsTrue(await vm.ImportConfirmedAsync(_root, explicitlyConfirmed: true));
        var acceptedA = state.Snapshot!;
        var session = state.Session!;
        var coverA = acceptedA.Games[0].Media.Single(x => x.Kind == "image");
        var xmlFile = Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        File.WriteAllText(xmlFile, "<gameList><game><path>broken", new UTF8Encoding(false));

        Assert.IsFalse(await vm.RefreshAsync());
        Assert.AreEqual("Failed", vm.ImportStatus);
        Assert.AreSame(acceptedA, state.Snapshot);
        Assert.AreSame(acceptedA, session.Current);
        Assert.IsFalse(state.ErrorMessage!.Contains(_root, StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(state.TryOpenMedia(coverA, out var stream));
        using (stream) Assert.IsFalse(stream!.CanWrite);
        WriteGamelist("B.png");
        Assert.IsTrue(await vm.RefreshAsync(), "A valid subsequent B may be committed");
        Assert.AreSame(state.Snapshot, session.Current);
        Assert.AreEqual(acceptedA.LibraryId, state.Snapshot!.LibraryId);
    }

    /// <summary>Backend proposals are bound to their owner and single accepted revision.</summary>
    [TestMethod]
    public void ESDE_REFRESH_GUARDS_RejectForeignStaleAndDoubleCommit()
    {
        var first = new EsDeImportSession(_root);
        var initialA = first.Import();
        var other = new EsDeImportSession(_root);
        var otherA = other.Import();

        WriteGamelist("B.png");
        var proposalB = first.PrepareRefresh();
        Assert.AreSame(initialA, first.Current, "Preparation must not mutate Current");
        Assert.AreEqual(initialA.LibraryId, proposalB.Snapshot.LibraryId);
        Assert.IsFalse(other.TryCommitPreparedRefresh(proposalB), "Foreign owner rejected");
        Assert.AreSame(otherA, other.Current);

        var adoptedByExplicitImport = first.Import();
        Assert.IsFalse(first.TryCommitPreparedRefresh(proposalB), "Stale base rejected");
        Assert.AreSame(adoptedByExplicitImport, first.Current);

        var valid = first.PrepareRefresh();
        Assert.IsTrue(first.TryCommitPreparedRefresh(valid));
        Assert.AreSame(valid.Snapshot, first.Current);
        Assert.IsFalse(first.TryCommitPreparedRefresh(valid), "Replay rejected");
        Assert.AreSame(valid.Snapshot, first.Current);
    }

    private sealed class BarrierAfterActualRefreshService : IEsDeImportApplicationService
    {
        private readonly EsDeImportApplicationService _real = new();
        private readonly TaskCompletionSource<bool> _release =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public EsDeCatalogueSnapshot? LastRefreshedSnapshot { get; private set; }

        public TaskCompletionSource<bool> AfterBackendRefresh { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<EsDeImportSession> PrepareAsync(string root, CancellationToken token) =>
            _real.PrepareAsync(root, token);

        public async Task<EsDeImportSession.PreparedEsDeRefresh> RefreshAsync(
            EsDeImportSession session, CancellationToken token)
        {
            // Genuine backend parsing; candidate B exists but session.Current stays A.
            var proposal = await _real.RefreshAsync(session, token);
            LastRefreshedSnapshot = proposal.Snapshot;
            AfterBackendRefresh.TrySetResult(true);
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(45));
            return proposal;
        }

        public void Release() => _release.TrySetResult(true);
    }
}
