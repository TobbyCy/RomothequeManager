using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>A run-scoped, unapproved local cover choice with source evidence.
/// This is not a game ID, saved decision or permission to open the image.</summary>
internal sealed record IdealMediaCoverProposal(
    string RomObservationId, string MediaObservationId, MediaUiState State,
    string RomRelativePath, string MediaRelativePath, string PlatformKey,
    IReadOnlyList<string> Provenance, IReadOnlyList<string> Evidence,
    IReadOnlyList<string> Conflicts, string RomScanId, string MediaRunId,
    string MediaRootRef, bool RequiresExplicitChoice)
{
    /// <summary>Label built only from file observations and provenance, not XML game titles.</summary>
    public string Label => PlatformKey + " · " + RomRelativePath + " → " +
        MediaRelativePath + " [" + State + "]";
    public override string ToString() => Label;
}

/// <summary>Pure, zero-I/O adapter for local cover proposals produced by the
/// scoped media matcher. XML media claims are optional; the matcher can produce
/// CANDIDATE suggestions from platform plus exact filename stem alone.</summary>
internal static class IdealMediaCoverProposals
{
    internal static IReadOnlyList<IdealMediaCoverProposal> Build(
        InventorySnapshot roms, MediaIndexSnapshot medias, MediaUiSnapshot projected)
    {
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(medias);
        ArgumentNullException.ThrowIfNull(projected);

        // A stale, incomplete or cross-root generation cannot offer an image.
        if (roms.ScanStatus != InventoryScanStatus.Complete ||
            medias.ScanStatus != InventoryScanStatus.Complete ||
            roms.Availability != InventoryAvailability.Online ||
            medias.Availability != InventoryAvailability.Online ||
            roms.Freshness != InventoryFreshness.Fresh ||
            medias.Freshness != InventoryFreshness.Fresh ||
            projected.RomScanId != roms.ScanId ||
            projected.MediaRunId != medias.RunId ||
            projected.MediaRootRef != medias.MediaRootRef)
            return Array.Empty<IdealMediaCoverProposal>();

        var observedRoms = roms.Observations
            .GroupBy(x => x.ObservationId, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);
        var observedMedia = medias.Observations
            .GroupBy(x => x.MediaObservationId, StringComparer.Ordinal)
            .Where(g => g.Count() == 1)
            .ToDictionary(g => g.Key, g => g.Single(), StringComparer.Ordinal);

        var result = new List<IdealMediaCoverProposal>();
        foreach (var row in projected.Rows)
        {
            if (row.State is not (MediaUiState.REFERENCED or MediaUiState.CANDIDATE
                    or MediaUiState.AMBIGUOUS) ||
                row.RomScanId != roms.ScanId || row.MediaRunId != medias.RunId ||
                row.MediaRootRef != medias.MediaRootRef ||
                !observedRoms.TryGetValue(row.RomObservationId, out var rom))
                continue;
            foreach (var id in row.CandidateMediaObservationIds.Distinct(StringComparer.Ordinal))
            {
                if (!observedMedia.TryGetValue(id, out var media) ||
                    media.Kind != MediaKind.Cover || !media.HeaderValidated ||
                    media.Availability != InventoryAvailability.Online ||
                    !string.Equals(media.PlatformKey, rom.PlatformKey,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                result.Add(new IdealMediaCoverProposal(
                    rom.ObservationId, media.MediaObservationId, row.State,
                    rom.RelativePath, media.RelativePath, rom.PlatformKey,
                    row.SourceProvenance, row.Evidence, row.Conflicts, roms.ScanId,
                    medias.RunId, medias.MediaRootRef,
                    row.State == MediaUiState.AMBIGUOUS ||
                    row.CandidateMediaObservationIds.Count != 1));
            }
        }
        return Array.AsReadOnly(result.OrderBy(x => x.PlatformKey, StringComparer.Ordinal)
            .ThenBy(x => x.RomRelativePath, StringComparer.Ordinal)
            .ThenBy(x => x.MediaRelativePath, StringComparer.Ordinal)
            .ThenBy(x => x.MediaObservationId, StringComparer.Ordinal).ToArray());
    }
}
