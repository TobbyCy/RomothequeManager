using System.Globalization;
using System.Text.Json;

namespace RomothequeManager.V2;

// Experimental read-only presentation DTO. This is not a new engine or persisted schema.
/// <summary>Envelope of the synthetic read-only registry and its comparison evidence; it is not a persistent catalogue contract.</summary>
public sealed class UiProjectionEnvelope
{
    /// <summary>Read-only registry of declared libraries, their partitions and observed generations.</summary>
    public UiRegistry Registry { get; init; } = new();
    /// <summary>Available A/B comparisons whose evidence is checked against the same registry.</summary>
    public List<UiComparison> Comparisons { get; init; } = [];
}

/// <summary>Registry of declared demo libraries used to validate coverage and availability without touching user media.</summary>
public sealed class UiRegistry
{
    /// <summary>Version of the in-memory synthetic projection contract; unsupported versions are rejected.</summary>
    public string SchemaVersion { get; init; } = "";
    /// <summary>Explicit declaration that this synthetic projection may not authorize any write operation.</summary>
    public bool ReadOnly { get; init; }
    /// <summary>Declared library entries whose identifiers must be unique and match the demo catalogue.</summary>
    public List<UiLibrary> Libraries { get; init; } = [];
}

/// <summary>Declares a stable source and root partition together with the observation domains it may cover.</summary>
public sealed class UiSource
{
    /// <summary>Stable source partition key; kept distinct from the root and observation domain.</summary>
    public string SourceKey { get; init; } = "";
    /// <summary>Root partition key attached to a declared source, not an authorization to access a real path.</summary>
    public string RootKey { get; init; } = "";
    /// <summary>Permitted observation domains for this source: ROMS or MEDIAS; duplicates are invalid.</summary>
    public List<string> Domains { get; init; } = [];
}

/// <summary>Per-platform completeness claim derived from partition-level observations rather than inferred from filenames.</summary>
public sealed class UiCoverage
{
    /// <summary>Platform scope to which this coverage, comparison or entry refers.</summary>
    public string Console { get; init; } = "";
    /// <summary>Evidence-derived coverage or comparability status; unknown coverage is not treated as complete.</summary>
    public string Status { get; init; } = "";
    /// <summary>Human-readable explanation of the observation, limitation or classification without changing its evidence.</summary>
    public string Reason { get; init; } = "";
}

/// <summary>Evidence for the coverage of one console, source, root and domain partition in a captured generation.</summary>
public sealed class UiCoverageProof
{
    /// <summary>Platform scope to which this coverage, comparison or entry refers.</summary>
    public string Console { get; init; } = "";
    /// <summary>Stable source partition key; kept distinct from the root and observation domain.</summary>
    public string SourceKey { get; init; } = "";
    /// <summary>Root partition key attached to a declared source, not an authorization to access a real path.</summary>
    public string RootKey { get; init; } = "";
    /// <summary>Observation domain of the partition whose completeness is being asserted.</summary>
    public string Domain { get; init; } = "";
    /// <summary>Evidence-derived coverage or comparability status; unknown coverage is not treated as complete.</summary>
    public string Status { get; init; } = "";
}

/// <summary>Captured library generation with scan status, partition proofs and explicit count-known semantics.</summary>
public sealed class UiGeneration
{
    /// <summary>Stable identity of the captured snapshot used as a reference in validated A/B comparisons.</summary>
    public string SnapshotId { get; init; } = "";
    /// <summary>Identity of the generation, distinct from snapshot and scan identifiers.</summary>
    public string GenerationId { get; init; } = "";
    /// <summary>Identity of the scan attempt from which the generation's evidence was captured.</summary>
    public string ScanId { get; init; } = "";
    /// <summary>Timestamp of the observation in UTC; validation rejects non-UTC or malformed values.</summary>
    public string CapturedAtUtc { get; init; } = "";
    /// <summary>Version of the in-memory synthetic projection contract; unsupported versions are rejected.</summary>
    public string SchemaVersion { get; init; } = "";
    /// <summary>Observed scan outcome: complete, partial or failed; it does not alone prove exhaustive coverage.</summary>
    public string ScanStatus { get; init; } = "";
    /// <summary>Whether the selected scope was exhaustively observed; a complete comparison requires this evidence.</summary>
    public bool? ScopeIsExhaustive { get; init; }
    /// <summary>Platform-level coverage obtained from partition proofs, never inferred from item counts.</summary>
    public List<UiCoverage> Coverage { get; init; } = [];
    /// <summary>Complete partition-level observation proofs, including explicitly unobserved partitions.</summary>
    public List<UiCoverageProof> CoverageProof { get; init; } = [];
    /// <summary>Whether a verified count may be shown; false requires the observed count to remain null.</summary>
    public bool CountKnown { get; init; }
    /// <summary>Count of observed units only when the index is accessible and a count is known; not a game-identity guarantee.</summary>
    public int? ObservedUnitCount { get; init; }
    /// <summary>Source description explaining the origin of an observation or explicit scoped mapping.</summary>
    public string Provenance { get; init; } = "";
}

