using System.Globalization;
using System.Text.Json;

namespace RomothequeManager.V2;

// Experimental read-only presentation DTO. This is not a new engine or persisted schema.
public sealed class UiProjectionEnvelope
{
    public UiRegistry Registry { get; init; } = new();
    public List<UiComparison> Comparisons { get; init; } = [];
}

public sealed class UiRegistry
{
    public string SchemaVersion { get; init; } = "";
    public bool ReadOnly { get; init; }
    public List<UiLibrary> Libraries { get; init; } = [];
}

public sealed class UiSource
{
    public string SourceKey { get; init; } = "";
    public string RootKey { get; init; } = "";
    public List<string> Domains { get; init; } = [];
}

public sealed class UiCoverage
{
    public string Console { get; init; } = "";
    public string Status { get; init; } = "";
    public string Reason { get; init; } = "";
}

public sealed class UiCoverageProof
{
    public string Console { get; init; } = "";
    public string SourceKey { get; init; } = "";
    public string RootKey { get; init; } = "";
    public string Domain { get; init; } = "";
    public string Status { get; init; } = "";
}

public sealed class UiGeneration
{
    public string SnapshotId { get; init; } = "";
    public string GenerationId { get; init; } = "";
    public string ScanId { get; init; } = "";
    public string CapturedAtUtc { get; init; } = "";
    public string SchemaVersion { get; init; } = "";
    public string ScanStatus { get; init; } = "";
    public bool? ScopeIsExhaustive { get; init; }
    public List<UiCoverage> Coverage { get; init; } = [];
    public List<UiCoverageProof> CoverageProof { get; init; } = [];
    public bool CountKnown { get; init; }
    public int? ObservedUnitCount { get; init; }
    public string Provenance { get; init; } = "";
}

public sealed class UiLibrary
{
    public string LibraryId { get; init; } = "";
    public string Name { get; init; } = "";
    public List<string> Platforms { get; init; } = [];
    public List<UiSource> Sources { get; init; } = [];
    public string CurrentAvailability { get; init; } = "";
    public string? AvailabilityObservedAtUtc { get; init; }
    public string Freshness { get; init; } = "";
    public bool IndexAccessible { get; init; }
    public string? LastVerifiedCompleteRef { get; init; }
    public string? LastObservedAtUtc { get; init; }
    public List<UiGeneration> Generations { get; init; } = [];
}

public sealed class UiComparisonLibrary
{
    public string LibraryId { get; init; } = "";
    public string Name { get; init; } = "";
    public string? LastVerifiedCompleteRef { get; init; }
}

public sealed class UiScope
{
    public string Console { get; init; } = "";
    public string Status { get; init; } = "";
    public string Reason { get; init; } = "";
    public string BeforeStatus { get; init; } = "";
    public string AfterStatus { get; init; } = "";
}

public sealed class UiComparisonEntry
{
    public string RomUnitId { get; init; } = "";
    public string GameId { get; init; } = "";
    public string VariantId { get; init; } = "";
    public string Console { get; init; } = "";
    public string? BeforeDigest { get; init; }
    public string? AfterDigest { get; init; }
    public string Reason { get; init; } = "";
    public string Classification { get; init; } = "";
}

public sealed class UiComparison
{
    public string SchemaVersion { get; init; } = "";
    public bool ReadOnly { get; init; }
    public UiComparisonLibrary Library { get; init; } = new();
    public UiGeneration Before { get; init; } = new();
    public UiGeneration After { get; init; } = new();
    public List<UiScope> ScopeCompared { get; init; } = [];
    public List<UiScope> ScopeNotComparable { get; init; } = [];
    public string CoverageStatus { get; init; } = "";
    public Dictionary<string, int> Counts { get; init; } = new();
    public List<UiComparisonEntry> Entries { get; init; } = [];
    public int CountDenominator { get; init; }
    public List<string> Limitations { get; init; } = [];
}

public sealed class LibraryProjection
{
    public const string SyntheticSchema = "0.1.0-synthetic";
    public static readonly IReadOnlyList<string> Classifications =
    [
        "ADDED", "CHANGED", "UNCHANGED", "NOT_OBSERVED_IN_B_COVERED_SCOPE",
        "UNKNOWN", "NOT_COMPARABLE"
    ];

    private readonly UiProjectionEnvelope _data;
    public IReadOnlyList<UiLibrary> Libraries => _data.Registry.Libraries;
    public IReadOnlyList<UiComparison> Comparisons => _data.Comparisons;

    private LibraryProjection(UiProjectionEnvelope data) => _data = data;

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

    public UiLibrary? FindLibrary(string? libraryId) => Libraries.FirstOrDefault(
        item => string.Equals(item.LibraryId, libraryId, StringComparison.Ordinal));

