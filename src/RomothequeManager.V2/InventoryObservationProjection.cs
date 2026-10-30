namespace RomothequeManager.V2.Inventory;

/// <summary>Source-neutral filters for observed files, never identified games.</summary>
/// <remarks>All criteria are applied to existing scan metadata in memory. No filesystem,
/// ES-DE, media, network, or persistence access occurs.</remarks>
public sealed record InventoryObservationFilter(
    string? PlatformKey = null,
    string? Extension = null,
    InventoryKind? Kind = null,
    string? SearchText = null,
    int PageIndex = 0,
    int PageSize = 50);

/// <summary>A physical file observation and its explicitly provisional filename label.</summary>
/// <remarks>The observation is carried unchanged. ObservationId is scan-scoped,
/// LocationKey session-scoped; neither is a game identity nor a durable SQLite key.
/// A future independent media index may reference an observation using separate
/// scoped evidence; this record makes no media link or identity decision.</remarks>
public sealed record InventoryObservationRow(
    FileObservation Observation,
    string ProvisionalLabel);

/// <summary>One bounded, stable page of files from a captured inventory generation.</summary>
/// <remarks>Snapshot retains coverage, diagnostics, availability, freshness,
/// and nullable ConfirmedGameCount unchanged. The calling service/ViewModel chooses
/// whether the supplied generation is Current or LastAttempt; Snapshot does not
/// carry that selection. Pages returned by GetPage expose read-only Rows.
/// MatchingObservationCount is the filtered number of observed files only;
/// it is never a confirmed-game count or a claim about unscanned files.</remarks>
public sealed record InventoryObservationPage(
    InventorySnapshot Snapshot,
    IReadOnlyList<InventoryObservationRow> Rows,
    int MatchingObservationCount,
    int PageIndex,
    int PageSize);

/// <summary>Read-only presentation projection over NEXT-02 FileObservation records.</summary>
/// <remarks>Does not construct InventorySession, call InventoryReader, import ES-DE XML,
/// assign a game ID, infer covers, scan files, or perform I/O. The caller chooses
/// which generation to present (Current complete or LastAttempt incomplete).
/// Partial/offline observations remain observations; missing results never mean DELETED.</remarks>
public static class InventoryObservationProjection
{
    /// <summary>Filter, order and page observations from exactly the supplied snapshot.</summary>
    /// <exception cref="ArgumentNullException">Snapshot is absent.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Page index/size is invalid.</exception>
    public static InventoryObservationPage GetPage(
        InventorySnapshot snapshot, InventoryObservationFilter? filter = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        filter ??= new InventoryObservationFilter();
        if (filter.PageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(filter), "PageIndex must be nonnegative.");
        if (filter.PageSize is < 1 or > 200)
            throw new ArgumentOutOfRangeException(nameof(filter), "PageSize must be between 1 and 200.");

        var matching = snapshot.Observations
            .Where(observation =>
                (string.IsNullOrWhiteSpace(filter.PlatformKey) ||
                 string.Equals(observation.PlatformKey, filter.PlatformKey.Trim(),
                     StringComparison.OrdinalIgnoreCase)) &&
                (string.IsNullOrWhiteSpace(filter.Extension) ||
                 string.Equals(observation.Extension, filter.Extension.Trim(),
                     StringComparison.OrdinalIgnoreCase)) &&
                (filter.Kind is null || observation.Kind == filter.Kind) &&
                (string.IsNullOrWhiteSpace(filter.SearchText) ||
                 observation.RelativePath.Contains(filter.SearchText.Trim(),
                     StringComparison.OrdinalIgnoreCase)))
            .OrderBy(observation => observation.RelativePath, StringComparer.Ordinal)
            .ThenBy(observation => observation.ObservationId, StringComparer.Ordinal)
            .ToArray();

        // Use long multiplication: a hostile page index cannot overflow into page zero.
        var start = (long)filter.PageIndex * filter.PageSize;
        var rows = start >= matching.Length
            ? Array.Empty<InventoryObservationRow>()
            : matching.Skip((int)start).Take(filter.PageSize)
                .Select(observation => new InventoryObservationRow(
                    observation, FilenameLabel(observation.RelativePath)))
                .ToArray();

        // Avoid exposing a mutable array through IReadOnlyList (which permits a cast).
        return new(snapshot, Array.AsReadOnly(rows), matching.Length, filter.PageIndex, filter.PageSize);
    }

    private static string FilenameLabel(string relativePath)
    {
        // RelativePath is a normalized ROMS/... string; avoid reading any source.
        var index = relativePath.LastIndexOf('/');
        return index < 0 ? relativePath : relativePath[(index + 1)..];
    }
}
