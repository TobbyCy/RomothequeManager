using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace RomothequeManager.V2;

public sealed partial class MainWindow : Window
{
    private readonly NavigationState _state = new(ReadOnlyCatalogue.Synthetic());
    private readonly LibraryProjection _libraryProjection;
    private readonly LibraryViewState _libraryViewState;
    private readonly StackPanel _page = new() { Spacing = 14 };
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#E9EEF6"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#A9B8CD"));
    private static readonly IBrush Card = new SolidColorBrush(Color.Parse("#202B3D"));

    public POCView CurrentView => ActiveView;

    // Exercises the three real Avalonia visual trees after the window opens.
    // The normal UI still uses actual buttons/textbox for human interaction.
    public bool SmokeExerciseFourViews()
    {
        if (_state.CurrentView != POCView.Dashboard || _page.Children.Count < 3) return false;
        _state.ShowCollection();
        Render();
        if (_state.CurrentView != POCView.Collection || _page.Children.Count < 4) return false;
        if (!_state.Open("syn-003")) return false;
        Render();
        if (_state.CurrentView != POCView.GameDetail || _page.Children.Count < 3) return false;
        _state.ShowLibraries();
        Render();
        if (_state.CurrentView != POCView.Libraries || _page.Children.Count < 2 ||
            !_libraryViewState.TryCompare()) return false;
        Render();
        _state.ShowDashboard();
        Render();
        return _state.CurrentView == POCView.Dashboard && _page.Children.Count >= 3;
    }

    public MainWindow()
    {
        _libraryProjection = LibraryProjection.LoadSynthetic(_state.Catalogue);
        _libraryViewState = new LibraryViewState(_libraryProjection);
        SizeChanged += (_, _) => { if (ActiveView == POCView.Libraries) Render(); };
        Title = "ROMothèque Manager — POC synthétique";
        Width = 1120; Height = 720; MinWidth = 760; MinHeight = 480;
        Background = new SolidColorBrush(Color.Parse("#121B2A"));
        var root = new Grid { ColumnDefinitions = new ColumnDefinitions("220,*") };
        var menu = new StackPanel { Spacing = 18, Margin = new Thickness(20) };
        menu.Children.Add(Text("ROMOTHÈQUE", 22, true));
        menu.Children.Add(Text("MANAGER · POC V2", 12));
        menu.Children.Add(Button("Tableau de bord", () => Navigate(POCView.Dashboard)));
        menu.Children.Add(Button("Collection", () => Navigate(POCView.Collection)));
        menu.Children.Add(Button("Bibliothèques & snapshots", () => Navigate(POCView.Libraries)));
        menu.Children.Add(Text("LECTURE SEULE", 13, true));
        menu.Children.Add(Button("Choisir une bibliothèque ES-DE…", () => _ = ChooseLibraryAsync()));
        menu.Children.Add(Button("Actualiser l'import", () => _ = RefreshImportAsync()));
        menu.Children.Add(Button("Voir l'import", ShowImported));
        menu.Children.Add(Button("Mode démonstration", ShowDemo));
        menu.Children.Add(Text("Mode démo au démarrage ; l'import requiert votre sélection.", 12));
        var sidebar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#192439")), Child = menu
        };
        root.Children.Add(sidebar);
        var scroll = new ScrollViewer
        {
            Content = _page, Margin = new Thickness(30),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetColumn(scroll, 1);
        root.Children.Add(scroll);
        Content = root;
        Render();
    }

    private static TextBlock Text(string value, double size = 16, bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = bold ? Ink : Muted,
        TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal
    };

    private static Button Button(string label, Action handler)
    {
        var b = new Button
        {
            Content = label, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Padding = new Thickness(14, 10), MinHeight = 40
        };
        b.Click += (_, _) => handler();
        return b;
    }

    private static Border Panel(Control content) => new()
    {
        Background = Card, CornerRadius = new CornerRadius(12),
        Padding = new Thickness(20), Child = content
    };

    private static StackPanel Group(params Control[] controls)
    {
        var group = new StackPanel { Spacing = 8 };
        foreach (var c in controls) group.Children.Add(c);
        return group;
    }