/// <summary>Synthetic library identity and its observed generations, availability and asserted partition scope.</summary>
public sealed class UiLibrary
{
    /// <summary>Stable identity joining a synthetic library to catalogue and comparison evidence.</summary>
    public string LibraryId { get; init; } = "";
    /// <summary>User-visible library label paired with its stable identity for consistency checks.</summary>
    public string Name { get; init; } = "";
    /// <summary>Declared set of platforms; omitted or duplicated platform scope invalidates the projection.</summary>
    public List<string> Platforms { get; init; } = [];
    /// <summary>Declared observation roots and domains used to construct the required coverage partitions.</summary>
    public List<UiSource> Sources { get; init; } = [];
    /// <summary>Latest observed availability of the library, independently of the age of stored snapshots.</summary>
    public string CurrentAvailability { get; init; } = "";
    /// <summary>UTC time at which availability was observed; unknown availability has no such timestamp.</summary>
    public string? AvailabilityObservedAtUtc { get; init; }
    /// <summary>Freshness of recorded evidence, separate from current availability or successful scan status.</summary>
    public string Freshness { get; init; } = "";
    /// <summary>Whether an index can support reliable counts; this must agree with CountKnown on each generation.</summary>
    public bool IndexAccessible { get; init; }
    /// <summary>Reference to a proven exhaustive complete snapshot; never replaced by a partial attempt.</summary>
    public string? LastVerifiedCompleteRef { get; init; }
    /// <summary>Most recent observation timestamp, if available; not proof of present access to the source.</summary>
    public string? LastObservedAtUtc { get; init; }
    /// <summary>Captured history of distinct scan attempts; IDs and coverage proofs are validated for coherence.</summary>
    public List<UiGeneration> Generations { get; init; } = [];
}

/// <summary>Immutable library metadata embedded in a comparison and checked against its registry entry.</summary>
public sealed class UiComparisonLibrary
{
    /// <summary>Stable identity joining a synthetic library to catalogue and comparison evidence.</summary>
    public string LibraryId { get; init; } = "";
    /// <summary>User-visible library label paired with its stable identity for consistency checks.</summary>
    public string Name { get; init; } = "";
    /// <summary>Declared set of platforms; omitted or duplicated platform scope invalidates the projection.</summary>
    public List<string> Platforms { get; init; } = [];
    /// <summary>Latest observed availability of the library, independently of the age of stored snapshots.</summary>
    public string CurrentAvailability { get; init; } = "";
    /// <summary>Freshness of recorded evidence, separate from current availability or successful scan status.</summary>
    public string Freshness { get; init; } = "";
    /// <summary>Whether an index can support reliable counts; this must agree with CountKnown on each generation.</summary>
    public bool IndexAccessible { get; init; }
    /// <summary>Reference to a proven exhaustive complete snapshot; never replaced by a partial attempt.</summary>
    public string? LastVerifiedCompleteRef { get; init; }
}

