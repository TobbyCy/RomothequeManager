using RomothequeManager.V2.MvvmFoundation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace RomothequeManager.V2;

public sealed partial class MainWindow : Window
{
    // Never construct the built-in demo catalogue on a normal, empty launch.
    // The legacy demo and its projection are instantiated only after an explicit choice.
    private readonly Lazy<NavigationState> _demoNavigation = new(() => new NavigationState(ReadOnlyCatalogue.Synthetic()));
    private readonly Lazy<LibraryProjection> _demoProjection;
    private readonly Lazy<LibraryViewState> _demoLibraries;
    private NavigationState _state => _demoNavigation.Value;
    private LibraryProjection _libraryProjection => _demoProjection.Value;
    private LibraryViewState _libraryViewState => _demoLibraries.Value;
    private POCView _emptyView = POCView.Dashboard;
    private readonly StackPanel _page = new() { Spacing = 16 };
    private ScrollViewer? _contentScroll;
    private Image? _officialSidebarLogo;
    private Button? _refreshImportButton;
    private Button? _viewImportButton;
    private TextBlock? _refreshImportHint;
    private TextBlock? _viewImportHint;
    private Vector _demoCollectionOffset;
    private Vector _importedCollectionOffset;
    private int _importedColumns = 1;

#if NEXT04_CI
    // The headless Windows runner cannot expose a physical ultrawide monitor.
    // Use a CI-only logical viewport for synthetic layout proof, never production.
    private double? _ciViewportWidth;
    internal void CiNext041SetSyntheticViewport(double width)
    {
        _ciViewportWidth = width;
        UpdateContentWidth();
        _importedColumns = ImportedColumnCount();
        Render();
    }

    internal void CiNext041ClearSyntheticViewport()
    {
        _ciViewportWidth = null;
        UpdateContentWidth();
        _importedColumns = ImportedColumnCount();
        Render();
    }
#endif

    private double LayoutViewportWidth()
    {
#if NEXT04_CI
        if (_ciViewportWidth is double synthetic) return synthetic;
#endif
        return Bounds.Width;
    }

    private void UpdateContentWidth()
    {
        // Physical viewport in production, explicit synthetic width only in isolated CI.
        _page.Width = Math.Max(420, Math.Min(1580, LayoutViewportWidth() - 306));
    }

    private int ImportedColumnCount()
    {
        var viewport = LayoutViewportWidth();
        return viewport >= 1900 ? 3 : viewport >= 1150 ? 2 : 1;
    }

    private void RestoreCollectionOffset(bool imported)
    {
        var remembered = imported ? _importedCollectionOffset : _demoCollectionOffset;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_contentScroll is not null && ActiveView == POCView.Collection &&
                _importedUi.IsImported == imported)
                _contentScroll.Offset = remembered;
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OpenDemoGame(string id)
    {
        var offset = _contentScroll?.Offset ?? default;
        if (_state.Open(id))
        {
            _demoCollectionOffset = offset;
            Render();
            if (_contentScroll is not null) _contentScroll.Offset = default;
        }
    }

    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#E8E6E3"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#AAB2BF"));
    private static readonly IBrush Card = new SolidColorBrush(Color.Parse("#23262B"));
    private static readonly IBrush Accent = new SolidColorBrush(Color.Parse("#B3202E"));
    private static readonly IBrush BorderInk = new SolidColorBrush(Color.Parse("#3B414A"));

    // Persistent visual state for sidebar buttons. Explicit foreground wins over
    // the Fluent hover template which previously washed text out.
    private sealed class SidebarVisual
    {
        public required Border Surface { get; init; }
        public required TextBlock Label { get; init; }
        public required string Caption { get; init; }
        public Button? Control { get; set; }
        public POCView? View { get; init; }
        public bool Hovered { get; set; }
    }
    private readonly List<SidebarVisual> _sidebarVisuals = [];

    public POCView CurrentView => ActiveView;

