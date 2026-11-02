#if NEXT05_CI
using Avalonia;
using Avalonia.Controls;

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

    internal bool CiLegacyOpenFirst() => CiLegacyOpenIndex(0);

    internal bool CiLegacyOpenIndex(int index)
    {
        var selected = _importedUi.PageGames.Skip(index).FirstOrDefault();
        if (selected is null) return false;
        OpenImportedGame(selected.GameId);
        return _importedUi.CurrentView == POCView.GameDetail &&
            _importedUi.SelectedGameId == selected.GameId;
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


    // B05 P1: reads the real attached visual tree; no artificial viewport or
    // content-width assignment that could hide a production resize failure.
    internal (bool Fits, string Geometry) CiLegacyCollectionNarrowGeometry()
    {
        if (!_importedUi.IsImported || ActiveView != POCView.Collection ||
            _contentScroll is null || _importedList is null ||
            _importedUi.PageGames.Count != 50 ||
            _page.Children.OfType<Avalonia.Controls.Border>().FirstOrDefault() is not { } hero ||
            _liveImportedSystemSelector is null || _liveImportedSearchBox is null)
            return (false, "B05_COLLECTION_50_OR_CONTROLS_NOT_READY");

        var notice = _importedList.Children.OfType<Avalonia.Controls.Border>().FirstOrDefault();
        var cardGrid = _importedList.Children.OfType<Avalonia.Controls.Grid>().FirstOrDefault();
        var firstCard = cardGrid?.Children.OfType<Avalonia.Controls.Border>().FirstOrDefault();
        var scrollOrigin = _contentScroll.TranslatePoint(new Avalonia.Point(0, 0), this);
        if (scrollOrigin is null || notice is null || firstCard is null ||
            hero.Child is not Avalonia.Controls.StackPanel heroCopy)
            return (false, "B05_VISUAL_LAYOUT_NOT_ATTACHED");

        var title = heroCopy.Children.OfType<Avalonia.Controls.TextBlock>()
            .FirstOrDefault(t => t.Text == "Collection");
        if (title is null) return (false, "B05_COLLECTION_TITLE_MISSING");
        var left = scrollOrigin.Value.X;
        var right = left + _contentScroll.Bounds.Width;
        var nodes = new (string Name, Avalonia.Controls.Control Control)[]
        {
            ("title", title),
            ("filters", _liveImportedSystemSelector),
            ("search", _liveImportedSearchBox),
            ("counter", notice),
            ("card", firstCard)
        };
        var details = new System.Text.StringBuilder();
        var fits = Bounds.Width >= 758 && Bounds.Width <= 766 &&
            _page.Bounds.Width > 0 &&
            _page.Bounds.Width <= _contentScroll.Bounds.Width &&
            _importedUi.TotalCount == 50;
        foreach (var (name, control) in nodes)
        {
            var translated = control.TranslatePoint(new Avalonia.Point(0, 0), this);
            var x = translated?.X ?? double.NaN;
            var end = x + control.Bounds.Width;
            details.Append(name).Append('=')
                .Append(x.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Append("..")
                .Append(end.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
                .Append(';');
            if (double.IsNaN(x) || x < left - 3 || end > right + 3 ||
                control.Bounds.Width <= 0)
                fits = false;
        }
        details.Append("scroll=").Append(left.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
            .Append("..").Append(right.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
            .Append(" pageWidth=")
            .Append(_page.Bounds.Width.ToString("F1", System.Globalization.CultureInfo.InvariantCulture))
            .Append(" windowWidth=")
            .Append(Bounds.Width.ToString("F1", System.Globalization.CultureInfo.InvariantCulture));
        return (fits, details.ToString());
    }

    // Checks actual keyboard-focus targets after native resize. This is not a
    // simulated human Tab/Shift+Tab journey or a #95 acceptance certificate.
    internal bool CiLegacyCollectionKeyboardFocus()
    {
        if (_liveImportedSystemSelector is not { IsVisible: true, IsEnabled: true } selector ||
            _liveImportedSearchBox is not { IsVisible: true, IsEnabled: true } search ||
            _importedList is null || _importedUi.PageGames.Count != 50)
            return false;
        var grid = _importedList.Children.OfType<Avalonia.Controls.Grid>().FirstOrDefault();
        var card = grid?.Children.OfType<Avalonia.Controls.Border>().FirstOrDefault();
        var line = card?.Child as Avalonia.Controls.Grid;
        var details = line?.Children.OfType<Avalonia.Controls.StackPanel>().FirstOrDefault();
        var action = details?.Children.OfType<Avalonia.Controls.Button>().FirstOrDefault();
        return selector.Focus() && selector.IsFocused &&
            search.Focus() && search.IsFocused &&
            action is not null && action.Focus() && action.IsFocused;
    }

    internal string CiLegacyTexts() => SmokeVisibleTexts();
    internal int CiLegacyLiveBitmapCount => _coverImages.Count;
}
#endif
