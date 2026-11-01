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
            $"sessionB={coverB.RelativePath.Contains("B.png", StringComparison.Ordinal)} " +
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

        public async Task<EsDeCatalogueSnapshot> RefreshAsync(
            EsDeImportSession session, CancellationToken token)
        {
            // Real backend, not a fake DTO. Before the fix, this mutates Current.
            var result = await _real.RefreshAsync(session, token);
            LastRefreshedSnapshot = result;
            AfterBackendRefresh.TrySetResult(true);
            await _release.Task.WaitAsync(TimeSpan.FromSeconds(45));
            return result;
        }

        public void Release() => _release.TrySetResult(true);
    }
}
