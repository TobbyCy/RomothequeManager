using System.Buffers.Binary;
using System.Diagnostics;
using System.Security;

namespace RomothequeManager.V2.Inventory.Media;

/// <summary>Media role proven by an explicitly configured folder convention, not by a filename.</summary>
public enum MediaKind { Unknown, Cover, Thumbnail, Logo, Background, Screenshot, Other }

/// <summary>Header classification only; no full image decode or renderability guarantee.</summary>
public enum MediaImageFormat { Unknown, Png, Jpeg, Webp }

/// <summary>Limits of an explicitly selected local MEDIAS root, never a ROMS or XML scan.</summary>
/// <remarks>Header and dimension checks do not establish safe rendering. The public
/// service remains fail-closed regardless of this configuration. The future SQLite
/// StorageRootId is independent of this ephemeral selection.</remarks>
public sealed record MediaRootScope(
    string MediaRoot,
    IReadOnlyDictionary<string, string>? PlatformAliases = null,
    IReadOnlyDictionary<string, MediaKind>? FolderKinds = null,
    int MaxDepth = 8,
    int MaxFiles = 5000,
    int MaxDirectories = 10000,
    TimeSpan? TimeLimit = null,
    int MaxHeaderBytes = 262144,
    long MaxImageBytes = 67108864,
    int MaxWidth = 8192,
    int MaxHeight = 8192,
    long MaxPixels = 32000000,
    long MaxTotalHeaderBytes = 8388608)
{
    /// <summary>Test-only deterministic cancellation seam; never set by product callers.</summary>
    internal Action<int>? HeaderObserved { get; init; }
}

/// <summary>One file observed within the selected media root; not a durable MediaAsset or game.</summary>
/// <remarks>All paths are media-root-relative and source facts are run-scoped.
/// HeaderValidated is not a promise that pixel decoding or full file validation succeeds.</remarks>
public sealed record MediaObservation(
    string MediaObservationId, string MediaRootRef, string RelativePath,
    string PlatformKey, MediaKind Kind, string Extension,
    MediaImageFormat DetectedFormat, bool HeaderValidated,
    long? SizeBytes, int? Width, int? Height, DateTimeOffset? ModifiedUtc,
    DateTimeOffset ObservedAtUtc, InventoryAvailability Availability,
    string SourceKind, IReadOnlyList<string> DiagnosticRefs);

/// <summary>Captured media enumeration; Current/LastAttempt selection belongs to the service.</summary>
/// <remarks>Incomplete or offline coverage must never be interpreted as removal.
/// All collections returned by the reader are read-only views.</remarks>
public sealed record MediaIndexSnapshot(
    string RunId, string MediaRootRef, DateTimeOffset CapturedAtUtc,
    InventoryScanStatus ScanStatus, InventoryAvailability Availability,
    InventoryFreshness Freshness, IReadOnlyList<InventoryCoverage> CoverageProof,
    IReadOnlyList<MediaObservation> Observations,
    IReadOnlyList<InventoryDiagnostic> Diagnostics,
    int ObservedFileCount, int? TotalFileCount);

/// <summary>Fail-closed media scan entry point with test-only synthetic root permission.</summary>
/// <remarks>No production caller can grant permission through config, CLI, XML or consent.
/// This API does not open ROMs and does not persist results. Only a separate owner
/// authorization may permit future personal media indexing.</remarks>
public sealed class MediaIndexApplicationService
{
    private readonly Func<MediaRootScope, bool> _syntheticPermission;
    private readonly object _sync = new();
    private readonly string _mediaRootRef = "media-selection-" + Guid.NewGuid().ToString("N");

    /// <summary>Production constructor: ALL scans are denied without filesystem access.</summary>
    public MediaIndexApplicationService() : this(static _ => false) { }

    /// <summary>Only the friend synthetic test assembly may grant a selected fixture root.</summary>
    internal MediaIndexApplicationService(Func<MediaRootScope, bool> syntheticPermission) =>
        _syntheticPermission = syntheticPermission ?? throw new ArgumentNullException(nameof(syntheticPermission));

    /// <summary>Last complete media generation, preserved after any degraded attempt.</summary>
    public MediaIndexSnapshot? Current { get; private set; }

    /// <summary>Last attempted generation, including denied, partial and offline results.</summary>
    public MediaIndexSnapshot? LastAttempt { get; private set; }

