using System.Collections.ObjectModel;

namespace RomothequeManager.V2.Inventory.Media;

/// <summary>Evidence-oriented UI states. None denotes a user-approved cover or identified game.</summary>
public enum MediaUiState
{
    /// <summary>Observed media without a suggested ROM pairing; no cover is assigned automatically.</summary>
    FOUND_UNLINKED,
    /// <summary>Unapproved, source-scoped naming suggestion; the user has not confirmed an association.</summary>
    CANDIDATE,
    /// <summary>Explicit sourced path reference under comparable roots; not proof of game identity or cover suitability.</summary>
    REFERENCED,
    /// <summary>Multiple or contradictory candidate matches requiring independent human review.</summary>
    AMBIGUOUS,
    /// <summary>No candidate under a genuinely complete comparable scope; not proof of global file removal.</summary>
    ABSENT_IN_COMPLETE_SCOPE,
    /// <summary>Root, generation, availability or completeness evidence is insufficient for meaningful comparison.</summary>
    NOT_COMPARABLE
}

/// <summary>One observed ROM's unapproved media candidates and provenance.</summary>
/// <remarks>Identifiers are per-scan observations and must not be stored as SQLite GameId,
/// MediaAssetId or UserDecisionId. A REFERENCED row only proves a sourced path claim.</remarks>
public sealed record MediaUiRow(
    string RomObservationId, MediaUiState State,
    IReadOnlyList<string> CandidateMediaObservationIds,
    IReadOnlyList<string> Evidence, IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> SourceProvenance, string RuleVersion,
    string RomScanId, string MediaRunId, string MediaRootRef);

/// <summary>A media observation not proposed for any ROM; never an automatic link.</summary>
public sealed record UnlinkedMediaRow(
    string MediaObservationId, MediaUiState State, string RelativePath,
    MediaKind Kind, MediaImageFormat Format, string SourceKind,
    string MediaRunId, string MediaRootRef);

/// <summary>Immutable projection for a later ViewModel; no Avalonia objects or filesystem paths.</summary>
/// <remarks>Selection of Current versus LastAttempt belongs to the caller, not this snapshot.
/// A complete comparable empty scope permits only a LOCAL absence observation.</remarks>
public sealed record MediaUiSnapshot(
    string RomScanId, string MediaRunId, string MediaRootRef,
    InventoryScanStatus RomScanStatus, InventoryScanStatus MediaScanStatus,
    IReadOnlyList<MediaUiRow> Rows,
    IReadOnlyList<UnlinkedMediaRow> UnlinkedMedias,
    IReadOnlyList<InventoryDiagnostic> Diagnostics);

/// <summary>Pure service creating presentation evidence from independent ROM and MEDIAS scans.</summary>
public interface IMediaPresentationService
{
    /// <summary>Project without I/O, XML import, scanning, guessing game IDs or saving decisions.</summary>
    MediaUiSnapshot Build(InventorySnapshot roms, MediaIndexSnapshot media,
        VerifiedScopeMapping mapping, IReadOnlyList<ExplicitMediaReference>? claims = null);
}

/// <summary>Default source-neutral implementation consuming the proven NEXT-05 media projection.</summary>
public sealed class MediaPresentationService : IMediaPresentationService
{
    /// <summary>Projects two independently captured scans into read-only media suggestions, without opening files or persisting associations.</summary>
    /// <remarks>Current versus LastAttempt selection remains the caller's responsibility; stale, incomplete and mismatched roots do not justify an ABSENT claim.</remarks>
    /// <param name="roms">Captured ROMS observations; not an authority to read their paths.</param>
    /// <param name="media">Independent captured MEDIAS observations; not a preview grant.</param>
    /// <param name="mapping">Explicit proven mapping between matching scan generations and roots.</param>
    /// <param name="claims">Optional sourced references, never implicit approval.</param>
    /// <returns>Run-bound projection retaining diagnostics and unlinked media evidence.</returns>
    public MediaUiSnapshot Build(InventorySnapshot roms, MediaIndexSnapshot media,
        VerifiedScopeMapping mapping, IReadOnlyList<ExplicitMediaReference>? claims = null)
    {
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(mapping);
        var links = MediaLinkProjection.Reconcile(roms, media, mapping, claims);
        static IReadOnlyList<T> Immutable<T>(IEnumerable<T> source) =>
            Array.AsReadOnly(source.ToArray());

        var linkedIds = new HashSet<string>(StringComparer.Ordinal);
        var rows = links.Select(link =>
        {
            foreach (var id in link.CandidateMediaObservationIds) linkedIds.Add(id);
            var state = link.Status switch
            {
                MediaLinkStatus.EXACT_REFERENCE => MediaUiState.REFERENCED,
                MediaLinkStatus.UNIQUE_CONVENTION_CANDIDATE => MediaUiState.CANDIDATE,
                MediaLinkStatus.AMBIGUOUS => MediaUiState.AMBIGUOUS,
                MediaLinkStatus.UNMATCHED => MediaUiState.ABSENT_IN_COMPLETE_SCOPE,
                _ => MediaUiState.NOT_COMPARABLE
            };
            return new MediaUiRow(link.RomObservationId, state,
                Immutable(link.CandidateMediaObservationIds),
                Immutable(link.Evidence), Immutable(link.Conflicts),
                Immutable(link.SourceProvenance), link.RuleVersion,
                roms.ScanId, media.RunId, media.MediaRootRef);
        }).ToArray();

        // Every unproposed observation belongs to the independent gallery;
        // no implicit association is constructed, even when a filename resembles a ROM.
        var unlinked = media.Observations
            .Where(x => !linkedIds.Contains(x.MediaObservationId))
            .OrderBy(x => x.RelativePath, StringComparer.Ordinal)
            .ThenBy(x => x.MediaObservationId, StringComparer.Ordinal)
            .Select(x => new UnlinkedMediaRow(x.MediaObservationId,
                MediaUiState.FOUND_UNLINKED, x.RelativePath, x.Kind,
                x.DetectedFormat, x.SourceKind, media.RunId, media.MediaRootRef))
            .ToArray();
        return new MediaUiSnapshot(roms.ScanId, media.RunId, media.MediaRootRef,
            roms.ScanStatus, media.ScanStatus, Array.AsReadOnly(rows),
            Array.AsReadOnly(unlinked),
            Immutable(roms.Diagnostics.Concat(media.Diagnostics)));
    }
}
