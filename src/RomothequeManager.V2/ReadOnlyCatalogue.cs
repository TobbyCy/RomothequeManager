using System.Collections.ObjectModel;

namespace RomothequeManager.V2;

// V2-01A synthetic-only candidate. No filesystem, media, exports or writable storage.
/// <summary>Invented demonstration game with observation labels and evidence; it does not represent a discovered ROM or persisted game identity.</summary>
public sealed record GameItem(
    string GameId, string Title, string Platform, string Variant,
    string Library, string LibraryId, string Availability, string Freshness, string Coverage,
    string SnapshotId, DateTimeOffset ObservedAt, string Evidence);

/// <summary>In-memory collection of six fictional, uniquely identified examples with no source filesystem access.</summary>
public sealed class ReadOnlyCatalogue
{
    private readonly ReadOnlyCollection<GameItem> _games;
    /// <summary>Read-only view of synthetic examples, never live inventory results.</summary>
    public IReadOnlyList<GameItem> Games => _games;

    private ReadOnlyCatalogue(GameItem[] games)
    {
        if (games.Length == 0 || games.Any(x => string.IsNullOrWhiteSpace(x.GameId)) ||
            games.Select(x => x.GameId).Distinct(StringComparer.Ordinal).Count() != games.Length)
            throw new ArgumentException("Synthetic catalogue identities must be unique.");
        _games = Array.AsReadOnly(games);
    }

    /// <summary>Constructs the fixed fictional catalogue demonstrating online, offline and unknown evidence states.</summary>
    public static ReadOnlyCatalogue Synthetic() => new([
        new("syn-001", "Explorateurs imaginaires", "GBA", "Édition Europe", "Bibliothèque A", "lib-a",
            "OFFLINE", "STALE", "COMPLETE", "fictif-2026-10-20", new(2026,10,20,14,0,0,TimeSpan.Zero),
            "Ancienne observation disponible, source hors ligne"),
        new("syn-002", "Station Orbitale 198X", "PS1", "Disque 1 / 2", "Bibliothèque A", "lib-a",
            "OFFLINE", "STALE", "INCOMPLETE", "fictif-2026-10-20", new(2026,10,20,14,0,0,TimeSpan.Zero),
            "Couverture partielle : aucune disparition déduite"),
        new("syn-003", "Le Jardin des Étoiles", "GBA", "Version imaginaire", "Bibliothèque B", "lib-b",
            "UNKNOWN", "UNKNOWN", "UNKNOWN", "fictif-2026-10-18", new(2026,10,18,9,30,0,TimeSpan.Zero),
            "Accès non établi ; données historiques uniquement"),
        new("syn-004", "Château Pixel", "SNES", "Édition fictive", "Bibliothèque B", "lib-b",
            "UNKNOWN", "UNKNOWN", "INCOMPLETE", "fictif-2026-10-18", new(2026,10,18,9,30,0,TimeSpan.Zero),
            "Un index incomplet n'est pas un catalogue vide"),
        new("syn-005", "Tournée Galactique", "PS1", "Édition démo", "Bibliothèque C", "lib-c",
            "ONLINE", "FRESH", "COMPLETE", "fictif-2026-10-22", new(2026,10,22,12,15,0,TimeSpan.Zero),
            "Observation synthétique explicitement qualifiée"),
        new("syn-006", "Labyrinthe Azur", "GBA", "Édition test", "Bibliothèque C", "lib-c",
            "ONLINE", "FRESH", "COMPLETE", "fictif-2026-10-22", new(2026,10,22,12,15,0,TimeSpan.Zero),
            "Un jeu fictif ; aucune ROM distribuée")
    ]);

    /// <summary>Filters fictional game titles, platforms and IDs case-insensitively without scanning files.</summary>
    public IReadOnlyList<GameItem> Search(string? text) =>
        _games.Where(g => string.IsNullOrWhiteSpace(text) ||
            g.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) ||
            g.Platform.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) ||
            g.GameId.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>Looks up an exact fictional identity, returning null for an absent or unmatched ID.</summary>
    public GameItem? Find(string? gameId) =>
        _games.FirstOrDefault(g => string.Equals(g.GameId, gameId, StringComparison.Ordinal));

    /// <summary>Counts distinct fictional library names with the specified availability label, not physical devices.</summary>
    public int CountLibraries(string availability) =>
        _games.Where(g => g.Availability == availability).Select(g => g.Library).Distinct(StringComparer.Ordinal).Count();
}

/// <summary>Non-persistent navigation destinations in the demonstration interface.</summary>
public enum POCView
{
    /// <summary>Aggregate fictional library evidence without a live filesystem observation.</summary>
    Dashboard,
    /// <summary>Searchable list of fictional game examples; never an independent ROM scan.</summary>
    Collection,
    /// <summary>One selected fictional item with source labels and observational caveats.</summary>
    GameDetail,
    /// <summary>Declared fictional library identities and their availability states.</summary>
    Libraries
}

/// <summary>Transient demo navigation and selection; changes affect only the in-memory UI state.</summary>
public sealed class NavigationState(ReadOnlyCatalogue catalogue)
{
    /// <summary>Immutable fictional catalogue supplied to navigation; never a real imported library.</summary>
    public ReadOnlyCatalogue Catalogue { get; } = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
    /// <summary>Current demo destination, initially Dashboard and never a persisted preference.</summary>
    public POCView CurrentView { get; private set; } = POCView.Dashboard;
    /// <summary>Fictional item selected for the detail page; null outside an opened game.</summary>
    public GameItem? Selected { get; private set; }
    /// <summary>In-memory query used only for filtering the demo's fictional items.</summary>
    public string SearchText { get; private set; } = "";

    /// <summary>Returns to the demonstration dashboard and clears any selected item.</summary>
    public void ShowDashboard() { CurrentView = POCView.Dashboard; Selected = null; }
    /// <summary>Shows the fictional collection and clears the prior detail selection.</summary>
    public void ShowCollection() { CurrentView = POCView.Collection; Selected = null; }
    /// <summary>Shows the declared demo libraries without probing their stated roots.</summary>
    public void ShowLibraries() { CurrentView = POCView.Libraries; Selected = null; }
    /// <summary>Updates the demo query and navigates to the collection without resolving paths.</summary>
    public void Search(string? value) { SearchText = value ?? ""; CurrentView = POCView.Collection; }
    /// <summary>Opens a known fictional game by ID; rejects unknown IDs without altering selection.</summary>
    /// <returns>True when the game is present in the immutable demo catalogue.</returns>
    public bool Open(string? id)
    {
        var game = Catalogue.Find(id);
        if (game is null) return false;
        Selected = game; CurrentView = POCView.GameDetail; return true;
    }
    /// <summary>Snapshot of demo entries matching the active text filter, with no disk access.</summary>
    public IReadOnlyList<GameItem> FilteredGames => Catalogue.Search(SearchText);
}
