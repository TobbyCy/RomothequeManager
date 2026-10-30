using System.Collections.ObjectModel;

namespace RomothequeManager.V2.Inventory.Media;

/// <summary>Limited evidence status for a ROM observation to local media, never a game ID.</summary>
public enum MediaLinkStatus
{
    EXACT_REFERENCE,
    UNIQUE_CONVENTION_CANDIDATE,
    AMBIGUOUS,
    UNMATCHED,
    NOT_COMPARABLE
}

/// <summary>Explicitly asserted relationship between captured scan roots and platform aliases.</summary>
/// <remarks>Caller must verify the selected roots independently. Matching run/root references
/// prevents stale cross-generation data; this object is not a persistent VolumeBinding.
/// A naming convention is a candidate generator, never an implicit user decision.</remarks>
public sealed record VerifiedScopeMapping(
    string RomScanId, string MediaScanId, string MediaRootRef,
    IReadOnlyDictionary<string, string> RomToMediaPlatform,
    string Provenance)
{
    /// <summary>Create a snapshot-bound mapping after explicit root selection and verification.</summary>
    public static VerifiedScopeMapping FromExplicitSelections(
        InventorySnapshot roms, MediaIndexSnapshot media,
        IReadOnlyDictionary<string, string> romToMediaPlatform,
        string provenance)
    {
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(romToMediaPlatform);
        if (string.IsNullOrWhiteSpace(provenance))
            throw new ArgumentException("Explicit mapping provenance required.", nameof(provenance));
        if (romToMediaPlatform.Any(kv => string.IsNullOrWhiteSpace(kv.Key) ||
            string.IsNullOrWhiteSpace(kv.Value) ||
            string.Equals(kv.Key, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kv.Value, "UNKNOWN", StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Mapping must use known explicit platforms.", nameof(romToMediaPlatform));
        return new VerifiedScopeMapping(roms.ScanId, media.RunId, media.MediaRootRef,
            new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string>(romToMediaPlatform, StringComparer.OrdinalIgnoreCase)),
            provenance);
    }
}

/// <summary>Optional, sourced and explicitly scoped reference, independent of ES-DE.</summary>
/// <remarks>This claim alone does not prove media identity, game identity or a user decision.
/// Paths are interpreted strictly as MEDIAS-relative observed paths.</remarks>
public sealed record ExplicitMediaReference(
    string RomObservationId, string MediaRelativePath,
    string SourceKind, string Provenance);

/// <summary>Pure, explainable suggestion or precise reference for one observed ROM file.</summary>
/// <remarks>All IDs are scan-scoped. No persistent GameId, MediaAssetId or UserDecision
/// is created; candidates must never automatically populate an approved cover.</remarks>
public sealed record MediaCandidateLink(
    string RomObservationId, MediaLinkStatus Status,
    IReadOnlyList<string> CandidateMediaObservationIds,
    IReadOnlyList<string> Evidence, IReadOnlyList<string> Conflicts,
    IReadOnlyList<string> SourceProvenance, string RuleVersion);

/// <summary>Pure ROM ↔ media reconciliation over two independently captured snapshots.</summary>
/// <remarks>Never accesses files, XML, scrapers, SQLite or any network. Exact reference
/// means an observed path agrees with a sourced claim within explicit scope/platform
/// proof; it does not certify commercial game identity or a usable cover.</remarks>
public static class MediaLinkProjection
{
    private const string Rule = "media-link/1";
    private static readonly string[] AmbiguousTokens =
        ["(USA", "(EU", "(Europe", "(JP", "(Japan", "(Disc", "(Disk", "(CD", "(Rev",
         "(Beta", "(Proto", "(Demo", "(Version", "(v1", "(v2", "(v3"];

    /// <summary>Return one immutable suggestion per eligible ROM observation.</summary>
    /// <exception cref="ArgumentNullException">A required snapshot or mapping was not supplied.</exception>
    public static IReadOnlyList<MediaCandidateLink> Reconcile(
        InventorySnapshot roms, MediaIndexSnapshot media, VerifiedScopeMapping mapping,
        IReadOnlyList<ExplicitMediaReference>? references = null)
    {
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(mapping);
        references ??= Array.Empty<ExplicitMediaReference>();

        var comparable = roms.ScanStatus == InventoryScanStatus.Complete &&
            media.ScanStatus == InventoryScanStatus.Complete &&
            roms.Availability == InventoryAvailability.Online &&
            media.Availability == InventoryAvailability.Online &&
            roms.Freshness == InventoryFreshness.Fresh &&
            media.Freshness == InventoryFreshness.Fresh &&
            mapping.RomScanId == roms.ScanId && mapping.MediaScanId == media.RunId &&
            mapping.MediaRootRef == media.MediaRootRef &&
            !string.IsNullOrWhiteSpace(mapping.Provenance);

        var eligible = roms.Observations
            .Where(o => o.Kind is InventoryKind.PotentialRom or InventoryKind.ArchiveCandidate or
                        InventoryKind.Playlist)
            .OrderBy(o => o.RelativePath, StringComparer.Ordinal)
            .ThenBy(o => o.ObservationId, StringComparer.Ordinal)
            .ToArray();
        var mediaByPath = media.Observations
            .Where(m => m.HeaderValidated && m.Kind == MediaKind.Cover)
            .GroupBy(m => m.RelativePath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        var mediaByKey = media.Observations
            .Where(m => m.HeaderValidated && m.Kind == MediaKind.Cover &&
                        !string.Equals(m.PlatformKey, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            .GroupBy(m => Key(m.PlatformKey, Stem(m.RelativePath)), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var romByKey = eligible
            .Where(r => !string.Equals(r.PlatformKey, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            .GroupBy(r => Key(r.PlatformKey, Stem(r.RelativePath)), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);
        var referenceUse = references
            .GroupBy(r => r.MediaRelativePath, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Select(r => r.RomObservationId).Distinct().Count(),
                StringComparer.Ordinal);

        static IReadOnlyList<string> ReadOnly(IEnumerable<string> values) =>
            Array.AsReadOnly(values.Distinct(StringComparer.Ordinal)
                .OrderBy(s => s, StringComparer.Ordinal).ToArray());

        var result = new List<MediaCandidateLink>(eligible.Length);
        foreach (var rom in eligible)
        {
            var evidence = new List<string>();
            var conflicts = new List<string>();
            var provenance = new List<string> { "FILESYSTEM_OBSERVATION", "FILESYSTEM_MEDIA" };
            var matches = new List<MediaObservation>();
            var status = MediaLinkStatus.UNMATCHED;
            if (!comparable ||
                string.Equals(rom.PlatformKey, "UNKNOWN", StringComparison.OrdinalIgnoreCase) ||
                !mapping.RomToMediaPlatform.TryGetValue(rom.PlatformKey, out var mediaPlatform) ||
                string.IsNullOrWhiteSpace(mediaPlatform))
            {
                status = MediaLinkStatus.NOT_COMPARABLE;
                conflicts.Add("NO_COMPARABLE_COMPLETE_SCOPED_MAPPING");
            }
            else
            {
                var stem = Stem(rom.RelativePath);
                var key = Key(mediaPlatform, stem);
                var romKey = Key(rom.PlatformKey, stem);
                var conventions = mediaByKey.GetValueOrDefault(key) ?? Array.Empty<MediaObservation>();
                var scopedReferences = references.Where(r =>
                    string.Equals(r.RomObservationId, rom.ObservationId, StringComparison.Ordinal)).ToArray();
                var exact = new List<MediaObservation>();
                foreach (var claim in scopedReferences)
                {
                    if (!mediaByPath.TryGetValue(claim.MediaRelativePath, out var referenced) ||
                        referenced.Length != 1 ||
                        !string.Equals(referenced[0].PlatformKey, mediaPlatform,
                            StringComparison.OrdinalIgnoreCase) ||
                        referenceUse.GetValueOrDefault(claim.MediaRelativePath) != 1 ||
                        string.IsNullOrWhiteSpace(claim.SourceKind) ||
                        string.IsNullOrWhiteSpace(claim.Provenance))
                    {
                        conflicts.Add("STALE_OR_CONFLICTING_EXPLICIT_REFERENCE");
                        continue;
                    }
                    exact.Add(referenced[0]);
                    provenance.Add(claim.SourceKind + ":" + claim.Provenance);
                }
                matches.AddRange(exact);
                if (exact.Count == 1 && scopedReferences.Length == 1)
                {
                    status = MediaLinkStatus.EXACT_REFERENCE;
                    evidence.Add("EXPLICIT_RELATIVE_PATH_AND_PLATFORM_AND_SCOPE");
                    provenance.Add(mapping.Provenance);
                }
                else if (exact.Count > 1 || (exact.Count == 1 && scopedReferences.Length > 1))
                {
                    status = MediaLinkStatus.AMBIGUOUS;
                    conflicts.Add("MULTIPLE_OR_CONFLICTING_REFERENCES");
                }
                else if (conventions.Length > 0)
                {
                    matches.AddRange(conventions);
                    var collision = conventions.Length != 1 ||
                        romByKey.GetValueOrDefault(romKey)?.Length != 1 ||
                        rom.Kind != InventoryKind.PotentialRom ||
                        AmbiguousTokens.Any(t => stem.Contains(t, StringComparison.OrdinalIgnoreCase)) ||
                        scopedReferences.Length > 0 ||
                        conventions.Any(c => c.RelativePath.Contains("/UNKNOWN/", StringComparison.OrdinalIgnoreCase));
                    if (collision)
                    {
                        status = MediaLinkStatus.AMBIGUOUS;
                        conflicts.Add("COLLIDING_MEDIA_OR_ROM_VARIANT_OR_STALE_CLAIM");
                    }
                    else
                    {
                        status = MediaLinkStatus.UNIQUE_CONVENTION_CANDIDATE;
                        evidence.Add("CONFIGURED_PLATFORM_AND_COVER_FOLDER_AND_UNIQUE_FILENAME");
                    }
                    provenance.Add(mapping.Provenance);
                }
                else if (scopedReferences.Length > 0)
                {
                    status = MediaLinkStatus.AMBIGUOUS;
                    conflicts.Add("STALE_REFERENCE_REQUIRES_REVIEW");
                }
                else
                    evidence.Add("NO_MATCH_WITHIN_COMPLETE_SELECTED_SCOPE");
            }
            result.Add(new MediaCandidateLink(rom.ObservationId, status,
                ReadOnly(matches.Select(m => m.MediaObservationId)),
                ReadOnly(evidence), ReadOnly(conflicts), ReadOnly(provenance), Rule));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static string Stem(string path) =>
        Path.GetFileNameWithoutExtension(path).Normalize(System.Text.NormalizationForm.FormC);

    private static string Key(string platform, string stem) =>
        platform.Normalize(System.Text.NormalizationForm.FormC) + "\u001f" + stem;
}
