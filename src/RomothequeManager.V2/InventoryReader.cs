using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("RomothequeManager.V2.Tests")]

namespace RomothequeManager.V2.Inventory;

// NEXT-02: opt-in filesystem observations only; no ES-DE, ROM hashing or source mutation.
// The public types are candidate v0.1 until independent DEV 2 contract review.
/// <summary>Coverage state of a bounded inventory attempt, not an assertion of ROM authenticity.</summary>
public enum InventoryScanStatus
{
    /// <summary>Selected partitions were traversed within declared limits; only a complete result can replace Current.</summary>
    Complete,
    /// <summary>Some entries were observed, but cancellation, limits or failures prevent conclusions about absent files.</summary>
    Partial,
    /// <summary>The attempt could not establish usable coverage; the previous complete snapshot must remain available.</summary>
    Failed,
}
/// <summary>Reachability of the explicitly selected local scope at observation time.</summary>
public enum InventoryAvailability
{
    /// <summary>The selected scope was reachable during this observation, not a guarantee that the device remains mounted.</summary>
    Online,
    /// <summary>The selected root was unavailable at this attempt; stored entries cannot be treated as deleted.</summary>
    Offline,
    /// <summary>The operating system refused access to the selected scope; this is not evidence of an empty collection.</summary>
    Inaccessible,
    /// <summary>No reliable availability assertion could be established from the attempted observation.</summary>
    Unknown,
}
/// <summary>Whether a snapshot can be treated as current without a new scan.</summary>
public enum InventoryFreshness
{
    /// <summary>The snapshot reflects the completed current observation within its declared capture window.</summary>
    Fresh,
    /// <summary>The saved evidence predates the present availability check and must not be assumed current.</summary>
    Stale,
    /// <summary>The system cannot establish whether the recorded observations are still current.</summary>
    Unknown,
}
/// <summary>Metadata-only file categories; no observed file is automatically a recognized game.</summary>
public enum InventoryKind
{
    /// <summary>A file is a ROM candidate by observed path or extension only, without verifying game identity or playability.</summary>
    PotentialRom,
    /// <summary>A playlist or multidisc container was identified by metadata; its linked files remain separate observations.</summary>
    Playlist,
    /// <summary>A recognized archive path was observed without opening or inspecting the compressed game contents.</summary>
    ArchiveCandidate,
    /// <summary>A supporting metadata or checksum file was observed and is not counted as a confirmed game.</summary>
    Sidecar,
    /// <summary>A potential local image or media file was observed without approving any association to a game.</summary>
    Media,
    /// <summary>No supported file classification is justified by the available metadata.</summary>
    Unknown,
}
/// <summary>Severity of a bounded inventory diagnostic with no private-path disclosure.</summary>
public enum InventorySeverity
{
    /// <summary>Diagnostic context that does not itself invalidate complete coverage.</summary>
    Info,
    /// <summary>A limitation or anomaly that requires inspection before relying on a derived conclusion.</summary>
    Warning,
    /// <summary>A failed operation or rejected source boundary that must remain visible in the attempt diagnostics.</summary>
    Error,
}

/// <summary>Explicitly selected local inventory boundary and operation budgets; constructing this record never authorizes a scan.</summary>
public sealed record InventoryScope(
    string LibraryRoot,
    IReadOnlyDictionary<string, string>? PlatformAliases = null,
    int MaxDepth = 8,
    int MaxFiles = 100000,
    int MaxDirectories = 10000,
    int MaxPlaylistBytes = 32768,
    int MaxPlaylistLines = 256,
    TimeSpan? TimeLimit = null)
{
    // Test-only deterministic cancellation seam, not exposed as a public inventory API.
    /// <summary>Test-only observer of bounded playlist reads, never a production progress or filesystem grant.</summary>
    internal Action<int>? PlaylistChunkObserved { get; init; }
}

