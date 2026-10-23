using System.Collections.ObjectModel;

namespace RomothequeManager.V2;

// V2-01A synthetic-only candidate. No filesystem, media, exports or writable storage.
public sealed record GameItem(
    string GameId, string Title, string Platform, string Variant,
    string Library, string LibraryId, string Availability, string Freshness, string Coverage,
    string SnapshotId, DateTimeOffset ObservedAt, string Evidence);

public sealed class ReadOnlyCatalogue
{
    private readonly ReadOnlyCollection<GameItem> _games;
    public IReadOnlyList<GameItem> Games => _games;

    private ReadOnlyCatalogue(GameItem[] games)
    {
        if (games.Length == 0 || games.Any(x => string.IsNullOrWhiteSpace(x.GameId)) ||
            games.Select(x => x.GameId).Distinct(StringComparer.Ordinal).Count() != games.Length)
            throw new ArgumentException("Synthetic catalogue identities must be unique.");
        _games = Array.AsReadOnly(games);
    }

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

    public IReadOnlyList<GameItem> Search(string? text) =>
        _games.Where(g => string.IsNullOrWhiteSpace(text) ||
            g.Title.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) ||
            g.Platform.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase) ||
            g.GameId.Contains(text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();

    public GameItem? Find(string? gameId) =>
        _games.FirstOrDefault(g => string.Equals(g.GameId, gameId, StringComparison.Ordinal));

    public int CountLibraries(string availability) =>
        _games.Where(g => g.Availability == availability).Select(g => g.Library).Distinct(StringComparer.Ordinal).Count();
}

public enum POCView { Dashboard, Collection, GameDetail, Libraries }

public sealed class NavigationState(ReadOnlyCatalogue catalogue)
{
    public ReadOnlyCatalogue Catalogue { get; } = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
    public POCView CurrentView { get; private set; } = POCView.Dashboard;
    public GameItem? Selected { get; private set; }
    public string SearchText { get; private set; } = "";

    public void ShowDashboard() { CurrentView = POCView.Dashboard; Selected = null; }
    public void ShowCollection() { CurrentView = POCView.Collection; Selected = null; }
    public void ShowLibraries() { CurrentView = POCView.Libraries; Selected = null; }
    public void Search(string? value) { SearchText = value ?? ""; CurrentView = POCView.Collection; }
    public bool Open(string? id)
    {
        var game = Catalogue.Find(id);
        if (game is null) return false;
        Selected = game; CurrentView = POCView.GameDetail; return true;
    }
    public IReadOnlyList<GameItem> FilteredGames => Catalogue.Search(SearchText);
}