/// <summary>Comparable or excluded platform scope, including the original reasons on both sides of an A/B comparison.</summary>
public sealed class UiScope
{
    /// <summary>Platform scope to which this coverage, comparison or entry refers.</summary>
    public string Console { get; init; } = "";
    /// <summary>Evidence-derived coverage or comparability status; unknown coverage is not treated as complete.</summary>
    public string Status { get; init; } = "";
    /// <summary>Human-readable explanation of the observation, limitation or classification without changing its evidence.</summary>
    public string Reason { get; init; } = "";
    /// <summary>Coverage status of this platform in the earlier generation of an A/B comparison.</summary>
    public string BeforeStatus { get; init; } = "";
    /// <summary>Coverage status of this platform in the later generation of an A/B comparison.</summary>
    public string AfterStatus { get; init; } = "";
    /// <summary>Evidence-based limitation or coverage explanation attached to the earlier generation.</summary>
    public string BeforeReason { get; init; } = "";
    /// <summary>Evidence-based limitation or coverage explanation attached to the later generation.</summary>
    public string AfterReason { get; init; } = "";
}

/// <summary>One source-grounded comparison classification; unknown coverage must never become an asserted difference.</summary>
public sealed class UiComparisonEntry
{
    /// <summary>Stable observed ROM unit identity within this synthetic comparison, not a verified commercial game.</summary>
    public string RomUnitId { get; init; } = "";
    /// <summary>Source catalogue game identity used to correlate an observed unit without guessing from file names.</summary>
    public string GameId { get; init; } = "";
    /// <summary>Variant identity distinguishing source entries under the same game identity.</summary>
    public string VariantId { get; init; } = "";
    /// <summary>Platform scope to which this coverage, comparison or entry refers.</summary>
    public string Console { get; init; } = "";
    /// <summary>Optional earlier content digest; null does not mean the item was deleted.</summary>
    public string? BeforeDigest { get; init; }
    /// <summary>Optional later content digest; null is not proof of absence outside the covered scope.</summary>
    public string? AfterDigest { get; init; }
    /// <summary>Human-readable explanation of the observation, limitation or classification without changing its evidence.</summary>
    public string Reason { get; init; } = "";
    /// <summary>Classification derived from the paired evidence, subject to completeness and coverage checks.</summary>
    public string Classification { get; init; } = "";
}

/// <summary>Read-only A/B result with an explicit denominator, coverage exclusions and traceable limitations.</summary>
public sealed class UiComparison
{
    /// <summary>Version of the in-memory synthetic projection contract; unsupported versions are rejected.</summary>
    public string SchemaVersion { get; init; } = "";
    /// <summary>Explicit declaration that this synthetic projection may not authorize any write operation.</summary>
    public bool ReadOnly { get; init; }
    /// <summary>Library metadata cross-checked against the registry before accepting this comparison.</summary>
    public UiComparisonLibrary Library { get; init; } = new();
    /// <summary>Earlier captured generation; must precede the later snapshot and match the registry exactly.</summary>
    public UiGeneration Before { get; init; } = new();
    /// <summary>Later captured generation; must match the registry exactly and maintain chronological order.</summary>
    public UiGeneration After { get; init; } = new();
    /// <summary>Platforms with proof of complete observation at both sides of the comparison.</summary>
    public List<UiScope> ScopeCompared { get; init; } = [];
    /// <summary>Platforms excluded from comparison because one or both generations lack complete coverage.</summary>
    public List<UiScope> ScopeNotComparable { get; init; } = [];
    /// <summary>Overall comparison completeness derived from its explicitly covered and excluded platforms.</summary>
    public string CoverageStatus { get; init; } = "";
    /// <summary>Per-classification tally whose sum must equal the explicit comparison denominator.</summary>
    public Dictionary<string, int> Counts { get; init; } = new();
    /// <summary>Classified observation entries checked individually against their platform and source evidence.</summary>
    public List<UiComparisonEntry> Entries { get; init; } = [];
    /// <summary>Total number of classified units, checked against entry count and every category total.</summary>
    public int CountDenominator { get; init; }
    /// <summary>Explanations of incomplete comparisons; mandatory whenever coverage cannot be established.</summary>
    public List<string> Limitations { get; init; } = [];
}

/// <summary>Validates and exposes the synthetic library registry and comparison evidence for read-only UI demonstration.</summary>
public sealed class LibraryProjection
{
    /// <summary>Only supported synthetic registry version; not a persisted user-library schema.</summary>
    public const string SyntheticSchema = "0.1.0-synthetic";
    /// <summary>Permitted A/B classifications, including UNKNOWN and NOT_COMPARABLE for insufficient evidence.</summary>
    public static readonly IReadOnlyList<string> Classifications =
    [
        "ADDED", "CHANGED", "UNCHANGED", "NOT_OBSERVED_IN_B_COVERED_SCOPE",
        "UNKNOWN", "NOT_COMPARABLE"
    ];