/// <summary>Metadata-only evidence about one observed path; neither the provisional kind nor a size implies a verified game.</summary>
public sealed record FileObservation(
    string ObservationId, string LocationKey, string PlatformKey, string RelativePath,
    InventoryKind Kind, string Extension, long? SizeBytes, DateTimeOffset? ModifiedUtc,
    DateTimeOffset ObservedAtUtc, string SourceKind, InventoryAvailability Availability,
    IReadOnlyList<string> DiagnosticRefs);

/// <summary>Safe, scoped explanation of an inventory limitation; diagnostic references avoid exposing raw filesystem details.</summary>
public sealed record InventoryDiagnostic(
    string Code, InventorySeverity Severity, string ScopeRef, string Reason,
    string SourceKind, DateTimeOffset ObservedAtUtc);

/// <summary>Per-partition coverage state used to distinguish complete enumeration from missing or interrupted evidence.</summary>
public sealed record InventoryCoverage(string PartitionKey, InventoryScanStatus Status);
/// <summary>A relative playlist target with observed existence; the target is never opened to infer a game identity.</summary>
public sealed record InventoryPlaylistMember(string RelativePath, bool Exists);
/// <summary>An observed playlist and its bounded relative member references, not a validated multidisc title.</summary>
public sealed record InventoryPlaylist(string PlaylistLocationKey, IReadOnlyList<InventoryPlaylistMember> Members);
/// <summary>Evidence-based group of potentially matching file observations; no automatic duplicate deletion or merging is implied.</summary>
public sealed record PotentialDuplicateGroup(
    string GroupId, IReadOnlyList<string> ObservationIds, string Reason);

/// <summary>Immutable record of observations, diagnostics and coverage for an attempt; unknown totals remain null rather than estimated.</summary>
public sealed record InventorySnapshot(
    string SchemaVersion, string LibrarySessionId, string ScanId, DateTimeOffset CapturedAtUtc,
    InventoryScanStatus ScanStatus, InventoryAvailability Availability, InventoryFreshness Freshness,
    IReadOnlyList<InventoryCoverage> CoverageProof, IReadOnlyList<FileObservation> Observations,
    IReadOnlyList<InventoryDiagnostic> Diagnostics, IReadOnlyList<InventoryPlaylist> Playlists,
    IReadOnlyList<PotentialDuplicateGroup> PotentialDuplicates, string ContentFingerprint,
    int ObservedFileCount, int? TotalFileCount, int? ConfirmedGameCount);

/// <summary>Maintains current complete evidence and the last attempt for one explicit scope; never relocates by trusting a drive letter.</summary>
public sealed class InventorySession
{
    private readonly InventoryScope _scope;
    private readonly string _sessionId = "library-" + Guid.NewGuid().ToString("N");
    private string _root;

/// <summary>Initialize a bounded local scope after validating its syntax and scan limits; construction performs no inventory scan.</summary>
    public InventorySession(InventoryScope explicitlySelectedScope)
    {
        ArgumentNullException.ThrowIfNull(explicitlySelectedScope);
        _scope = explicitlySelectedScope;
        _root = ValidateRootSyntax(_scope.LibraryRoot);
        ValidateLimits(_scope);
    }

/// <summary>Last fully completed inventory snapshot; partial, failed or cancelled attempts do not replace this evidence.</summary>
    public InventorySnapshot? Current { get; private set; }
/// <summary>Most recent scan or relocation attempt, including incomplete attempts that must remain inspectable.</summary>
    public InventorySnapshot? LastAttempt { get; private set; }

    // Failed or partial observations remain inspectable, but never erase a complete snapshot.
/// <summary>Observe the selected ROMS scope with bounded metadata reads; update Current only when the attempt completes.</summary>
    public InventorySnapshot Scan(CancellationToken cancellationToken = default)
    {
        var attempted = InventoryReader.Scan(_scope with { LibraryRoot = _root }, _sessionId, cancellationToken);
        LastAttempt = attempted;
        if (attempted.ScanStatus == InventoryScanStatus.Complete) Current = attempted;
        return attempted;
    }

