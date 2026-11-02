namespace RomothequeManager.V2;

// UI-only selection state. It never computes, persists or exports comparisons.
/// <summary>Purely local selection of fictional libraries and A/B evidence; does not compute or persist comparisons.</summary>
public sealed class LibraryViewState
{
    private readonly LibraryProjection _projection;
    /// <summary>Stable identity of the library chosen from the validated synthetic projection.</summary>
    public string? SelectedLibraryId { get; private set; }
    /// <summary>Earlier selected snapshot reference; changing it invalidates the current comparison.</summary>
    public string? BeforeSnapshotId { get; private set; }
    /// <summary>Later snapshot reference; chronology and evidence are checked before comparison.</summary>
    public string? AfterSnapshotId { get; private set; }
    /// <summary>Qualified evidence from the projection, or null after invalid input or selection changes.</summary>
    public UiComparison? CurrentComparison { get; private set; }
    /// <summary>Human-readable refusal reason when a requested comparison lacks valid evidence.</summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>Selected library resolved by exact ID, null if the identity is absent.</summary>
    public UiLibrary? SelectedLibrary => _projection.FindLibrary(SelectedLibraryId);

    /// <summary>Starts with the first declared fictional library while retaining the validated projection.</summary>
    public LibraryViewState(LibraryProjection projection)
    {
        _projection = projection ?? throw new ArgumentNullException(nameof(projection));
        var first = projection.Libraries.FirstOrDefault();
        if (first is not null) SelectLibrary(first.LibraryId);
    }

    /// <summary>Changes library only for an existing ID and resets the A/B pair to its chronological boundaries.</summary>
    /// <returns>False without changing state when the library is unknown.</returns>
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

    /// <summary>Selects a candidate earlier generation and invalidates any previous A/B result.</summary>
    public void SetBefore(string? snapshotId)
    {
        BeforeSnapshotId = snapshotId;
        CurrentComparison = null;
        ErrorMessage = null;
    }

    /// <summary>Selects a candidate later generation and invalidates any previous A/B result.</summary>
    public void SetAfter(string? snapshotId)
    {
        AfterSnapshotId = snapshotId;
        CurrentComparison = null;
        ErrorMessage = null;
    }

    /// <summary>Accepts only prequalified, chronologically ordered A/B evidence from an indexed library.</summary>
    /// <remarks>Unknown coverage, inaccessible indexes and unqualified pairs yield refusal; missing evidence is never treated as deletion.</remarks>
    /// <returns>True when a previously validated comparison matches the selected library and pair.</returns>
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