    private readonly UiProjectionEnvelope _data;
    /// <summary>Declared library entries whose identifiers must be unique and match the demo catalogue.</summary>
    public IReadOnlyList<UiLibrary> Libraries => _data.Registry.Libraries;
    /// <summary>Available A/B comparisons whose evidence is checked against the same registry.</summary>
    public IReadOnlyList<UiComparison> Comparisons => _data.Comparisons;

    private LibraryProjection(UiProjectionEnvelope data) => _data = data;

    /// <summary>Loads the embedded fictional library fixture and refuses a projection inconsistent with the supplied synthetic catalogue.</summary>
    public static LibraryProjection LoadSynthetic(ReadOnlyCatalogue catalogue)
    {
        var assembly = typeof(LibraryProjection).Assembly;
        var name = assembly.GetManifestResourceNames().SingleOrDefault(
            value => value.EndsWith("library-ui-v2-01b.synthetic.json", StringComparison.Ordinal));
        if (name is null) throw new InvalidOperationException("Synthetic UI fixture is not embedded.");
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException("Synthetic UI fixture cannot be opened.");
        using var reader = new StreamReader(stream);
        var projection = FromJson(reader.ReadToEnd());
        projection.AssertCatalogueCoherence(catalogue);
        return projection;
    }

    /// <summary>Parses and validates a synthetic projection, rejecting writable payloads, contradictory coverage and malformed comparison evidence.</summary>
    /// <exception cref="ArgumentException">The projection is malformed or contradicts its declared read-only evidence.</exception>
    public static LibraryProjection FromJson(string json)
    {
        UiProjectionEnvelope data;
        try
        {
            data = JsonSerializer.Deserialize<UiProjectionEnvelope>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new ArgumentException("Empty UI projection.");
        }
        catch (JsonException exception)
        {
            throw new ArgumentException("Invalid UI projection JSON.", nameof(json), exception);
        }
        Validate(data);
        return new LibraryProjection(data);
    }

    /// <summary>Looks up a declared library by exact stable identity; a missing or unknown ID returns null.</summary>
    public UiLibrary? FindLibrary(string? libraryId) => Libraries.FirstOrDefault(
        item => string.Equals(item.LibraryId, libraryId, StringComparison.Ordinal));

    /// <summary>Selects an A/B comparison by exact library and snapshot IDs; returns null when no pair matches.</summary>
    public UiComparison? FindComparison(string libraryId, string beforeId, string afterId) =>
        Comparisons.FirstOrDefault(item =>
            string.Equals(item.Library.LibraryId, libraryId, StringComparison.Ordinal) &&
            string.Equals(item.Before.SnapshotId, beforeId, StringComparison.Ordinal) &&
            string.Equals(item.After.SnapshotId, afterId, StringComparison.Ordinal));

    /// <summary>Rejects a catalogue whose library identities or display names disagree with the validated synthetic projection.</summary>
    public void AssertCatalogueCoherence(ReadOnlyCatalogue catalogue)
    {
        var games = catalogue.Games;
        var known = new HashSet<string>(Libraries.Select(item => item.LibraryId), StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var game in games)
        {
            if (!known.Contains(game.LibraryId)) throw new ArgumentException("Game libraryId is absent from UI projection.");
            var library = FindLibrary(game.LibraryId)!;
            if (!string.Equals(game.Library, library.Name, StringComparison.Ordinal))
                throw new ArgumentException("Game library name does not match its stable identity.");
            used.Add(game.LibraryId);
        }
        if (used.Count != known.Count) throw new ArgumentException("UI libraries and synthetic catalogue are inconsistent.");
    }

    private static bool Choice(string value, params string[] allowed) =>
        allowed.Contains(value, StringComparer.Ordinal);

