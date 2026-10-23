namespace RomothequeManager.V2;

// UI-only selection state. It never computes, persists or exports comparisons.
public sealed class LibraryViewState
{
    private readonly LibraryProjection _projection;
    public string? SelectedLibraryId { get; private set; }
    public string? BeforeSnapshotId { get; private set; }
    public string? AfterSnapshotId { get; private set; }
    public UiComparison? CurrentComparison { get; private set; }
    public string? ErrorMessage { get; private set; }

    public UiLibrary? SelectedLibrary => _projection.FindLibrary(SelectedLibraryId);

    public LibraryViewState(LibraryProjection projection)
    {
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        var first = projection.Libraries.FirstOrDefault();
        if (first is not null) SelectLibrary(first.LibraryId);
    }

    public bool SelectLibrary(string? libraryId)
    {
        var library = _projection.FindLibrary(libraryId);
        if (library is null) return false;
        SelectedLibraryId = library.LibraryId;
        var snapshots = library.Generations.OrderBy(item => item.CapturedAtUtc, StringComparer.Ordinal).ToArray();
        BeforeSnapshotId = snapshots.FirstOrDefault()?.SnapshotId;
        AfterSnapshotId = snapshots.LastOrDefault()?.SnapshotId;
        CurrentComparison = null;
        ErrorMessage = null;
        return true;
    }

    public void SetBefore(string? snapshotId)
    {
        BeforeSnapshotId = snapshotId;
        CurrentComparison = null;
        ErrorMessage = null;
    }

    public void SetAfter(string? snapshotId)
    {
        AfterSnapshotId = snapshotId;
        CurrentComparison = null;
        ErrorMessage = null;
    }

    public bool TryCompare()
    {
        CurrentComparison = null;
        ErrorMessage = null;
        var library = SelectedLibrary;
        if (library is null) return Refuse("Bibliothèque inconnue.");
        if (!library.IndexAccessible) return Refuse("Index inaccessible : comparaison indisponible, compteur inconnu.");
        var before = library.Generations.FirstOrDefault(item =>
            string.Equals(item.SnapshotId, BeforeSnapshotId, StringComparison.Ordinal));
        var after = library.Generations.FirstOrDefault(item =>
            string.Equals(item.SnapshotId, AfterSnapshotId, StringComparison.Ordinal));
        if (before is null || after is null) return Refuse("Snapshot A ou B introuvable dans cette bibliothèque.");
        if (before.SnapshotId == after.SnapshotId) return Refuse("A et B doivent être deux générations distinctes.");
        if (DateTimeOffset.Parse(after.CapturedAtUtc) < DateTimeOffset.Parse(before.CapturedAtUtc))
            return Refuse("La date de B ne peut pas précéder celle de A.");
        var qualified = _projection.FindComparison(library.LibraryId, before.SnapshotId, after.SnapshotId);
        if (qualified is null)
            return Refuse("Aucune comparaison préqualifiée pour cette paire A/B. Aucun résultat déduit.");
        CurrentComparison = qualified;
        return true;
    }

    private bool Refuse(string reason)
    {
        ErrorMessage = reason;
        return false;
    }
}