    // Explicit relocation requires a fully observed, equivalent projection; not drive-letter trust.
/// <summary>Accept a new explicitly selected root only after a complete snapshot proves matching content fingerprint; preserve Current on refusal.</summary>
    public InventorySnapshot RelocateVerified(string explicitlySelectedRoot, CancellationToken cancellationToken = default)
    {
        if (Current is null) throw new InvalidOperationException("A complete snapshot is required.");
        var root = ValidateRootSyntax(explicitlySelectedRoot);
        var staged = InventoryReader.Scan(_scope with { LibraryRoot = root }, _sessionId, cancellationToken);
        LastAttempt = staged;
        if (staged.ScanStatus != InventoryScanStatus.Complete ||
            !string.Equals(staged.ContentFingerprint, Current.ContentFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Relocation could not be verified; last complete snapshot preserved.");
        _root = root;
        Current = staged;
        return staged;
    }

/// <summary>Reject nonlocal, relative, UNC or device-style roots before any traversal and return a normalized absolute local path.</summary>
    internal static string ValidateRootSyntax(string root)
    {
        if (string.IsNullOrWhiteSpace(root) || root.Contains("://", StringComparison.Ordinal) ||
            root.StartsWith(@"\\", StringComparison.Ordinal) ||
            root.StartsWith("//", StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(root))
            throw new ArgumentException("An explicitly selected local absolute root is required.");
        var full = Path.GetFullPath(root);
        if (full.StartsWith(@"\\", StringComparison.Ordinal) || full.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("Network and device paths are forbidden.");
        return full;
    }

    private static void ValidateLimits(InventoryScope scope)
    {
        if (scope.MaxDepth < 1 || scope.MaxDepth > 64 ||
            scope.MaxFiles < 1 || scope.MaxDirectories < 1 ||
            scope.MaxPlaylistBytes < 1 || scope.MaxPlaylistBytes > 1048576 ||
            scope.MaxPlaylistLines < 1 || scope.TimeLimit is { Ticks: <= 0 })
            throw new ArgumentOutOfRangeException(nameof(scope), "Inventory limits must be positive and bounded.");
    }
}

/// <summary>Metadata-only bounded scanner underpinning InventorySession; it never hashes ROM data or authorizes a private library.</summary>
internal static class InventoryReader
{
    private const string Source = "FILESYSTEM_OBSERVATION";
    private const string Schema = "next-02-inventory/0.1-candidate";
    private static readonly HashSet<string> RomExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".gb", ".gbc", ".gba", ".nes", ".sfc", ".smc", ".n64", ".z64", ".v64", ".chd", ".cue", ".gdi", ".cso", ".3ds", ".nds", ".nsp" };
    private static readonly HashSet<string> MediaExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp", ".gif", ".mp4" };
    private static readonly HashSet<string> SidecarExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".xml", ".txt", ".nfo", ".json", ".dat", ".sha256" };

/// <summary>Build a bounded observation snapshot with cancellation, coverage proof and stable provenance identifiers.</summary>
    internal static InventorySnapshot Scan(InventoryScope scope, string librarySessionId, CancellationToken token)
    {
        var scanId = Guid.NewGuid().ToString("N");
        var observedAt = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var observations = new List<FileObservation>();
        var diagnostics = new List<InventoryDiagnostic>();
        var playlists = new List<InventoryPlaylist>();
        var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        bool partial = false;
        InventoryAvailability availability = InventoryAvailability.Online;

        void Diagnostic(string code, InventorySeverity severity, string relative, string reason)
        {
            diagnostics.Add(new(code, severity, LocationKey(librarySessionId, relative), reason, Source, observedAt));
        }

        InventorySnapshot Finish(InventoryScanStatus status)
        {
            var ordered = observations.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray();
            var duplicates = ordered.Where(o => o.SizeBytes.HasValue &&
                    o.Kind is InventoryKind.PotentialRom or InventoryKind.ArchiveCandidate)
                .GroupBy(o => o.PlatformKey + "\u001f" +
                    Path.GetFileNameWithoutExtension(o.RelativePath).ToUpperInvariant() + "\u001f" +
                    o.Extension.ToUpperInvariant() + "\u001f" + o.SizeBytes,
                    StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new PotentialDuplicateGroup("candidate-" + Digest(group.Key),
                    group.Select(x => x.ObservationId).ToArray(),
                    "Same platform, normalized filename, extension and observed byte count; NOT a binary match."))
                .ToArray();
            foreach (var duplicate in duplicates)
                Diagnostic("POTENTIAL_DUPLICATE", InventorySeverity.Info, duplicate.GroupId,
                    "Candidate only; never a confirmed duplicate or deletion instruction.");

            var proof = new List<InventoryCoverage>
                { new("ROMS", status == InventoryScanStatus.Complete ? InventoryScanStatus.Complete :
                    status == InventoryScanStatus.Failed ? InventoryScanStatus.Failed : InventoryScanStatus.Partial) };
            proof.AddRange(platforms.OrderBy(x => x, StringComparer.Ordinal)
                .Select(p => new InventoryCoverage("ROMS/" + p,
                    status == InventoryScanStatus.Complete ? InventoryScanStatus.Complete : InventoryScanStatus.Partial)));
            var fingerprintInput = "scope:ROMS|policy:explicit-folder/1|extension:conservative/1|" + status + "\n" +
                string.Join("\n", ordered.Select(x => x.RelativePath + "\u001f" + x.PlatformKey +
                    "\u001f" + x.Kind + "\u001f" + x.SizeBytes + "\u001f" + x.ModifiedUtc?.UtcTicks)) +
                "\n" + string.Join("|", proof.Select(x => x.PartitionKey + ":" + x.Status));
            return new(Schema, librarySessionId, scanId, observedAt, status,
                availability, status == InventoryScanStatus.Failed ? InventoryFreshness.Unknown : InventoryFreshness.Fresh,
                proof, ordered, diagnostics.ToArray(), playlists.ToArray(), duplicates,
                Digest(fingerprintInput), ordered.Length,
                status == InventoryScanStatus.Complete ? ordered.Length : null, null);
        }

        try
        {
            var root = InventorySession.ValidateRootSyntax(scope.LibraryRoot);
            if (!Directory.Exists(root))
            {
                availability = InventoryAvailability.Offline;
                Diagnostic("ROOT_UNAVAILABLE", InventorySeverity.Error, "ROMS", "Selected library root unavailable.");
                return Finish(InventoryScanStatus.Failed);
            }
            if (IsReparse(root))
            {
                availability = InventoryAvailability.Inaccessible;
                Diagnostic("LINK_SKIPPED", InventorySeverity.Error, "ROMS", "Reparse root refused.");
                return Finish(InventoryScanStatus.Failed);
            }
            var romRoot = Path.Combine(root, "ROMS");
            if (!Directory.Exists(romRoot) || IsReparse(romRoot))
            {
                availability = InventoryAvailability.Inaccessible;
                Diagnostic("ROMS_UNAVAILABLE", InventorySeverity.Error, "ROMS", "ROMS scope absent or unsafe.");
                return Finish(InventoryScanStatus.Failed);
            }

            var stack = new Stack<(string Path, int Depth)>();
            stack.Push((romRoot, 0));
            var visitedDirectories = 0;
            var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool stop = false;

            while (stack.Count > 0 && !stop)
            {
                if (token.IsCancellationRequested || (scope.TimeLimit is { } limit && clock.Elapsed >= limit))
                {
                    partial = true;
                    Diagnostic(token.IsCancellationRequested ? "CANCELLED" : "LIMIT_REACHED",
                        InventorySeverity.Warning, "ROMS", "Enumeration stopped before complete coverage.");
                    break;
                }
                if (++visitedDirectories > scope.MaxDirectories)
                {
                    partial = true;
                    Diagnostic("LIMIT_REACHED", InventorySeverity.Warning, "ROMS", "Directory budget reached.");
                    break;
                }

                var (directory, depth) = stack.Pop();
                var relativeDirectory = Normalize(Path.GetRelativePath(romRoot, directory));
                if (depth == 1) platforms.Add(relativeDirectory);
                try
                {
                    // Bounded materialization: never sort an unbounded directory into memory.
                    var budget = (long)scope.MaxFiles + scope.MaxDirectories + 1;
                    var entries = Directory.EnumerateFileSystemEntries(directory)
                        .Take((int)Math.Min(budget, int.MaxValue))
                        .OrderBy(x => x, StringComparer.Ordinal).ToArray();
                    if (entries.LongLength >= budget)
                    {
                        partial = true;
                        Diagnostic("LIMIT_REACHED", InventorySeverity.Warning, relativeDirectory,
                            "Directory entry limit reached; coverage incomplete.");
                    }

                    foreach (var entry in entries)
                    {
                        if (token.IsCancellationRequested)
                        {
                            partial = true;
                            Diagnostic("CANCELLED", InventorySeverity.Warning, relativeDirectory, "Scan cancelled.");
                            stop = true;
                            break;
                        }
                        if (scope.TimeLimit is { } timeLimit && clock.Elapsed >= timeLimit)
                        {
                            partial = true;
                            Diagnostic("LIMIT_REACHED", InventorySeverity.Warning, relativeDirectory,
                                "Time limit reached.");
                            stop = true;
                            break;
                        }

                        var relative = Normalize(Path.GetRelativePath(romRoot, entry));
                        var location = LocationKey(librarySessionId, "ROMS/" + relative);
                        try
                        {
                            var attrs = File.GetAttributes(entry);
                            if ((attrs & FileAttributes.ReparsePoint) != 0)
                            {
                                partial = true;
                                Diagnostic("LINK_SKIPPED", InventorySeverity.Warning, relative,
                                    "Symbolic link/junction refused.");
                                continue;
                            }
                            if ((attrs & FileAttributes.Directory) != 0)
                            {
                                if (depth >= scope.MaxDepth)
                                {
                                    partial = true;
                                    Diagnostic("LIMIT_REACHED", InventorySeverity.Warning, relative,
                                        "Maximum directory depth reached.");
                                    continue;
                                }
                                stack.Push((entry, depth + 1));
                                continue;
                            }
                            if (observations.Count >= scope.MaxFiles)
                            {
                                partial = true;
                                Diagnostic("LIMIT_REACHED", InventorySeverity.Warning, relative,
                                    "Maximum file count reached.");
                                stop = true;
                                break;
                            }
                            if (!seenPaths.Add(relative))
                            {
                                partial = true;
                                Diagnostic("CASE_COLLISION", InventorySeverity.Error, relative,
                                    "Two paths collide under Windows case folding.");
                                continue;
                            }

                            var pieces = relative.Split('/');
                            var platform = "UNKNOWN";
                            if (pieces.Length > 1 && scope.PlatformAliases is { } aliases)
                            {
                                foreach (var alias in aliases)
                                    if (string.Equals(pieces[0], alias.Key, StringComparison.OrdinalIgnoreCase) &&
                                        !string.IsNullOrWhiteSpace(alias.Value))
                                    {
                                        platform = alias.Value;
                                        break;
                                    }
                            }
                            if (platform == "UNKNOWN")
                                Diagnostic("UNKNOWN_PLATFORM", InventorySeverity.Info, relative,
                                    "No explicitly configured platform alias for this folder.");

                            var extension = Path.GetExtension(entry).ToLowerInvariant();
                            var kind = Classify(extension);
                            if (kind == InventoryKind.ArchiveCandidate)
                                Diagnostic("AMBIGUOUS_EXTENSION", InventorySeverity.Info, relative,
                                    "Extension alone does not establish a game identity.");
                            long? size = null;
                            DateTimeOffset? modified = null;
                            var info = new FileInfo(entry);
                            size = info.Length;
                            modified = info.LastWriteTimeUtc;
                            var observation = new FileObservation(
                                "obs-" + scanId + "-" + observations.Count, location, platform,
                                "ROMS/" + relative, kind, extension, size, modified,
                                observedAt, Source, InventoryAvailability.Online, Array.Empty<string>());
                            observations.Add(observation);

                            if (kind == InventoryKind.Playlist)
                                playlists.Add(ReadPlaylist(entry, romRoot, observation.LocationKey,
                                    scope, token, Diagnostic, () => partial = true));
                        }
                        catch (OperationCanceledException) when (token.IsCancellationRequested)
                        {
                            partial = true;
                            stop = true;
                            Diagnostic("CANCELLED", InventorySeverity.Warning, relative,
                                "Scan cancelled during file or playlist reading.");
                        }
                        catch (Exception e) when (IsExpectedIo(e))
                        {
                            partial = true;
                            Diagnostic("UNREADABLE_ENTRY", InventorySeverity.Warning, relative,
                                "Entry unavailable or not readable during enumeration.");
                        }
                    }
                }
                catch (Exception e) when (IsExpectedIo(e))
                {
                    partial = true;
                    Diagnostic("UNREADABLE_DIRECTORY", InventorySeverity.Warning, relativeDirectory,
                        "Directory could not be completely enumerated.");
                }
            }
        }
        catch (Exception e) when (IsExpectedIo(e))
        {
            availability = InventoryAvailability.Inaccessible;
            partial = true;
            Diagnostic("ROOT_UNAVAILABLE", InventorySeverity.Error, "ROMS", "Root became inaccessible.");
        }
        return Finish(partial ? InventoryScanStatus.Partial : InventoryScanStatus.Complete);
    }

    private static InventoryPlaylist ReadPlaylist(string file, string romRoot, string location,
        InventoryScope scope, CancellationToken token,
        Action<string, InventorySeverity, string, string> diagnostic, Action markPartial)
    {
        var members = new List<InventoryPlaylistMember>();
        var relative = Normalize(Path.GetRelativePath(romRoot, file));

        // A preliminary size check is just an optimization, never the read bound.
        // Keep the handle read-only and deny concurrent writers while it is open.
        // Revalidate the path after opening; residual rename/reparse races still require
        // a separate Windows security review before any real SSD scan is authorized.
        if (IsReparse(file))
        {
            diagnostic("LINK_SKIPPED", InventorySeverity.Warning, relative, "Playlist is a reparse point.");
            markPartial();
            return new(location, members);
        }
        token.ThrowIfCancellationRequested();
        using var handle = File.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (IsReparse(file))
        {
            diagnostic("LINK_SKIPPED", InventorySeverity.Warning, relative,
                "Playlist was replaced by a reparse point during opening.");
            markPartial();
            return new(location, members);
        }

        byte[] bytes;
        try
        {
            // Bound actual bytes read, not FileInfo.Length observed before opening.
            bytes = ReadBoundedPlaylistBytes(handle, scope.MaxPlaylistBytes,
                scope.MaxPlaylistLines, token, scope.PlaylistChunkObserved);
        }
        catch (InvalidDataException)
        {
            diagnostic("PLAYLIST_LIMIT", InventorySeverity.Warning, relative,
                "Playlist byte/line limit or invalid UTF-8 reached during reading.");
            markPartial();
            return new(location, members);
        }

        // Strict UTF-8, no environment-dependent locale decoding of untrusted text.
        string text;
        try { text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes); }
        catch (DecoderFallbackException)
        {
            diagnostic("PLAYLIST_LIMIT", InventorySeverity.Warning, relative, "Playlist has invalid UTF-8.");
            markPartial();
            return new(location, members);
        }
        var lines = text.TrimStart('\uFEFF').Split('\n');
        foreach (var raw in lines)
        {
            token.ThrowIfCancellationRequested();
            var member = raw.Trim().Replace('\\', '/');
            if (member.Length == 0 || member.StartsWith('#')) continue;
            if (member.StartsWith('/') || member.Contains(':') ||
                member.Split('/').Any(p => p == "..") || member.StartsWith("//"))
            {
                diagnostic("PLAYLIST_PATH_REFUSED", InventorySeverity.Warning, relative,
                    "Unsafe playlist member refused.");
                continue;
            }
            var resolved = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(file)!,
                member.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = Path.GetFullPath(romRoot).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!resolved.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) || IsReparse(resolved))
            {
                diagnostic("PLAYLIST_PATH_REFUSED", InventorySeverity.Warning, relative,
                    "Playlist member outside permitted root or linked.");
                continue;
            }
            var exists = File.Exists(resolved);
            if (!exists)
                diagnostic("PLAYLIST_MEMBER_MISSING", InventorySeverity.Info, relative,
                    "Playlist member currently unavailable; never a deletion.");
            members.Add(new(Normalize(Path.GetRelativePath(romRoot, resolved)), exists));
        }
        return new(location, members);
    }

    // Internal testable core; the source stream can grow despite a prior file-size check.
    // At most MaxPlaylistBytes + 1 bytes are ever requested; extra data fails closed.