    private static DateTimeOffset RequireUtc(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.EndsWith("Z", StringComparison.Ordinal) ||
            !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var parsed) || parsed.Offset != TimeSpan.Zero)
            throw new ArgumentException("UTC date required: " + field);
        return parsed;
    }

    // Length-prefixed, ordinal partition identity prevents source/root/domain collisions.
    private static string PartitionKey(string platform, string sourceKey, string rootKey, string domain) =>
        $"{platform.Length}:{platform}{sourceKey.Length}:{sourceKey}{rootKey.Length}:{rootKey}{domain.Length}:{domain}";

    private static HashSet<string> ExpectedPartitions(UiLibrary library)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in library.Sources)
        {
            if (string.IsNullOrWhiteSpace(source.SourceKey) || string.IsNullOrWhiteSpace(source.RootKey) ||
                !sourceIds.Add(source.SourceKey) || source.Domains.Count == 0 ||
                source.Domains.Distinct(StringComparer.Ordinal).Count() != source.Domains.Count ||
                source.Domains.Any(domain => !Choice(domain, "ROMS", "MEDIAS")))
                throw new ArgumentException("Invalid or duplicated source partition.");
            foreach (var platform in library.Platforms)
            foreach (var domain in source.Domains)
                if (!expected.Add(PartitionKey(platform, source.SourceKey, source.RootKey, domain)))
                    throw new ArgumentException("Duplicated declared partition.");
        }
        return expected;
    }

    private static void ValidateGeneration(UiLibrary library, UiGeneration generation,
        HashSet<string> expectedPartitions)
    {
        RequireUtc(generation.CapturedAtUtc, "capturedAtUtc");
        if (generation.SchemaVersion != "1.0.0" ||
            !Choice(generation.ScanStatus, "complete", "partial", "failed") ||
            generation.ScopeIsExhaustive is null ||
            string.IsNullOrWhiteSpace(generation.Provenance))
            throw new ArgumentException("Invalid scan, schema or provenance.");
        if (library.IndexAccessible != generation.CountKnown ||
            (generation.CountKnown && (generation.ObservedUnitCount is null || generation.ObservedUnitCount < 0)) ||
            (!generation.CountKnown && generation.ObservedUnitCount is not null))
            throw new ArgumentException("Unknown count must remain null.");

        var actualPartitions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var proof in generation.CoverageProof)
        {
            var key = PartitionKey(proof.Console, proof.SourceKey, proof.RootKey, proof.Domain);
            if (!Choice(proof.Status, "complete", "partial", "unobserved") ||
                !expectedPartitions.Contains(key) || !actualPartitions.Add(key))
                throw new ArgumentException("Foreign, duplicated or invalid coverage partition.");
        }
        if (!actualPartitions.SetEquals(expectedPartitions))
            throw new ArgumentException("Missing coverage partition.");

        var coveredPlatforms = new HashSet<string>(StringComparer.Ordinal);
        foreach (var coverage in generation.Coverage)
        {
            if (!library.Platforms.Contains(coverage.Console, StringComparer.Ordinal) ||
                !coveredPlatforms.Add(coverage.Console) || string.IsNullOrWhiteSpace(coverage.Reason))
                throw new ArgumentException("Invalid or duplicated console coverage.");
            var proofs = generation.CoverageProof.Where(proof => proof.Console == coverage.Console).ToArray();
            var complete = generation.ScanStatus == "complete" &&
                generation.ScopeIsExhaustive == true &&
                proofs.All(proof => proof.Status == "complete");
            var derived = complete ? "complete" :
                proofs.All(proof => proof.Status == "unobserved") ? "unobserved" : "partial";
            if (coverage.Status != derived)
                throw new ArgumentException("Console coverage contradicts partition evidence.");
        }
        if (coveredPlatforms.Count != library.Platforms.Count)
            throw new ArgumentException("Undeclared platform coverage.");
    }

    private static bool GenerationEquals(UiGeneration canonical, UiGeneration embedded) =>
        string.Equals(JsonSerializer.Serialize(canonical), JsonSerializer.Serialize(embedded),
            StringComparison.Ordinal);

    private static void Validate(UiProjectionEnvelope data)
    {
        if (data.Registry.SchemaVersion != SyntheticSchema || !data.Registry.ReadOnly)
            throw new ArgumentException("Unsupported or writable synthetic registry.");
        var libraryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var library in data.Registry.Libraries)
        {
            if (string.IsNullOrWhiteSpace(library.LibraryId) || string.IsNullOrWhiteSpace(library.Name) ||
                !libraryIds.Add(library.LibraryId))
                throw new ArgumentException("Invalid or duplicated library identity.");
            if (!Choice(library.CurrentAvailability, "ONLINE", "OFFLINE", "INACCESSIBLE", "UNKNOWN") ||
                !Choice(library.Freshness, "FRESH", "STALE", "UNKNOWN"))
                throw new ArgumentException("Invalid independent library states.");
            if (library.CurrentAvailability == "UNKNOWN")
            {
                if (library.AvailabilityObservedAtUtc is not null)
                    throw new ArgumentException("UNKNOWN availability cannot have an observation date.");
            }
            else RequireUtc(library.AvailabilityObservedAtUtc, "availabilityObservedAtUtc");
            if (library.LastObservedAtUtc is not null)
                RequireUtc(library.LastObservedAtUtc, "lastObservedAtUtc");
            if (library.Platforms.Count == 0 || library.Sources.Count == 0 ||
                library.Platforms.Distinct(StringComparer.Ordinal).Count() != library.Platforms.Count ||
                library.Platforms.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Library declared scope is missing or duplicated.");

            var expectedPartitions = ExpectedPartitions(library);
            var snapshotIds = new HashSet<string>(StringComparer.Ordinal);
            var generationIds = new HashSet<string>(StringComparer.Ordinal);
            var scanIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var generation in library.Generations)
            {
                if (string.IsNullOrWhiteSpace(generation.SnapshotId) ||
                    string.IsNullOrWhiteSpace(generation.GenerationId) ||
                    string.IsNullOrWhiteSpace(generation.ScanId) ||
                    generation.SnapshotId == generation.GenerationId ||
                    !snapshotIds.Add(generation.SnapshotId) ||
                    !generationIds.Add(generation.GenerationId) ||
                    !scanIds.Add(generation.ScanId))
                    throw new ArgumentException("Invalid snapshot/generation/scan identity.");
                ValidateGeneration(library, generation, expectedPartitions);
            }
            if (library.LastVerifiedCompleteRef is not null)
            {
                var verified = library.Generations.FirstOrDefault(g =>
                    string.Equals(g.SnapshotId, library.LastVerifiedCompleteRef, StringComparison.Ordinal));
                if (verified is null || verified.ScanStatus != "complete" ||
                    verified.ScopeIsExhaustive != true ||
                    verified.Coverage.Count != library.Platforms.Count ||
                    verified.Coverage.Any(coverage => coverage.Status != "complete"))
                    throw new ArgumentException("Last verified complete reference is not proven.");
            }
        }

        var comparisonKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var comparison in data.Comparisons)
        {
            if (comparison.SchemaVersion != SyntheticSchema || !comparison.ReadOnly ||
                !Choice(comparison.CoverageStatus, "COMPLETE", "INCOMPLETE"))
                throw new ArgumentException("Invalid comparison schema or status.");
            var library = data.Registry.Libraries.FirstOrDefault(item =>
                string.Equals(item.LibraryId, comparison.Library.LibraryId, StringComparison.Ordinal));
            if (library is null || !library.IndexAccessible)
                throw new ArgumentException("Comparison refers to inaccessible or absent library.");
            if (comparison.Library.Name != library.Name ||
                !comparison.Library.Platforms.SequenceEqual(library.Platforms, StringComparer.Ordinal) ||
                comparison.Library.CurrentAvailability != library.CurrentAvailability ||
                comparison.Library.Freshness != library.Freshness ||
                comparison.Library.IndexAccessible != library.IndexAccessible ||
                comparison.Library.LastVerifiedCompleteRef != library.LastVerifiedCompleteRef)
                throw new ArgumentException("Comparison library metadata contradicts registry.");

            var before = library.Generations.FirstOrDefault(item =>
                string.Equals(item.SnapshotId, comparison.Before.SnapshotId, StringComparison.Ordinal));
            var after = library.Generations.FirstOrDefault(item =>
                string.Equals(item.SnapshotId, comparison.After.SnapshotId, StringComparison.Ordinal));
            if (before is null || after is null || before.SnapshotId == after.SnapshotId ||
                !GenerationEquals(before, comparison.Before) ||
                !GenerationEquals(after, comparison.After) ||
                RequireUtc(before.CapturedAtUtc, "before") > RequireUtc(after.CapturedAtUtc, "after"))
                throw new ArgumentException("Contradictory A/B payload or chronology.");
            var key = library.LibraryId + "\u001f" + before.SnapshotId + "\u001f" + after.SnapshotId;
            if (!comparisonKeys.Add(key)) throw new ArgumentException("Duplicate A/B comparison.");

            var expectedCompared = new HashSet<string>(StringComparer.Ordinal);
            var expectedExcluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var platform in library.Platforms)
            {
                var a = before.Coverage.Single(coverage => coverage.Console == platform);
                var b = after.Coverage.Single(coverage => coverage.Console == platform);
                if (before.ScanStatus == "complete" && after.ScanStatus == "complete" &&
                    a.Status == "complete" && b.Status == "complete")
                    expectedCompared.Add(platform);
                else
                    expectedExcluded.Add(platform);
            }
            var observedCompared = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scope in comparison.ScopeCompared)
            {
                if (!expectedCompared.Contains(scope.Console) ||
                    !observedCompared.Add(scope.Console) ||
                    scope.Status != "complete" || string.IsNullOrWhiteSpace(scope.Reason))
                    throw new ArgumentException("Unproven, foreign or duplicated comparable scope.");
            }
            if (!observedCompared.SetEquals(expectedCompared))
                throw new ArgumentException("Missing comparable scope.");
            var observedExcluded = new HashSet<string>(StringComparer.Ordinal);
            foreach (var scope in comparison.ScopeNotComparable)
            {
                if (!expectedExcluded.Contains(scope.Console) || !observedExcluded.Add(scope.Console) ||
                    scope.Status != "INCOMPLETE")
                    throw new ArgumentException("Foreign or duplicated excluded scope.");
                var a = before.Coverage.Single(coverage => coverage.Console == scope.Console);
                var b = after.Coverage.Single(coverage => coverage.Console == scope.Console);
                if (scope.BeforeStatus != a.Status || scope.AfterStatus != b.Status ||
                    scope.BeforeReason != a.Reason || scope.AfterReason != b.Reason)
                    throw new ArgumentException("Excluded scope contradicts A/B evidence.");
            }
            if (!observedExcluded.SetEquals(expectedExcluded))
                throw new ArgumentException("Missing excluded scope.");
            var expectedStatus = expectedExcluded.Count == 0 ? "COMPLETE" : "INCOMPLETE";
            if (comparison.CoverageStatus != expectedStatus ||
                (comparison.CoverageStatus == "COMPLETE" && observedCompared.Count == 0))
                throw new ArgumentException("Unproven comparison completeness.");

            if (comparison.CountDenominator < 0 ||
                comparison.Counts.Count != Classifications.Count ||
                Classifications.Any(value => !comparison.Counts.ContainsKey(value) || comparison.Counts[value] < 0) ||
                comparison.Counts.Values.Sum() != comparison.CountDenominator ||
                comparison.Entries.Count != comparison.CountDenominator)
                throw new ArgumentException("Comparison counts/denominator inconsistent.");
            foreach (var classification in Classifications)
            {
                if (comparison.Counts[classification] !=
                    comparison.Entries.Count(entry => entry.Classification == classification))
                    throw new ArgumentException("Comparison classification count mismatch.");
            }
            var unitIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in comparison.Entries)
            {
                if (string.IsNullOrWhiteSpace(entry.RomUnitId) ||
                    string.IsNullOrWhiteSpace(entry.GameId) ||
                    string.IsNullOrWhiteSpace(entry.VariantId) ||
                    !unitIds.Add(entry.RomUnitId) ||
                    !Classifications.Contains(entry.Classification, StringComparer.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.Reason) ||
                    !library.Platforms.Contains(entry.Console, StringComparer.Ordinal))
                    throw new ArgumentException("Invalid comparison entry.");
                if (expectedExcluded.Contains(entry.Console) &&
                    !Choice(entry.Classification, "UNKNOWN", "NOT_COMPARABLE"))
                    throw new ArgumentException("Unproven scope cannot claim an observed difference.");
            }
            if (comparison.CoverageStatus == "INCOMPLETE" && comparison.Limitations.Count == 0)
                throw new ArgumentException("Incomplete comparison must disclose limitations.");
        }
    }
}