    // Exercises the three real Avalonia visual trees after the window opens.
    // The normal UI still uses actual buttons/textbox for human interaction.
    public bool SmokeExerciseFourViews()
    {
        // --smoke-window is an explicit synthetic test, not normal startup.
        ShowDemo();
        if (_state.CurrentView != POCView.Dashboard || _page.Children.Count < 3) return false;
        _state.ShowCollection();
        Render();
        // The premium Hero consolidates two previous title/description blocks.
        // Assert the actual interactive collection, not a brittle visual-root count.
        if (_state.CurrentView != POCView.Collection || _list is null ||
            !_page.Children.OfType<TextBox>().Any() ||
            !_list.Children.OfType<Border>().Any()) return false;
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
        _importVm = new ImportedCatalogueViewModel(_importedUi);
        _importVm.PropertyChanged += (_, change) =>
        {
            // Track UI scroll before an asynchronous refresh, without owning its job.
            if (change.PropertyName == nameof(ImportedCatalogueViewModel.IsBusy))
            {
                var collection = _importedUi.IsImported && ActiveView == POCView.Collection;
                if (_importVm.IsBusy && collection)
                    _importedCollectionOffset = _contentScroll?.Offset ?? default;
                Render();
                if (!_importVm.IsBusy && collection)
                    RestoreCollectionOffset(true);
            }
        };
        _demoProjection = new Lazy<LibraryProjection>(() => LibraryProjection.LoadSynthetic(_state.Catalogue));
        _demoLibraries = new Lazy<LibraryViewState>(() => new LibraryViewState(_libraryProjection));
        SizeChanged += (_, _) =>
        {
            UpdateContentWidth();
            var columns = ImportedColumnCount();
            if (_importedUi.IsImported && ActiveView == POCView.Collection &&
                columns != _importedColumns)
            {
                _importedColumns = columns;
                Render();
            }
            else if ((_importedUi.Mode == CatalogueMode.Demonstration && _scenarioUi.Active) ||
                     ActiveView is POCView.Dashboard or POCView.Libraries)
                Render();
        };
        Title = "ROMothèque Manager";
        Width = 1120; Height = 720; MinWidth = 760; MinHeight = 480;
        Background = new SolidColorBrush(Color.Parse("#16181D"));

        // Both resources come from the official byte-for-byte approved branding pack.
        // The EXE also embeds the same multiresolution ICO via ApplicationIcon.
        var brandingRoot = AppContext.BaseDirectory;
        var logoPath = Path.Combine(brandingRoot, "RM_V2_Horizontal_Fond_Sombre.png");
        var iconPath = Path.Combine(brandingRoot, "RM_V2_Favicon.ico");
        if (!File.Exists(logoPath) || !File.Exists(iconPath))
            throw new InvalidDataException("Official application branding is missing.");
        Icon = new WindowIcon(iconPath);
        var logoBitmap = new Bitmap(logoPath);
        Closed += (_, _) =>
        {
            _importVm.Dispose(); // cancel outstanding ES-DE import and suppress late callbacks
            StopLegacyMediaOnClose(); // detach and Dispose thumbnails before close
            logoBitmap.Dispose();
        };

        var root = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("264,*"),
            Background = new SolidColorBrush(Color.Parse("#16181D"))
        };
        var menu = new StackPanel { Spacing = 9, Margin = new Thickness(14, 18, 14, 14) };
        var sidebarLogo = new Image
        {
            // Native source: 1960×576 (3.40:1), displayed at proportional 228×67 DIP.
            // No fixed crop or bitmap regeneration; high-DPI uses native pixels.
            Source = logoBitmap, Width = 228, Height = 67,
            Stretch = Stretch.Uniform, HorizontalAlignment = HorizontalAlignment.Center
        };
        // Native PNG, unchanged branding; high-quality interpolation only on logo.
        RenderOptions.SetBitmapInterpolationMode(sidebarLogo, BitmapInterpolationMode.HighQuality);
        _officialSidebarLogo = sidebarLogo;
        menu.Children.Add(new Border
        {
            Background = new SolidColorBrush(Color.Parse("#16181D")),
            Padding = new Thickness(2, 8), CornerRadius = new CornerRadius(9),
            Child = sidebarLogo
        });
        menu.Children.Add(Text("EXPLORER", 11, true));
        menu.Children.Add(SidebarButton("Tableau de bord", () => Navigate(POCView.Dashboard), POCView.Dashboard));
        menu.Children.Add(SidebarButton("Collection", () => Navigate(POCView.Collection), POCView.Collection));
        menu.Children.Add(Text("BIBLIOTHÈQUES", 11, true));
        menu.Children.Add(SidebarButton("Bibliothèques & supports", () => Navigate(POCView.Libraries), POCView.Libraries));
        menu.Children.Add(Text("GESTION", 11, true));
        // Plain informational label, not a control or a faux-disabled action.
        menu.Children.Add(Text("Exports · à venir", 13));
        menu.Children.Add(Text("Fonction non disponible dans cette version.", 11));
        menu.Children.Add(Text("OUTILS · LECTURE SEULE", 11, true));
        menu.Children.Add(SidebarButton("Inventaire de fichiers · sans ES-DE", () =>
            new ObservedLibraryWindow().Show(this)));
        menu.Children.Add(SidebarButton("Choisir une bibliothèque ES-DE…", () => _ = ChooseLibraryAsync()));
        _refreshImportButton = SidebarButton("Actualiser l'import", null);
        _refreshImportButton.Command = _importVm.RefreshCommand;
        menu.Children.Add(_refreshImportButton);
        _refreshImportHint = Text("Disponible après import d'une bibliothèque ES-DE.", 11);
        menu.Children.Add(_refreshImportHint);
        _viewImportButton = SidebarButton("Voir l'import", ShowImported);
        menu.Children.Add(_viewImportButton);
        _viewImportHint = Text("Aucun catalogue importé à afficher.", 11);
        menu.Children.Add(_viewImportHint);
        menu.Children.Add(SidebarButton("Essayer une démonstration fictive", ShowDemo));
        menu.Children.Add(Text("Aucun scan au démarrage. Import sur sélection.", 11));
        var sidebar = new Border
        {
            Background = new SolidColorBrush(Color.Parse("#1D2025")),
            BorderBrush = new SolidColorBrush(Color.Parse("#35383E")),
            BorderThickness = new Thickness(0, 0, 1, 0),
            Child = new ScrollViewer
            {
                Content = menu,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            }
        };
        root.Children.Add(sidebar);
        var scroll = new ScrollViewer
        {
            Content = _page, Margin = new Thickness(16, 14, 16, 12),
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        Grid.SetColumn(scroll, 1);
        root.Children.Add(scroll);
        _contentScroll = scroll;
        _page.HorizontalAlignment = HorizontalAlignment.Center;
        Content = root;
        UpdateContentWidth();
        _importedColumns = ImportedColumnCount();
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
            Padding = new Thickness(14, 10), MinHeight = 40,
            Foreground = Ink,
            Background = new SolidColorBrush(Color.Parse("#2D3036")),
            BorderBrush = new SolidColorBrush(Color.Parse("#44474E")),
            BorderThickness = new Thickness(1)
        };
        b.Click += (_, _) => handler();
        return b;
    }

    // Native button remains focusable/keyboard accessible. Label and surface colors
    // are under our control for inactive, active and pointer-hover states.
    private Button SidebarButton(string title, Action? action, POCView? view = null)
    {
        var label = new TextBlock
        {
            Text = title, FontSize = 13, TextWrapping = TextWrapping.Wrap,
            VerticalAlignment = VerticalAlignment.Center
        };
        var surface = new Border
        {
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 7), MinHeight = 34, Child = label
        };
        var state = new SidebarVisual { Surface = surface, Label = label, Caption = title, View = view };
        _sidebarVisuals.Add(state);
        var button = new Button
        {
            Content = surface, Background = new SolidColorBrush(Color.Parse("#1D2025")),
            BorderThickness = new Thickness(0), Padding = new Thickness(0),
            MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
        state.Control = button;
        button.PointerEntered += (_, _) => { state.Hovered = true; UpdateSidebarStyles(); };
        button.PointerExited += (_, _) => { state.Hovered = false; UpdateSidebarStyles(); };
        if (action is not null) button.Click += (_, _) => action();
        return button;
    }

    private void UpdateSidebarStyles()
    {
        foreach (var state in _sidebarVisuals)
        {
            var enabled = state.Control?.IsEnabled != false;
            bool active = enabled && state.View is POCView view && view == ActiveView;
            var colors = SidebarNavigationPalette.Resolve(active, enabled && state.Hovered, enabled);
            state.Label.Text = enabled ? state.Caption : state.Caption + " · indisponible";
            state.Surface.Background = new SolidColorBrush(Color.Parse(colors.Background));
            state.Surface.BorderBrush = new SolidColorBrush(Color.Parse(colors.Accent));
            state.Label.Foreground = new SolidColorBrush(Color.Parse(colors.Foreground));
            state.Label.FontWeight = active ? FontWeight.SemiBold : FontWeight.Normal;
        }
    }

    // Shared local visual primitives; no external assets or new dependencies.
    // Used by imported and demo views, without changing navigation/data contracts.
    private static Border Panel(Control content) => new()
    {
        Background = Card, BorderBrush = BorderInk, BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12), Padding = new Thickness(20), Child = content
    };

    private static Border Hero(string eyebrow, string title, string subtitle)
    {
        var copy = new StackPanel { Spacing = 7 };
        copy.Children.Add(new TextBlock
        {
            Text = eyebrow.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#F28C92")),
            LetterSpacing = 1.3
        });
        copy.Children.Add(Text(title, 30, true));
        copy.Children.Add(Text(subtitle, 13));
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#22262D")),
            BorderBrush = BorderInk, BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(23, 19),
            Child = copy
        };
    }

    private static Border Notice(string title, string explanation, bool warning = false)
    {
        var body = new StackPanel { Spacing = 4 };
        body.Children.Add(Text(title, 15, true));
        body.Children.Add(Text(explanation, 12));
        return new Border
        {
            Background = new SolidColorBrush(Color.Parse(warning ? "#382B2E" : "#242A32")),
            BorderBrush = warning ? Accent : BorderInk,
            BorderThickness = new Thickness(3, 1, 1, 1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(13, 11),
            Child = body
        };
    }

    private static Button PrimaryButton(string caption, Action action)
    {
        var b = Button(caption, action);
        b.Background = Accent;
        b.BorderBrush = new SolidColorBrush(Color.Parse("#D34655"));
        b.FontWeight = FontWeight.SemiBold;
        return b;
    }

    private static StackPanel Group(params Control[] controls)
    {
        var group = new StackPanel { Spacing = 8 };
        foreach (var c in controls) group.Children.Add(c);
        return group;
    }

    private void Render()
    {
        if (_refreshImportButton is not null) _refreshImportButton.IsEnabled = _importVm.CanRefresh;
        if (_viewImportButton is not null) _viewImportButton.IsEnabled = _importedUi.HasImportedCatalogue && !_importVm.IsBusy;
        if (_refreshImportHint is not null) _refreshImportHint.IsVisible = _refreshImportButton?.IsEnabled != true;
        if (_viewImportHint is not null) _viewImportHint.IsVisible = _viewImportButton?.IsEnabled != true;
        UpdateSidebarStyles();
        // Detaching a ComboBox/TextBox may emit late SelectionChanged/TextChanged.
        // Invalidate the previous controls before clearing the visual tree.
        _liveImportedSystemSelector = null;
        _liveImportedSearchBox = null;
        _page.Children.Clear();
        ReleaseCoverImages();
        _importedList = null;
        Title = "ROMothèque Manager" + (_importedUi.IsImported ?
            " — Bibliothèque ES-DE" : _importedUi.Mode == CatalogueMode.Demonstration ?
            " — Démonstration" : "");
        if (_importedUi.ErrorMessage is not null)
            _page.Children.Add(Notice("Import non effectué", _importedUi.ErrorMessage, warning: true));
        if (_importVm.IsBusy)
        {
            _page.Children.Add(Notice("Import ES-DE en cours",
                "Lecture des métadonnées sélectionnées ; pas de scan global ni d'écriture."));
            _page.Children.Add(new ProgressBar { IsIndeterminate = true, Height = 5 });
            _page.Children.Add(new Button
            {
                Content = "Annuler l'import ES-DE",
                Command = _importVm.CancelCommand,
                MinHeight = 40
            });
        }
        if (_importedUi.IsImported)
        {
            RenderImported();
            return;
        }
        if (_importedUi.Mode == CatalogueMode.NoCatalog)
        {
            RenderNoCatalog();
            return;
        }
        _page.Children.Add(Button("Revenir à l'accueil vide", ReturnToWelcome));
        if (_scenarioUi.Active)
        {
            RenderScenario();
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

    private void RenderNoCatalog()
    {
        var view = _emptyView;
        var title = view switch
        {
            POCView.Collection => "Collection vide",
            POCView.Libraries => "Bibliothèques",
            _ => "Bienvenue dans ROMothèque Manager"
        };
        var explanation = view switch
        {
            POCView.Collection => "Aucun jeu affiché. Importez votre bibliothèque ES-DE pour explorer les systèmes et les fiches.",
            POCView.Libraries => "Aucune bibliothèque importée dans cette session. Choisissez un dossier pour commencer.",
            _ => "Votre bibliothèque est prête à être importée, sans données fictives préchargées."
        };
        _page.Children.Add(Hero("Premiers pas  /  bibliothèque", title, explanation));
        _page.Children.Add(PrimaryButton("Inventorier les fichiers · sans ES-DE", () =>
            new ObservedLibraryWindow().Show(this)));
        _page.Children.Add(Text("Inventaire réel désactivé dans cette Alpha ; seules les fixtures de validation sont autorisées en CI.", 12));
        _page.Children.Add(Button("Importer des métadonnées ES-DE (facultatif)", () => _ = ChooseLibraryAsync()));
        _page.Children.Add(Panel(Group(
            Text("Comment importer votre ROMothèque", 19, true),
            Text("1. Préparez le dossier principal contenant ROMS/ et, selon votre installation, MEDIAS/ ou ES-DE/gamelists/."),
            Text("2. Sélectionnez le dossier PARENT de ROMS/, et non le sous-dossier ROMS/."),
            Text("3. Confirmez l'import en lecture seule, puis consultez les systèmes, la Collection et les fiches.")
        )));
        _page.Children.Add(Notice("Informations importantes",
            "L'inventaire des fichiers ne dépend pas d'ES-DE, mais le scan personnel reste verrouillé. " +
            "L'import historique ES-DE est facultatif et indépendant. Aucune persistance après fermeture, aucun fichier ROM modifié."));
        _page.Children.Add(Button("Essayer une démonstration fictive", ShowDemo));
    }

    private void Dashboard()
    {
        foreach (var section in VisualDashboard.BuildDemo(
            _state.Catalogue, _libraryProjection,
            () => Navigate(POCView.Collection),
            () => Navigate(POCView.Libraries),
            VisualDashboard.IsCompact(Width)))
            _page.Children.Add(section);
    }

    private void Collection()
    {
        _page.Children.Add(Hero("Démo  /  collection", "Collection",
            "Données synthétiques : cette liste ne prouve aucune bibliothèque complète."));
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
        _list.Children.Add(Notice(games.Count + " résultats fictifs",
            "Couverture globale non garantie · aucune ROM personnelle lue."));
        foreach (var game in games)
        {
            var id = game.GameId;
            _list.Children.Add(Panel(Group(
                Text(game.Platform.ToUpperInvariant() + "  /  DÉMONSTRATION", 11, true),
                Text(game.Title, 19, true),
                Text(game.Platform + " · " + game.Variant),
                Text(game.Availability + " / " + game.Freshness + " / " + game.Coverage, 13),
                PrimaryButton("Ouvrir la fiche", () => OpenDemoGame(id))
            )));
        }
    }

    private void Libraries()
    {
        _page.Children.Add(Hero("Bibliothèques", "Bibliothèques & supports",
            "Sources de démonstration, états et simulation sans accès à vos dossiers."));
        _page.Children.Add(PrimaryButton("Importer une bibliothèque ES-DE…", () => _ = ChooseLibraryAsync()));
        _page.Children.Add(Text("Sélection manuelle d’une racine ES-DE ; import confirmé en lecture seule.", 12));
        _page.Children.Add(ScenarioSelector());
        _page.Children.Add(Button("NEXT-03 · Prévisualisation MVVM (SIMULATION)", () =>
            new InventoryPreviewWindow().Show(this)));
        _page.Children.Add(Text("NEXT-03 : aperçu indépendant, aucune lecture de dossier et aucun inventaire réel.", 12));

        _page.Children.Add(LibraryPage.Build(_libraryProjection, _libraryViewState,
            Render, Bounds.Width < 1000));
    }

    private void Detail()
    {
        var game = _state.Selected;
        if (game is null) { _state.ShowCollection(); Render(); return; }
        _page.Children.Add(Button("← Retour à la collection", () => Navigate(POCView.Collection)));
        _page.Children.Add(Hero("Fiche de démonstration  /  " + game.Platform,
            game.Title, "Données fictives · aucun titre de jeu identifié depuis une ROM."));
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
