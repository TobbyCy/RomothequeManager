using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>ViewModel-only navigation for independently observed filesystem files.</summary>
/// <remarks>Consumes the existing inventory service through InventoryViewModel. Filtering,
/// sorting and paging are delegated to DEV 1's pure InventoryObservationProjection.
/// ObservationId and LocationKey are transient; neither identifies a game or media asset.
/// No filesystem access, XML import, matching, or persistence is performed here.</remarks>
public sealed class ObservedLibraryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly InventoryViewModel _inventory;
    private readonly bool _fixtureMode;
    private readonly InventoryCommand _dashboard;
    private readonly InventoryCommand _collection;
    private readonly InventoryCommand _openFile;
    private readonly InventoryCommand _back;
    private readonly InventoryCommand _previous;
    private readonly InventoryCommand _next;
    private string _search = "";
    private string _platform = "Toutes";
    private string _extension = "Toutes";
    private string _kind = "Tous";
    private int _pageIndex;
    private InventorySnapshot? _generation;
    private InventoryObservationPage? _page;
    private InventoryObservationRow? _selected;
    private ObservedView _view = ObservedView.Dashboard;
    private bool _disposed;
    private RestoredCatalogSession? _restored;
    private RsAUiViewModel? _rsa;

    /// <summary>Presentation-only navigation for observed ROM files; sorting and paging use the pure projection without filesystem I/O.</summary>
    public ObservedLibraryViewModel(InventoryViewModel inventory, bool fixtureMode)
    {
        _inventory = inventory ?? throw new ArgumentNullException(nameof(inventory));
        _fixtureMode = fixtureMode;
        Media = new MediaPanelViewModel(new MediaPresentationService(), new MediaPreviewService());
        _dashboard = new InventoryCommand(_ => View = ObservedView.Dashboard, _ => true);
        _collection = new InventoryCommand(_ => View = ObservedView.Collection, _ => true);
        _openFile = new InventoryCommand(_ => OpenSelected(), _ => _selected is not null);
        _back = new InventoryCommand(_ => View = ObservedView.Collection, _ => true);
        _previous = new InventoryCommand(_ => { _pageIndex--; RefreshPage(); }, _ => _pageIndex > 0);
        _next = new InventoryCommand(_ => { _pageIndex++; RefreshPage(); }, _ => HasNextPage);
        _inventory.PropertyChanged += OnInventoryChanged;
        RefreshPage();
    }

    /// <summary>Notifies the view when an accepted inventory generation, filter, selection or navigation state changes.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Inventory adapter supplying service-owned snapshots and explicit scan commands.</summary>
    public InventoryViewModel Inventory => _inventory;
    /// <summary>Optional fixture-only restored SQLite session; its historical data remains offline and unverified.</summary>
    public RestoredCatalogSession? RestoredSession => _restored;
    /// <summary>Optional restricted RS-A fixture adapter; it does not grant access to personal ROMS.</summary>
    public RsAUiViewModel? RsA => _rsa;
    /// <summary>Indicates that the restricted fixture-only RS-A adapter is attached.</summary>
    public bool HasRsA => _rsa is not null;
    /// <summary>Indicates historical SQLite fixture presentation rather than a live scan.</summary>
    public bool HasRestoredSession => _restored is not null;
    /// <summary>Shows the evidence-only media panel only outside RS-A and restored-catalogue modes.</summary>
    public bool HasLiveMediaPanel => _restored is null && _rsa is null;
    private InventorySnapshot? DisplayedSnapshot => _restored is not null
        ? _restored.DisplayedInventory : _rsa is not null
        ? _rsa.DisplayedSnapshot : _inventory.DisplayedSnapshot;
    private IReadOnlyList<FileObservation> DisplayedObservations =>
        DisplayedSnapshot?.Observations ?? Array.Empty<FileObservation>();
    /// <summary>Evidence-only media panel; candidate display never approves a cover association.</summary>
    public MediaPanelViewModel Media { get; }
    /// <summary>Indicates that the composition was explicitly created for bounded fixture testing.</summary>
    public bool FixtureMode => _fixtureMode;
    /// <summary>Allows fixture inventory only without competing RS-A/restored modes and with service consent gates satisfied.</summary>
    public bool CanInventoryFixture => _fixtureMode && _restored is null && _rsa is null && _inventory.CanStart;
    /// <summary>Explains whether displayed data are simulated, historical, fixture-only or disabled in production.</summary>
    public string ModeNotice => _restored is not null
        ? "Collection SQLite fictive restaurée : consultation OFFLINE / STALE ; aucun accès disque."
        : _rsa is not null
        ? "RS-A · simulation Windows confinée aux fixtures internes. Aucun droit de scan personnel."
        : _fixtureMode
        ? "PREUVE WINDOWS · vrais fichiers fictifs, autorisation limitée à ces fixtures uniquement."
        : "Inventaire personnel désactivé dans cette version. Le service de production refuse tout scan.";
    /// <summary>Explains the scope and limits of the displayed ROMS evidence and unavailable permissions.</summary>
    public string ScopeText => _restored is not null
        ? "Historique SQLite uniquement : aucun scan, aucune lecture média ou revalidation du SSD."
        : _rsa is not null
        ? "RS-A · handles Windows, ROMS métadonnées seulement, grant non persistant, sans MEDIAS ni Skia."
        : "ROMS/ uniquement · profondeur 8 · 100 000 fichiers max · 10 000 dossiers max · playlists M3U 32 Kio / 256 lignes. Lecture seule, sans hash ROM, XML ni sauvegarde.";
    /// <summary>Navigates to the dashboard without initiating a scan.</summary>
    public ICommand ShowDashboardCommand => _dashboard;
    /// <summary>Navigates to the filtered observed-file collection.</summary>
    public ICommand ShowCollectionCommand => _collection;
    /// <summary>Opens detail for a currently selected observed file, not a confirmed game identity.</summary>
    public ICommand OpenFileCommand => _openFile;
    /// <summary>Returns from observation detail to the collection without scanning.</summary>
    public ICommand ReturnCollectionCommand => _back;
    /// <summary>Navigates backward within the bounded projection page set.</summary>
    public ICommand PreviousPageCommand => _previous;
    /// <summary>Navigates forward only when a further page exists.</summary>
    public ICommand NextPageCommand => _next;
    /// <summary>Current observed-file route; leaving detail invalidates an existing preview.</summary>
    public ObservedView View
    {
        get => _view;
        set
        {
            if (_view == value) return;
            _view = value;
            if (value != ObservedView.FileDetail) Media.ClearPreview();
            Notify();
            Notify(nameof(IsDashboard));
            Notify(nameof(IsCollection));
            Notify(nameof(IsFileDetail));
        }
    }
    /// <summary>True when the dashboard route is selected.</summary>
    public bool IsDashboard => View == ObservedView.Dashboard;
    /// <summary>True when the observed-file collection route is selected.</summary>
    public bool IsCollection => View == ObservedView.Collection;
    /// <summary>True when an observation detail route is selected.</summary>
    public bool IsFileDetail => View == ObservedView.FileDetail;
    /// <summary>True when an inventory generation is available for presentation.</summary>
    public bool HasSnapshot => DisplayedSnapshot is not null;
    /// <summary>Case-insensitive text filter; changing it resets paging and selection.</summary>
    public string SearchText
    {
        get => _search;
        set { var next = value ?? ""; if (_search == next) return; _search = next; FiltersChanged(); }
    }
    /// <summary>Observed platform-key filter, with an all-platforms default and no game-title inference.</summary>
    public string PlatformFilter
    {
        get => _platform;
        set { var next = value ?? "Toutes"; if (_platform == next) return; _platform = next; FiltersChanged(); }
    }
    /// <summary>Observed extension filter, including a distinct option for missing extensions.</summary>
    public string ExtensionFilter
    {
        get => _extension;
        set { var next = value ?? "Toutes"; if (_extension == next) return; _extension = next; FiltersChanged(); }
    }
    /// <summary>Observed inventory-kind filter; changing it resets paging and selection.</summary>
    public string KindFilter
    {
        get => _kind;
        set { var next = value ?? "Tous"; if (_kind == next) return; _kind = next; FiltersChanged(); }
    }
    /// <summary>Platform keys actually present in the displayed generation plus the all-platforms choice.</summary>
    public IReadOnlyList<string> PlatformOptions => new[] { "Toutes" }
        .Concat(DisplayedObservations.Select(x => string.IsNullOrWhiteSpace(x.PlatformKey)
            ? "UNKNOWN" : x.PlatformKey).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)).ToArray();
    /// <summary>Extensions actually observed in the displayed generation plus the all-extensions choice.</summary>
    public IReadOnlyList<string> ExtensionOptions => new[] { "Toutes" }
        .Concat(DisplayedObservations.Select(x => string.IsNullOrWhiteSpace(x.Extension)
            ? "(aucune)" : x.Extension).Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)).ToArray();
    /// <summary>Available inventory-kind names plus the all-kinds choice.</summary>
    public IReadOnlyList<string> KindOptions => new[] { "Tous" }
        .Concat(Enum.GetNames<InventoryKind>()).ToArray();
    /// <summary>Current bounded page from InventoryObservationProjection, never an unbounded live scan.</summary>
    public IReadOnlyList<InventoryObservationRow> Rows =>
        _page?.Rows ?? Array.Empty<InventoryObservationRow>();
    /// <summary>Number of observations matching active filters, not an inventory completeness claim.</summary>
    public int MatchingFileCount => _page?.MatchingObservationCount ?? 0;
    /// <summary>Zero-based index of the displayed bounded observation page.</summary>
    public int PageIndex => _pageIndex;
    /// <summary>Displays the filtered count and page position, with a 50-row maximum per page.</summary>
    public string PageText => _page is null ? "Aucun inventaire" :
        "Fichiers correspondant aux filtres : " + MatchingFileCount +
        " · page " + (_pageIndex + 1) + "/" + Math.Max(1, (MatchingFileCount + 49) / 50);
    /// <summary>True only when matching observations remain beyond the current 50-row page.</summary>
    public bool HasNextPage => _page is not null && (long)(_pageIndex + 1) * 50 < MatchingFileCount;
    /// <summary>True when the current projection page is not the first.</summary>
    public bool HasPreviousPage => _pageIndex > 0;
    /// <summary>Accepts only a row in the current page; selection clears prior media choices and preview.</summary>
    public InventoryObservationRow? SelectedRow
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            if (value is not null && !Rows.Any(r =>
                r.Observation.ObservationId == value.Observation.ObservationId)) return;
            _selected = value;
            Media.SelectRom(_restored is null ? value?.Observation.ObservationId : null);
            _restored?.SelectRom(value?.Observation);
            Notify();
            _openFile.NotifyCanExecuteChanged();
        }
    }
    /// <summary>Provisional label of the selected observation, never a verified commercial game name.</summary>
    public string SelectedName => _selected?.ProvisionalLabel ?? "Aucun fichier sélectionné";
    /// <summary>Displays status, availability and freshness from historical evidence; does not revalidate a drive.</summary>
    public string SnapshotStatus => _restored is not null ? _restored.StatusLabel :
        _rsa is { IsOffline: true } ? _rsa.SnapshotLabel :
        DisplayedSnapshot is { } s
        ? "Inventaire " + s.ScanStatus + " · disponibilité " + s.Availability +
          " · fraîcheur " + s.Freshness + " · capturé " + s.CapturedAtUtc.ToString("u")
        : "Aucun inventaire consultable.";
    /// <summary>Shows observed files and supported totals, leaving unknown counts explicitly unknown.</summary>
    public string Summary => DisplayedSnapshot is { } s
        ? "Fichiers observés : " + s.ObservedFileCount +
          " · total " + (s.ScanStatus == InventoryScanStatus.Complete &&
              s.TotalFileCount.HasValue ? s.TotalFileCount.Value.ToString() : "inconnu") +
          " · jeux identifiés : " + (s.ConfirmedGameCount?.ToString() ?? "inconnu")
        : "Aucun fichier observé dans cette session.";
    /// <summary>Bounded frequency summary of observed platform keys, not inferred game platforms.</summary>
    public string PlatformSummary => Summarize(x => x.PlatformKey);
    /// <summary>Bounded frequency summary of observed file extensions.</summary>
    public string ExtensionSummary => Summarize(x => x.Extension);
    /// <summary>Bounded frequency summary of observed inventory file kinds.</summary>
    public string TypeSummary => Summarize(x => x.Kind.ToString());
    /// <summary>Identifies the displayed complete, attempt, RS-A or restored historical generation.</summary>
    public string GenerationLabel => _restored is not null ? _restored.StatusLabel
        : _rsa is not null ? _rsa.SnapshotLabel : _inventory.DisplayedGenerationText;
    /// <summary>Diagnostics belonging to the currently displayed generation.</summary>
    public IReadOnlyList<InventoryDiagnostic> Diagnostics =>
        DisplayedSnapshot?.Diagnostics ?? Array.Empty<InventoryDiagnostic>();
    /// <summary>Read-only coverage proof of the currently displayed generation.</summary>
    public IReadOnlyList<InventoryCoverage> Coverage =>
        DisplayedSnapshot?.CoverageProof ?? Array.Empty<InventoryCoverage>();
    /// <summary>Count of diagnostics in the displayed inventory generation.</summary>
    public string DiagnosticSummary => Diagnostics.Count + " diagnostic(s) de la génération affichée.";
    /// <summary>Makes no-observation or no-filter-match status explicit without inferring absent games.</summary>
    public string EmptyText => _page is null ? "Aucune preuve : sélectionner et confirmer un inventaire fictif." :
        MatchingFileCount == 0
        ? "Aucun fichier observé ne correspond aux filtres. Ceci n'est pas une preuve d'absence de jeux."
        : "";
    /// <summary>Explains evidence-only media, lack of automatic association and mode-specific access restrictions.</summary>
    public string MediaNotice => _rsa is not null
        ? "RS-A n'accorde aucun droit MEDIAS, aperçu ou association."
        : _restored is null
        ? "Jaquette locale : non associée. Une proposition de média ou un aperçu ne valide jamais une association avec ce fichier."
        : "Choix manuel depuis un index historique fictif : aucun aperçu ni lien physique vérifié.";

    private string Summarize(Func<FileObservation, string> selector)
    {
        var obs = DisplayedObservations;
        if (obs.Count == 0) return "Aucune donnée de répartition disponible.";
        return string.Join(" · ", obs.GroupBy(selector, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(x => x.Count()).ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Take(8).Select(x => (string.IsNullOrWhiteSpace(x.Key) ? "UNKNOWN" : x.Key) +
                " : " + x.Count()));
    }

    private void FiltersChanged()
    {
        _pageIndex = 0;
        _selected = null;
        Media.SelectRom(null);
        _restored?.SelectRom(null);
        RefreshPage();
        Notify(nameof(SearchText));
        Notify(nameof(PlatformFilter));
        Notify(nameof(ExtensionFilter));
        Notify(nameof(KindFilter));
    }

    private void OnInventoryChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || _restored is not null || _rsa is not null) return;
        // The inventory VM emits a property batch for one state transition.
        // Rebuild the expensive page and ComboBox options only for a new snapshot,
        // otherwise TwoWay SelectedItem can re-enter the filters indefinitely.
        if (e.PropertyName is nameof(InventoryViewModel.CanStart) or
            nameof(InventoryViewModel.ConsentConfirmed) or nameof(InventoryViewModel.Phase))
            Notify(nameof(CanInventoryFixture));
        if (e.PropertyName != nameof(InventoryViewModel.DisplayedSnapshot)) return;
        var newGeneration = _inventory.DisplayedSnapshot;
        if (ReferenceEquals(_generation, newGeneration)) return;
        _generation = newGeneration;
        _selected = null;
        Media.SelectRom(null);
        _pageIndex = 0;
        _platform = "Toutes";
        _extension = "Toutes";
        _kind = "Tous";
        _search = "";
        RefreshPage();
        Notify(nameof(PlatformOptions));
        Notify(nameof(ExtensionOptions));
        Notify(nameof(KindOptions));
    }

    /// <summary>Attach a narrowly scoped RS-A fixture adapter; never available from production UI.</summary>
    internal void AttachRsAForCi(RsAUiViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_fixtureMode || _disposed || _restored is not null || _rsa is not null)
            throw new InvalidOperationException("UI_RSA_FIXTURE_SESSION_REQUIRED");
        _rsa = session;
        _rsa.PropertyChanged += OnRsAChanged;
        _inventory.PropertyChanged -= OnInventoryChanged;
        _selected = null;
        _pageIndex = 0;
        _search = "";
        _platform = "Toutes";
        _extension = "Toutes";
        _kind = "Tous";
        Media.SelectRom(null);
        Notify(nameof(RsA));
        Notify(nameof(HasRsA));
        Notify(nameof(HasLiveMediaPanel));
        Notify(nameof(CanInventoryFixture));
        Notify(nameof(ModeNotice));
        Notify(nameof(ScopeText));
        Notify(nameof(MediaNotice));
        RefreshPage();
    }

    private void OnRsAChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(RsAUiViewModel.DisplayedSnapshot)) return;
        var next = _rsa?.DisplayedSnapshot;
        if (ReferenceEquals(_generation, next)) return;
        _generation = next;
        _selected = null;
        _pageIndex = 0;
        _platform = "Toutes";
        _extension = "Toutes";
        _kind = "Tous";
        _search = "";
        Media.SelectRom(null);
        RefreshPage();
        Notify(nameof(PlatformOptions));
        Notify(nameof(ExtensionOptions));
        Notify(nameof(KindOptions));
    }

    /// <summary>Inject one fixture-owned restored session. Normal product startup does not call this.</summary>
    internal void AttachRestoredForCi(RestoredCatalogSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!_fixtureMode || _disposed || _restored is not null || _rsa is not null)
            throw new InvalidOperationException("UI05_FIXTURE_SESSION_REQUIRED");
        _restored = session;
        _restored.PropertyChanged += OnRestoredChanged;
        _inventory.PropertyChanged -= OnInventoryChanged;
        _selected = null;
        _pageIndex = 0;
        _search = "";
        _platform = "Toutes";
        _extension = "Toutes";
        _kind = "Tous";
        Media.SelectRom(null);
        Notify(nameof(RestoredSession));
        Notify(nameof(HasRestoredSession));
        Notify(nameof(HasLiveMediaPanel));
        Notify(nameof(ModeNotice));
        Notify(nameof(ScopeText));
        Notify(nameof(MediaNotice));
        RefreshPage();
        Notify(nameof(PlatformOptions));
        Notify(nameof(ExtensionOptions));
        Notify(nameof(KindOptions));
    }

    private void OnRestoredChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_disposed || e.PropertyName != nameof(RestoredCatalogSession.DisplayedInventory))
            return;
        _selected = null;
        _pageIndex = 0;
        _restored?.SelectRom(null);
        RefreshPage();
    }

    private void RefreshPage()
    {
        var snapshot = DisplayedSnapshot;
        if (snapshot is null)
            _page = null;
        else
        {
            InventoryKind? kind = Enum.TryParse<InventoryKind>(_kind, out var parsed)
                ? parsed : null;
            var platform = _platform == "Toutes" ? null : _platform;
            var extension = _extension == "Toutes" ? null :
                _extension == "(aucune)" ? "" : _extension;
            _page = InventoryObservationProjection.GetPage(snapshot,
                new InventoryObservationFilter(platform, extension, kind, _search, _pageIndex, 50));
            if (_page.Rows.Count == 0 && _pageIndex > 0)
            {
                _pageIndex = Math.Max(0, (_page.MatchingObservationCount - 1) / 50);
                _page = InventoryObservationProjection.GetPage(snapshot,
                    new InventoryObservationFilter(platform, extension, kind, _search, _pageIndex, 50));
            }
        }
        foreach (var property in new[]
        {
            nameof(Inventory), nameof(CanInventoryFixture), nameof(HasSnapshot),
            nameof(Rows), nameof(MatchingFileCount), nameof(PageIndex), nameof(PageText),
            nameof(HasNextPage), nameof(HasPreviousPage), nameof(SelectedRow),
            nameof(SelectedName), nameof(SnapshotStatus), nameof(Summary),
            nameof(PlatformSummary), nameof(ExtensionSummary), nameof(TypeSummary),
            nameof(GenerationLabel), nameof(Diagnostics), nameof(Coverage),
            nameof(DiagnosticSummary), nameof(EmptyText), nameof(PlatformFilter),
            nameof(ExtensionFilter), nameof(KindFilter), nameof(SearchText)
        }) Notify(property);
        _previous.NotifyCanExecuteChanged();
        _next.NotifyCanExecuteChanged();
        _openFile.NotifyCanExecuteChanged();
    }

    private void OpenSelected()
    {
        if (_selected is null) return;
        Notify(nameof(SelectedName));
        View = ObservedView.FileDetail;
    }

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Unsubscribes snapshot notifications, disposes owned fixture adapters and the media panel, but not the injected inventory service.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _inventory.PropertyChanged -= OnInventoryChanged;
        if (_rsa is not null)
        {
            _rsa.PropertyChanged -= OnRsAChanged;
            _rsa.Dispose();
        }
        if (_restored is not null)
        {
            _restored.PropertyChanged -= OnRestoredChanged;
            _restored.Dispose();
        }
        Media.Dispose();
    }
}

/// <summary>UI-only locations for an observed-file workflow; not game identities.</summary>
public enum ObservedView { Dashboard, Collection, FileDetail }