    public UiComparison? FindComparison(string libraryId, string beforeId, string afterId) =>
        Comparisons.FirstOrDefault(item =>
            string.Equals(item.Library.LibraryId, libraryId, StringComparison.Ordinal) &&
            string.Equals(item.Before.SnapshotId, beforeId, StringComparison.Ordinal) &&
            string.Equals(item.After.SnapshotId, afterId, StringComparison.Ordinal));

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
            if (library.LastObservedAtUtc is not null) RequireUtc(library.LastObservedAtUtc, "lastObservedAtUtc");
            if (library.Platforms.Count == 0 || library.Sources.Count == 0)
                throw new ArgumentException("Library declared scope is missing.");
            var platformIds = new HashSet<string>(library.Platforms, StringComparer.Ordinal);
            if (platformIds.Count != library.Platforms.Count)
                throw new ArgumentException("Duplicate platform.");
            var sourceIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var source in library.Sources)
            {
                if (string.IsNullOrWhiteSpace(source.SourceKey) || string.IsNullOrWhiteSpace(source.RootKey) ||
                    !sourceIds.Add(source.SourceKey) || source.Domains.Count == 0 ||
                    source.Domains.Any(domain => !Choice(domain, "ROMS", "MEDIAS")))
                    throw new ArgumentException("Invalid source scope.");
            }
            var snapshotIds = new HashSet<string>(StringComparer.Ordinal);
            var generationIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var generation in library.Generations)
            {
                if (string.IsNullOrWhiteSpace(generation.SnapshotId) ||
                    string.IsNullOrWhiteSpace(generation.GenerationId) ||
                    string.IsNullOrWhiteSpace(generation.ScanId) ||
                    generation.SnapshotId == generation.GenerationId ||
                    !snapshotIds.Add(generation.SnapshotId) ||
                    !generationIds.Add(generation.GenerationId))
                    throw new ArgumentException("Invalid snapshot/generation identity.");
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
                var coveredPlatforms = new HashSet<string>(StringComparer.Ordinal);
                foreach (var coverage in generation.Coverage)
                {
                    if (!platformIds.Contains(coverage.Console) || !coveredPlatforms.Add(coverage.Console) ||
                        !Choice(coverage.Status, "complete", "partial", "unobserved") ||
                        string.IsNullOrWhiteSpace(coverage.Reason))
                        throw new ArgumentException("Invalid coverage declaration.");
                    if (coverage.Status == "complete")
                    {
                        if (generation.ScanStatus != "complete" || generation.ScopeIsExhaustive != true ||
                            !generation.CoverageProof.Any(proof =>
                                proof.Console == coverage.Console && proof.Status == "complete"))
                            throw new ArgumentException("Unproven complete coverage.");
                    }
                }
                if (coveredPlatforms.Count != platformIds.Count)
                    throw new ArgumentException("Undeclared platform coverage.");
            }
            if (library.LastVerifiedCompleteRef is not null)
            {
                var verified = library.Generations.FirstOrDefault(g =>
                    string.Equals(g.SnapshotId, library.LastVerifiedCompleteRef, StringComparison.Ordinal));
                if (verified is null || verified.ScanStatus != "complete" ||
                    verified.ScopeIsExhaustive != true ||
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
            var before = library.Generations.FirstOrDefault(item =>
                string.Equals(item.SnapshotId, comparison.Before.SnapshotId, StringComparison.Ordinal));
            var after = library.Generations.FirstOrDefault(item =>
                string.Equals(item.SnapshotId, comparison.After.SnapshotId, StringComparison.Ordinal));
            if (before is null || after is null || before.SnapshotId == after.SnapshotId ||
                before.GenerationId != comparison.Before.GenerationId ||
                after.GenerationId != comparison.After.GenerationId ||
                RequireUtc(before.CapturedAtUtc, "before") > RequireUtc(after.CapturedAtUtc, "after"))
                throw new ArgumentException("Invalid A/B snapshot references or chronology.");
            var key = library.LibraryId + "\u001f" + before.SnapshotId + "\u001f" + after.SnapshotId;
            if (!comparisonKeys.Add(key)) throw new ArgumentException("Duplicate A/B comparison.");
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
                    !unitIds.Add(entry.RomUnitId) ||
                    !Classifications.Contains(entry.Classification, StringComparer.Ordinal) ||
                    string.IsNullOrWhiteSpace(entry.Reason) ||
                    !library.Platforms.Contains(entry.Console, StringComparer.Ordinal))
                    throw new ArgumentException("Invalid comparison entry.");
            }
            if (comparison.CoverageStatus == "COMPLETE" && comparison.ScopeNotComparable.Count != 0)
                throw new ArgumentException("Unproven complete comparison coverage.");
            if (comparison.CoverageStatus == "INCOMPLETE" && comparison.ScopeNotComparable.Count == 0)
                throw new ArgumentException("Incomplete comparison must expose scope limitations.");
        }
    }
}