    /// <summary>Perform one explicitly confirmed scan, only if the internal fixture gate allows it.</summary>
    /// <remarks>The caller must invoke this off the UI thread. Cancellation produces Partial,
    /// not a false Complete. Concurrent calls are serialized, retaining the last Complete.</remarks>
    public MediaIndexSnapshot ScanConfirmed(MediaRootScope scope, bool explicitConsent,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (_sync)
        {
            // Pure syntax validation; never open a path before checking authorization.
            var root = InventorySession.ValidateRootSyntax(scope.MediaRoot);
            if (string.Equals(Path.GetFileName(Path.TrimEndingDirectorySeparator(root)),
                    "ROMS", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("ROMS cannot be a MEDIAS root.", nameof(scope));
            var validScope = scope with { MediaRoot = root };
            MediaIndexSnapshot result;
            if (!explicitConsent || !_syntheticPermission(validScope))
            {
                var at = DateTimeOffset.UtcNow;
                result = new MediaIndexSnapshot(
                    Guid.NewGuid().ToString("N"), _mediaRootRef, at,
                    InventoryScanStatus.Failed, InventoryAvailability.Unknown,
                    InventoryFreshness.Unknown,
                    Array.AsReadOnly(new[] { new InventoryCoverage("MEDIAS", InventoryScanStatus.Failed) }),
                    Array.AsReadOnly(Array.Empty<MediaObservation>()),
                    Array.AsReadOnly(new[] { new InventoryDiagnostic(
                        !explicitConsent ? "MEDIA_CONSENT_REQUIRED" : "REAL_MEDIA_SCAN_HOLD",
                        InventorySeverity.Warning, "MEDIAS",
                        "Media indexing not authorized.", "FILESYSTEM_MEDIA", at) }),
                    0, null);
            }
            else
                result = MediaIndexReader.Scan(validScope, _mediaRootRef, cancellationToken);
            LastAttempt = result;
            if (result.ScanStatus == InventoryScanStatus.Complete) Current = result;
            return result;
        }
    }

    /// <summary>Asynchronous wrapper for test/service composition; identical permission gate.</summary>
    public Task<MediaIndexSnapshot> ScanConfirmedAsync(MediaRootScope scope, bool explicitConsent,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => ScanConfirmed(scope, explicitConsent, cancellationToken));
}

/// <summary>Internal, bounded filesystem media reader. Never callable via the public gate.</summary>
internal static class MediaIndexReader
{
    private const string Source = "FILESYSTEM_MEDIA";
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".webp" };
    private static readonly HashSet<string> Excluded = new(StringComparer.OrdinalIgnoreCase)
        { "ROMOTHEQUE_MANAGER", "data", "backups", "logs", ".git" };

    internal static MediaIndexSnapshot Scan(MediaRootScope scope, string mediaRootRef,
        CancellationToken token)
    {
        ValidateLimits(scope);
        var root = scope.MediaRoot;
        var runId = Guid.NewGuid().ToString("N");
        var observedAt = DateTimeOffset.UtcNow;
        var watch = Stopwatch.StartNew();
        var observed = new List<MediaObservation>();
        var diagnostics = new List<InventoryDiagnostic>();
        var partial = false;
        var availability = InventoryAvailability.Online;
        var filesSeen = 0;
        var dirsSeen = 0;
        long headerBytesTotal = 0;

        void Diagnostic(string code, InventorySeverity severity, string relative, string reason) =>
            diagnostics.Add(new InventoryDiagnostic(code, severity, relative, reason, Source, observedAt));

        MediaIndexSnapshot Finish(InventoryScanStatus status)
        {
            var ordered = observed.OrderBy(o => o.RelativePath, StringComparer.Ordinal).ToArray();
            return new MediaIndexSnapshot(runId, mediaRootRef, observedAt, status, availability,
                status == InventoryScanStatus.Complete ? InventoryFreshness.Fresh : InventoryFreshness.Unknown,
                Array.AsReadOnly(new[] { new InventoryCoverage("MEDIAS", status) }),
                Array.AsReadOnly(ordered), Array.AsReadOnly(diagnostics.ToArray()),
                ordered.Length, status == InventoryScanStatus.Complete ? ordered.Length : null);
        }

        try
        {
            if (!Directory.Exists(root))
            {
                availability = InventoryAvailability.Offline;
                Diagnostic("MEDIA_ROOT_OFFLINE", InventorySeverity.Warning, "MEDIAS", "Selected root unavailable.");
                return Finish(InventoryScanStatus.Failed);
            }
            if (HasReparseComponent(root))
            {
                availability = InventoryAvailability.Inaccessible;
                Diagnostic("MEDIA_REPARSE_REFUSED", InventorySeverity.Error, "MEDIAS", "Linked root refused.");
                return Finish(InventoryScanStatus.Failed);
            }

            var stack = new Stack<(string Path, int Depth)>();
            stack.Push((root, 0));
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var stop = false;
            while (stack.Count > 0 && !stop)
            {
                if (token.IsCancellationRequested || watch.Elapsed >= (scope.TimeLimit ?? TimeSpan.FromSeconds(30)))
                {
                    partial = true;
                    Diagnostic(token.IsCancellationRequested ? "MEDIA_CANCELLED" : "MEDIA_TIME_LIMIT",
                        InventorySeverity.Warning, "MEDIAS", "Enumeration incomplete.");
                    break;
                }
                if (++dirsSeen > scope.MaxDirectories)
                {
                    partial = true;
                    Diagnostic("MEDIA_DIRECTORY_LIMIT", InventorySeverity.Warning, "MEDIAS",
                        "Directory budget exceeded.");
                    break;
                }
                var (directory, depth) = stack.Pop();
                var dirRel = Path.GetRelativePath(root, directory).Replace('\\', '/');
                try
                {
                    var budget = (long)scope.MaxFiles + scope.MaxDirectories + 1;
                    var entries = Directory.EnumerateFileSystemEntries(directory)
                        .Take((int)Math.Min(budget, int.MaxValue))
                        .OrderBy(s => s, StringComparer.Ordinal).ToArray();
                    if (entries.LongLength >= budget)
                    {
                        partial = true;
                        Diagnostic("MEDIA_ENTRY_LIMIT", InventorySeverity.Warning, dirRel,
                            "Directory entry budget reached.");
                        stop = true;
                    }
                    foreach (var path in entries)
                    {
                        if (token.IsCancellationRequested || watch.Elapsed >= (scope.TimeLimit ?? TimeSpan.FromSeconds(30)))
                        {
                            partial = true;
                            Diagnostic(token.IsCancellationRequested ? "MEDIA_CANCELLED" : "MEDIA_TIME_LIMIT",
                                InventorySeverity.Warning, dirRel, "Enumeration incomplete.");
                            stop = true;
                            break;
                        }
                        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
                        if (relative == ".." || relative.StartsWith("../", StringComparison.Ordinal) ||
                            Path.IsPathRooted(relative))
                        {
                            partial = true;
                            Diagnostic("MEDIA_OUTSIDE_SCOPE", InventorySeverity.Error, "MEDIAS", "Out-of-root entry.");
                            continue;
                        }
                        try
                        {
                            var attrs = File.GetAttributes(path);
                            if ((attrs & FileAttributes.ReparsePoint) != 0)
                            {
                                partial = true;
                                Diagnostic("MEDIA_REPARSE_REFUSED", InventorySeverity.Warning,
                                    relative, "Symbolic link/junction refused.");
                                continue;
                            }
                            if ((attrs & FileAttributes.Directory) != 0)
                            {
                                if (relative.Split('/').Any(Excluded.Contains))
                                {
                                    Diagnostic("MEDIA_DIRECTORY_EXCLUDED", InventorySeverity.Info,
                                        relative, "Technical/application folder excluded.");
                                    continue;
                                }
                                if (depth >= scope.MaxDepth)
                                {
                                    partial = true;
                                    Diagnostic("MEDIA_DEPTH_LIMIT", InventorySeverity.Warning,
                                        relative, "Depth limit reached.");
                                    continue;
                                }
                                stack.Push((path, depth + 1));
                                continue;
                            }
                            if (++filesSeen > scope.MaxFiles)
                            {
                                partial = true;
                                Diagnostic("MEDIA_FILE_LIMIT", InventorySeverity.Warning,
                                    relative, "File budget reached.");
                                stop = true;
                                break;
                            }
                            if (!seen.Add(relative))
                            {
                                partial = true;
                                Diagnostic("MEDIA_CASE_COLLISION", InventorySeverity.Error,
                                    relative, "Colliding relative path.");
                                continue;
                            }
                            var extension = Path.GetExtension(path).ToLowerInvariant();
                            if (!Extensions.Contains(extension)) continue;
                            var pieces = relative.Split('/');
                            var platform = "UNKNOWN";
                            if (pieces.Length > 1 && scope.PlatformAliases is not null &&
                                scope.PlatformAliases.TryGetValue(pieces[0], out var alias) &&
                                !string.IsNullOrWhiteSpace(alias))
                                platform = alias;
                            var kind = MediaKind.Unknown;
                            if (pieces.Length > 2 && scope.FolderKinds is not null &&
                                scope.FolderKinds.TryGetValue(pieces[1], out var configured))
                                kind = configured;
                            var info = new FileInfo(path);
                            var size = info.Length;
                            var modified = (DateTimeOffset)info.LastWriteTimeUtc;
                            var format = MediaImageFormat.Unknown;
                            var valid = false;
                            int? width = null, height = null;
                            var refs = new List<string>();
                            void Warn(string code, string reason)
                            {
                                refs.Add(code);
                                Diagnostic(code, InventorySeverity.Warning, relative, reason);
                            }
                            if (size > scope.MaxImageBytes)
                                Warn("MEDIA_IMAGE_SIZE_LIMIT", "Image exceeds selected byte limit.");
                            else if (headerBytesTotal >= scope.MaxTotalHeaderBytes)
                            {
                                partial = true;
                                Warn("MEDIA_HEADER_TOTAL_LIMIT", "Cumulative header budget exhausted.");
                                stop = true;
                            }
                            else
                            {
                                // Recheck the full parent chain immediately before opening.
                                if (HasReparseComponent(path))
                                {
                                    partial = true;
                                    Warn("MEDIA_REPARSE_REFUSED", "Entry changed into a link.");
                                }
                                else
                                {
                                    var allowed = (int)Math.Min(Math.Min(size, scope.MaxHeaderBytes),
                                        scope.MaxTotalHeaderBytes - headerBytesTotal);
                                    var bytes = new byte[allowed];
                                    var count = 0;
                                    using (var stream = new FileStream(path, FileMode.Open,
                                        FileAccess.Read, FileShare.Read))
                                    {
                                        // Bounded chunks, with cancellation and elapsed-time checks
                                        // DURING header reads, not only between files.
                                        while (count < allowed)
                                        {
                                            if (token.IsCancellationRequested ||
                                                watch.Elapsed >= (scope.TimeLimit ?? TimeSpan.FromSeconds(30)))
                                            {
                                                partial = true;
                                                stop = true;
                                                Warn(token.IsCancellationRequested ? "MEDIA_CANCELLED" :
                                                    "MEDIA_TIME_LIMIT", "Header read interrupted.");
                                                break;
                                            }
                                            var n = stream.Read(bytes, count, Math.Min(4096, allowed - count));
                                            if (n == 0) throw new EndOfStreamException();
                                            count += n;
                                            headerBytesTotal += n;
                                            scope.HeaderObserved?.Invoke(count);
                                        }
                                    }
                                    if (!stop)
                                    {
                                        format = Classify(bytes, extension, scope,
                                            out valid, out width, out height, out var diagnosticCode);
                                        if (diagnosticCode is not null)
                                            Warn(diagnosticCode, "Header unsupported, malformed or outside limits.");
                                    }
                                }
                            }
                            observed.Add(new MediaObservation(
                                "media-obs-" + runId + "-" + observed.Count, mediaRootRef,
                                "MEDIAS/" + relative, platform, kind, extension,
                                format, valid, size, width, height, modified, observedAt,
                                InventoryAvailability.Online, Source, Array.AsReadOnly(refs.ToArray())));
                            if (stop) break;
                        }
                        catch (Exception ex) when (ExpectedIo(ex))
                        {
                            partial = true;
                            Diagnostic("MEDIA_UNREADABLE", InventorySeverity.Warning,
                                relative, "File could not be read safely.");
                        }
                    }
                }
                catch (Exception ex) when (ExpectedIo(ex))
                {
                    partial = true;
                    Diagnostic("MEDIA_DIRECTORY_UNREADABLE", InventorySeverity.Warning,
                        dirRel, "Directory could not be completely enumerated.");
                }
            }
        }
        catch (Exception ex) when (ExpectedIo(ex))
        {
            availability = InventoryAvailability.Inaccessible;
            Diagnostic("MEDIA_ROOT_UNAVAILABLE", InventorySeverity.Error, "MEDIAS",
                "Selected root became inaccessible.");
            partial = true;
        }
        return Finish(partial ? InventoryScanStatus.Partial : InventoryScanStatus.Complete);
    }

    private static void ValidateLimits(MediaRootScope s)
    {
        if (s.MaxDepth is < 1 or > 64 || s.MaxFiles is < 1 or > 100000 ||
            s.MaxDirectories is < 1 or > 100000 ||
            s.TimeLimit is { Ticks: <= 0 } ||
            s.TimeLimit > TimeSpan.FromMinutes(5) ||
            s.MaxHeaderBytes is < 33 or > 262144 ||
            s.MaxImageBytes is < 33 or > 67108864 ||
            s.MaxWidth is < 1 or > 8192 || s.MaxHeight is < 1 or > 8192 ||
            s.MaxPixels is < 1 or > 32000000 ||
            s.MaxTotalHeaderBytes is < 33 or > 134217728)
            throw new ArgumentOutOfRangeException(nameof(s), "Media limits invalid or unbounded.");
    }

    private static bool HasReparseComponent(string path)
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

    private static bool ExpectedIo(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or SecurityException or ArgumentException
            or NotSupportedException or System.ComponentModel.Win32Exception;

    private static MediaImageFormat Classify(byte[] b, string extension, MediaRootScope scope,
        out bool valid, out int? width, out int? height, out string? diagnostic)
    {
        valid = false;
        width = height = null;
        diagnostic = null;
        var data = b.AsSpan();
        var png = data.Length >= 8 && data[..8].SequenceEqual(new byte[]
            { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpg = data.Length >= 2 && data[0] == 255 && data[1] == 216;
        var webp = data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) &&
                   data.Slice(8, 4).SequenceEqual("WEBP"u8);
        var detected = png ? MediaImageFormat.Png : jpg ? MediaImageFormat.Jpeg :
            webp ? MediaImageFormat.Webp : MediaImageFormat.Unknown;
        if (webp)
        {
            diagnostic = "UNSUPPORTED_FORMAT";
            return detected;
        }
        if (!((png && extension == ".png") ||
              (jpg && (extension == ".jpg" || extension == ".jpeg"))))
        {
            diagnostic = detected == MediaImageFormat.Unknown ?
                "MEDIA_INVALID_SIGNATURE" : "MEDIA_EXTENSION_MISMATCH";
            return detected;
        }
        int w = 0, h = 0;
        if (png)
        {
            if (data.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(data.Slice(8, 4)) != 13 ||
                !data.Slice(12, 4).SequenceEqual("IHDR"u8) ||
                data[24] is not (1 or 2 or 4 or 8 or 16) ||
                !ValidPngColor(data[24], data[25]) ||
                data[26] != 0 || data[27] != 0 || data[28] is > 1 ||
                PngCrc(data.Slice(12, 17)) != BinaryPrimitives.ReadUInt32BigEndian(data.Slice(29, 4)))
            {
                diagnostic = "MEDIA_INVALID_PNG_HEADER";
                return detected;
            }
            w = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(16, 4));
            h = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(20, 4));
        }
        else if (!TryJpegDimensions(data, out w, out h))
        {
            diagnostic = "MEDIA_INVALID_JPEG_HEADER";
            return detected;
        }
        if (w < 1 || h < 1 || w > scope.MaxWidth || h > scope.MaxHeight ||
            (long)w * h > scope.MaxPixels)
        {
            diagnostic = "MEDIA_DIMENSION_LIMIT";
            return detected;
        }
        valid = true;
        width = w;
        height = h;
        return detected;
    }

    private static bool ValidPngColor(byte depth, byte color) => color switch
    {
        0 => depth is 1 or 2 or 4 or 8 or 16,
        2 => depth is 8 or 16,
        3 => depth is 1 or 2 or 4 or 8,
        4 => depth is 8 or 16,
        6 => depth is 8 or 16,
        _ => false
    };

    private static uint PngCrc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffff;
        foreach (var b in data)
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static bool TryJpegDimensions(ReadOnlySpan<byte> data, out int width, out int height)
    {
        width = height = 0;
        var offset = 2;
        while (offset + 4 <= data.Length)
        {
            if (data[offset++] != 255) return false;
            while (offset < data.Length && data[offset] == 255) offset++;
            if (offset >= data.Length) return false;
            var marker = data[offset++];
            if (marker is 0 or 0xd8 or 0xd9 or 0xda) return false;
            if (marker is >= 0xd0 and <= 0xd7 or 0x01) continue;
            if (offset + 2 > data.Length) return false;
            var size = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset, 2));
            if (size < 2 || offset + size > data.Length) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3)
            {
                if (size < 8 || data[offset + 2] != 8) return false;
                height = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 3, 2));
                width = BinaryPrimitives.ReadUInt16BigEndian(data.Slice(offset + 5, 2));
                return true;
            }
            offset += size;
        }
        return false;
    }
}
