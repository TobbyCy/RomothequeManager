using System.Diagnostics;
using System.Security;
using System.Security.Cryptography;
using System.Text;

namespace RomothequeManager.V2.Inventory;

// NEXT-02: opt-in filesystem observations only; no ES-DE, ROM hashing or source mutation.
// The public types are candidate v0.1 until independent DEV 2 contract review.
public enum InventoryScanStatus { Complete, Partial, Failed }
public enum InventoryAvailability { Online, Offline, Inaccessible, Unknown }
public enum InventoryFreshness { Fresh, Stale, Unknown }
public enum InventoryKind { PotentialRom, Playlist, ArchiveCandidate, Sidecar, Media, Unknown }
public enum InventorySeverity { Info, Warning, Error }

public sealed record InventoryScope(
    string LibraryRoot,
    IReadOnlyDictionary<string, string>? PlatformAliases = null,
    int MaxDepth = 8,
    int MaxFiles = 100000,
    int MaxDirectories = 10000,
    int MaxPlaylistBytes = 32768,
    int MaxPlaylistLines = 256,
    TimeSpan? TimeLimit = null);

public sealed record FileObservation(
    string ObservationId, string LocationKey, string PlatformKey, string RelativePath,
    InventoryKind Kind, string Extension, long? SizeBytes, DateTimeOffset? ModifiedUtc,
    DateTimeOffset ObservedAtUtc, string SourceKind, InventoryAvailability Availability,
    IReadOnlyList<string> DiagnosticRefs);

public sealed record InventoryDiagnostic(
    string Code, InventorySeverity Severity, string ScopeRef, string Reason,
    string SourceKind, DateTimeOffset ObservedAtUtc);

public sealed record InventoryCoverage(string PartitionKey, InventoryScanStatus Status);
public sealed record InventoryPlaylistMember(string RelativePath, bool Exists);
public sealed record InventoryPlaylist(string PlaylistLocationKey, IReadOnlyList<InventoryPlaylistMember> Members);
public sealed record PotentialDuplicateGroup(
    string GroupId, IReadOnlyList<string> ObservationIds, string Reason);

public sealed record InventorySnapshot(
    string SchemaVersion, string LibrarySessionId, string ScanId, DateTimeOffset CapturedAtUtc,
    InventoryScanStatus ScanStatus, InventoryAvailability Availability, InventoryFreshness Freshness,
    IReadOnlyList<InventoryCoverage> CoverageProof, IReadOnlyList<FileObservation> Observations,
    IReadOnlyList<InventoryDiagnostic> Diagnostics, IReadOnlyList<InventoryPlaylist> Playlists,
    IReadOnlyList<PotentialDuplicateGroup> PotentialDuplicates, string ContentFingerprint,
    int ObservedFileCount, int? TotalFileCount, int? ConfirmedGameCount);

public sealed class InventorySession
{
    private readonly InventoryScope _scope;
    private readonly string _sessionId = "library-" + Guid.NewGuid().ToString("N");
    private string _root;

    public InventorySession(InventoryScope explicitlySelectedScope)
    {
        ArgumentNullException.ThrowIfNull(explicitlySelectedScope);
        _scope = explicitlySelectedScope;
        _root = ValidateRootSyntax(_scope.LibraryRoot);
        ValidateLimits(_scope);
    }

    public InventorySnapshot? Current { get; private set; }
    public InventorySnapshot? LastAttempt { get; private set; }

    // Failed or partial observations remain inspectable, but never erase a complete snapshot.
    public InventorySnapshot Scan(CancellationToken cancellationToken = default)
    {
        var attempted = InventoryReader.Scan(_scope with { LibraryRoot = _root }, _sessionId, cancellationToken);
        LastAttempt = attempted;
        if (attempted.ScanStatus == InventoryScanStatus.Complete) Current = attempted;
        return attempted;
    }

    // Explicit relocation requires a fully observed, equivalent projection; not drive-letter trust.
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
        var info = new FileInfo(file);
        if (info.Length > scope.MaxPlaylistBytes)
        {
            diagnostic("PLAYLIST_LIMIT", InventorySeverity.Warning, relative, "Playlist exceeds byte limit.");
            markPartial();
            return new(location, members);
        }
        // This text-only read is bounded and re-checks symlinks immediately before opening.
        if (IsReparse(file))
        {
            diagnostic("LINK_SKIPPED", InventorySeverity.Warning, relative, "Playlist changed to a link.");
            markPartial();
            return new(location, members);
        }
        var lines = File.ReadAllLines(file);
        if (lines.Length > scope.MaxPlaylistLines)
        {
            diagnostic("PLAYLIST_LIMIT", InventorySeverity.Warning, relative, "Playlist exceeds line limit.");
            markPartial();
            return new(location, members);
        }
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
