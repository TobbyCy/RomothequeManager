#if NEXT05_CI
namespace RomothequeManager.V2;

// #141: only present in the runner's explicitly compiled synthetic Windows EXE.
// The release build has no entrypoint to this fixture.
public sealed partial class MainWindow
{
    internal bool CiLegacyImport(string fixture, int expectedGames)
    {
        if (!_importedUi.TryImport(fixture) ||
            _importedUi.Snapshot?.Games.Count != expectedGames)
            return false;
        _importedUi.Navigate(POCView.Collection);
        Render();
        return _importedUi.TotalCount == expectedGames;
    }

    internal bool CiLegacyOpenFirst()
    {
        var first = _importedUi.PageGames.FirstOrDefault();
        if (first is null) return false;
        OpenImportedGame(first.GameId);
        return _importedUi.CurrentView == POCView.GameDetail &&
            _importedUi.SelectedGameId == first.GameId;
    }

    internal bool CiLegacyShowCollection()
    {
        _importedUi.Navigate(POCView.Collection);
        Render();
        return _importedUi.CurrentView == POCView.Collection;
    }

    internal bool CiLegacyShowDashboard()
    {
        _importedUi.Navigate(POCView.Dashboard);
        Render();
        return _importedUi.CurrentView == POCView.Dashboard;
    }

    internal bool CiLegacySearch(string search)
    {
        _importedUi.Search(search);
        Render();
        return _importedUi.SearchText == search;
    }

    internal bool CiLegacyReimport()
    {
        if (!_importedUi.TryReimport()) return false;
        Render();
        return _importedUi.IsImported;
    }

    internal string CiLegacyTexts() => SmokeVisibleTexts();
    internal int CiLegacyLiveBitmapCount => _coverImages.Count;
}
#endif