/// <summary>Read only a capped playlist byte range for metadata parsing; never follow targets or open ROM content.</summary>
    internal static byte[] ReadBoundedPlaylistBytes(
        Stream source, int maxBytes, int maxLines, CancellationToken token,
        Action<int>? afterChunk = null)
    {
        if (maxBytes < 1 || maxLines < 1) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        using var output = new MemoryStream(Math.Min(maxBytes, 4096));
        var buffer = new byte[Math.Min(maxBytes + 1, 4096)];
        int total = 0, newlines = 0;
        byte last = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            int want = Math.Min(buffer.Length, maxBytes + 1 - total);
            int count = source.ReadAsync(buffer.AsMemory(0, want), token).AsTask()
                .GetAwaiter().GetResult();
            token.ThrowIfCancellationRequested();
            if (count == 0) break;
            total += count;
            if (total > maxBytes) throw new InvalidDataException("PLAYLIST_BYTE_LIMIT");
            for (int i = 0; i < count; ++i)
            {
                if (buffer[i] == (byte)'\n') ++newlines;
                last = buffer[i];
            }
            if (newlines > maxLines) throw new InvalidDataException("PLAYLIST_LINE_LIMIT");
            output.Write(buffer, 0, count);
            afterChunk?.Invoke(total);
            token.ThrowIfCancellationRequested();
        }
        if (total > 0 && last != (byte)'\n' && newlines >= maxLines)
            throw new InvalidDataException("PLAYLIST_LINE_LIMIT");
        return output.ToArray();
    }

    private static InventoryKind Classify(string extension)
    {
        if (extension == ".m3u") return InventoryKind.Playlist;
        if (extension is ".zip" or ".7z" or ".rar" or ".iso" or ".bin") return InventoryKind.ArchiveCandidate;
        if (RomExtensions.Contains(extension)) return InventoryKind.PotentialRom;
        if (SidecarExtensions.Contains(extension)) return InventoryKind.Sidecar;
        if (MediaExtensions.Contains(extension)) return InventoryKind.Media;
        return InventoryKind.Unknown;
    }

    private static bool IsReparse(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var part in full[root.Length..].Split(Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private static bool IsExpectedIo(Exception e) =>
        e is IOException or UnauthorizedAccessException or SecurityException or ArgumentException
            or NotSupportedException or System.ComponentModel.Win32Exception;

    private static string Normalize(string path) => path.Replace('\\', '/');
    private static string LocationKey(string sessionId, string path) =>
        "loc-" + Digest(sessionId.Length + ":" + sessionId + "|" + path.Length + ":" + path);
    private static string Digest(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant()[..24];
}