    private void Render()
    {
        _page.Children.Clear();
        ReleaseCoverImages();
        _importedList = null;
        Title = _importedUi.IsImported ? "ROMothèque Manager — Bibliothèque importée" :
            "ROMothèque Manager — POC synthétique";
        if (_importedUi.ErrorMessage is not null)
            _page.Children.Add(Text(_importedUi.ErrorMessage, 14, true));
        if (_importBusy) _page.Children.Add(Text("Lecture en cours ; aucune source modifiée.", 14, true));
        if (_importedUi.IsImported)
        {
            RenderImported();
            return;
        }
        switch (_state.CurrentView)
        {
            case POCView.Dashboard: Dashboard(); break;
            case POCView.Collection: Collection(); break;
            case POCView.GameDetail: Detail(); break;
            case POCView.Libraries: Libraries(); break;
        }
    }

    private void Dashboard()
    {
        _page.Children.Add(Text("Tableau de bord", 30, true));
        _page.Children.Add(Text("Un premier regard sur une collection fictive, sans aucun accès à vos ROM.", 15));
        _page.Children.Add(Panel(Group(Text("6 jeux inventés", 25, true),
            Text("3 bibliothèques fictives · 1 ONLINE · 1 OFFLINE · 1 UNKNOWN"),
            Text("Dernière observation simulée : 22.10.2026 à 12:15 UTC"),
            Text("OFFLINE ne signifie jamais que les jeux ont été supprimés.", 14, true))));
        _page.Children.Add(Button("Parcourir la collection", () => { _state.ShowCollection(); Render(); }));
    }

    private void Collection()
    {
        _page.Children.Add(Text("Collection", 30, true));
        _page.Children.Add(Text("Résultats observés dans les fixtures. Ce total ne prouve pas une bibliothèque complète."));
        var search = new TextBox { PlaceholderText = "Rechercher un jeu ou une plateforme", Text = _state.SearchText };
        search.TextChanged += (_, _) => { _state.Search(search.Text); FillResults(); };
        _page.Children.Add(search);
        _list = new StackPanel { Spacing = 10 };
        _page.Children.Add(_list);
        FillResults();
    }

    private StackPanel? _list;
    private void FillResults()
    {
        if (_list is null) return;
        _list.Children.Clear();
        var games = _state.FilteredGames;
        _list.Children.Add(Text(games.Count + " résultat(s) synthétique(s) — couverture globale non garantie.", 13));
        foreach (var game in games)
        {
            var id = game.GameId;
            _list.Children.Add(Panel(Group(
                Text(game.Title, 19, true),
                Text(game.Platform + " · " + game.Variant),
                Text(game.Availability + " / " + game.Freshness + " / " + game.Coverage, 13),
                Button("Ouvrir la fiche", () => { if (_state.Open(id)) Render(); })
            )));
        }
    }

    private void Libraries()
    {
        _page.Children.Add(Text("Bibliothèques & snapshots", 30, true));
        _page.Children.Add(LibraryPage.Build(_libraryProjection, _libraryViewState,
            Render, Bounds.Width < 1000));
    }

    private void Detail()
    {
        var game = _state.Selected;
        if (game is null) { _state.ShowCollection(); Render(); return; }
        _page.Children.Add(Button("← Retour à la collection", () => { _state.ShowCollection(); Render(); }));
        _page.Children.Add(Text(game.Title, 30, true));
        _page.Children.Add(Panel(Group(
            Text("Plateforme : " + game.Platform, 18, true),
            Text("Variante : " + game.Variant),
            Text("Identifiant fictif : " + game.GameId),
            Text("Bibliothèque : " + game.Library),
            Text("Identité logique : " + game.LibraryId),
            Text("Disponibilité actuelle : " + game.Availability, 16, true),
            Text("Fraîcheur de la dernière observation : " + game.Freshness),
            Text("Couverture : " + game.Coverage),
            Text("Snapshot fictif : " + game.SnapshotId),
            Text("Observé le : " + game.ObservedAt.ToString("yyyy-MM-dd HH:mm 'UTC'")),
            Text("Provenance : " + game.Evidence),
            Text("La disponibilité actuelle et l'observation historique sont deux états différents.", 14, true)
        )));
    }
}
