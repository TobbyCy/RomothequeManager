using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using RomothequeManager.V2.Inventory.RsA;

namespace RomothequeManager.V2.Inventory.Media;

/// <summary>Volatile proof of a single MEDIAS observation: native handle identity
/// and the hash of its bounded header. Never persisted or exported to the UI.</summary>
internal sealed record NativeMediaFileEvidence(
    RsAHandleIdentity Identity, string HeaderSha256, int HeaderBytes);

/// <summary>Native, consent-bound index result with per-observation ephemeral
/// identity evidence; no source paths, handles or grants are persisted.</summary>
internal sealed record NativeMediaIndexResult(
    MediaIndexSnapshot Snapshot,
    IReadOnlyDictionary<string, NativeMediaFileEvidence> FileEvidence);

/// <summary>Windows-only read-only MEDIAS indexer and preview reader. Only
/// parent-relative handles can be used; ROMS grants and filesystem path-based
/// MediaIndexReader/MediaPreviewService fixture openers are never invoked.</summary>
internal static class NativeIdealMediaReader
{
    private const int MaxPreviewBytes = 8 * 1024 * 1024;
    private const int MaxPreviewSide = 512;
    private const int MaxSourceSide = 2048;
    private const long MaxPixels = 3_500_000;
    private const string Source = "IDEAL_NATIVE_MEDIA";

    /// <summary>Index bounded headers and file metadata only with an independently
    /// claimed MEDIAS index capability. Any incomplete scope is Partial, not Complete.</summary>
    internal static NativeMediaIndexResult Index(IdealMediaGrantSession session,
        IdealMediaReadGrant grant, MediaRootScope scope, long generation,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("MEDIA_WINDOWS_NATIVE_ONLY");
        if (scope.MaxDepth is < 1 or > 16 || scope.MaxFiles is < 1 or > 30000 ||
            scope.MaxDirectories is < 1 or > 100000 ||
            scope.MaxHeaderBytes is < 33 or > 262144 ||
            scope.MaxTotalHeaderBytes is < 33 or > 134217728 ||
            scope.MaxImageBytes is < 33 or > 67108864 ||
            scope.MaxWidth is < 1 or > 8192 || scope.MaxHeight is < 1 or > 8192 ||
            scope.MaxPixels is < 1 or > 32000000 ||
            scope.TimeLimit is { Ticks: <= 0 } || scope.TimeLimit > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(scope), "MEDIA_BUDGET_REQUIRED");

        var began = DateTimeOffset.UtcNow;
        var run = Guid.NewGuid().ToString("N");
        var observed = new List<MediaObservation>();
        var evidence = new Dictionary<string, NativeMediaFileEvidence>(StringComparer.Ordinal);
        var diagnostics = new List<InventoryDiagnostic>();
        var timer = Stopwatch.StartNew();
        int files = 0, dirs = 0;
        long headers = 0;
        var status = InventoryScanStatus.Failed;
        var availability = InventoryAvailability.Unknown;

        void Diagnostic(string code, string relative) =>
            diagnostics.Add(new InventoryDiagnostic(code, InventorySeverity.Warning, relative,
                code, Source, began));
        void Check()
        {
            token.ThrowIfCancellationRequested();
            if (!grant.Valid(IdealMediaOperation.Index, grant.RootId, generation))
                throw new UnauthorizedAccessException("MEDIA_INDEX_GRANT_REVOKED");
            if (timer.Elapsed >= (scope.TimeLimit ?? TimeSpan.FromSeconds(30)))
                throw new RsAStopException("MEDIA_INDEX_TIMEOUT");
        }

        try
        {
            using var root = session.ClaimRoot(grant, IdealMediaOperation.Index, generation);
            availability = InventoryAvailability.Online;

            void Visit(SafeFileHandle folder, string prefix, int depth)
            {
                Check();
                if (++dirs > scope.MaxDirectories || depth > scope.MaxDepth)
                    throw new RsAStopException("MEDIA_DIRECTORY_BUDGET");
                foreach (var entry in RsAAnchoredReader.Enumerate(folder))
                {
                    Check();
                    if (entry.Name is "." or ".." || RsAAnchoredReader.Excluded(entry.Name))
                        continue;
                    var relative = prefix.Length == 0 ? entry.Name : prefix + "/" + entry.Name;
                    // Open child ONLY by its name relative to the trusted native
                    // parent; no Directory.EnumerateFiles or File.Open(path).
                    if (entry.Directory)
                    {
                        if (depth == scope.MaxDepth)
                            throw new RsAStopException("MEDIA_DEPTH_BUDGET");
                        using var nested = RsAAnchoredReader.OpenChild(folder,
                            entry.Name, isDirectory: true);
                        Visit(nested, relative, depth + 1);
                        continue;
                    }
                    if (++files > scope.MaxFiles)
                        throw new RsAStopException("MEDIA_FILE_BUDGET");
                    var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
                        continue;
                    using var child = RsAAnchoredReader.OpenMediaFile(folder, entry.Name);
                    var size = RsAAnchoredReader.Size(child);
                    var identity = RsAAnchoredReader.Identity(child);
                    var headerValid = false;
                    var format = MediaImageFormat.Unknown;
                    int? width = null, height = null;
                    var refs = new List<string>();
                    byte[] data = [];
                    if (size < 33 || size > scope.MaxImageBytes)
                    {
                        refs.Add("MEDIA_IMAGE_SIZE_LIMIT");
                    }
                    else if (headers >= scope.MaxTotalHeaderBytes ||
                             headers + Math.Min(size, scope.MaxHeaderBytes) >
                             scope.MaxTotalHeaderBytes)
                    {
                        throw new RsAStopException("MEDIA_TOTAL_HEADER_BUDGET");
                    }
                    else
                    {
                        var length = (int)Math.Min(size, scope.MaxHeaderBytes);
                        data = new byte[length];
                        using (var stream = new FileStream(child, FileAccess.Read,
                            8192, isAsync: false))
                        {
                            var offset = 0;
                            while (offset < length)
                            {
                                Check();
                                var read = stream.Read(data, offset,
                                    Math.Min(4096, length - offset));
                                if (read == 0)
                                    throw new IOException("MEDIA_HEADER_TRUNCATED");
                                offset += read;
                                headers += read;
                            }
                        }
                        format = MediaIndexReader.Classify(data, extension, scope,
                            out headerValid, out width, out height, out var diagnostic);
                        if (diagnostic is not null) refs.Add(diagnostic);
                    }
                    var parts = relative.Split('/');
                    var platform = parts.Length > 1 && scope.PlatformAliases is not null &&
                        scope.PlatformAliases.TryGetValue(parts[0], out var alias) &&
                        !string.IsNullOrWhiteSpace(alias) ? alias : "UNKNOWN";
                    var kind = parts.Length > 2 && scope.FolderKinds is not null &&
                        scope.FolderKinds.TryGetValue(parts[1], out var configured)
                            ? configured : MediaKind.Unknown;
                    var id = "media-native-" + run + "-" + observed.Count;
                    observed.Add(new MediaObservation(id, grant.RootId, "MEDIAS/" + relative,
                        platform, kind, extension, format, headerValid, size,
                        width, height, null, began, InventoryAvailability.Online,
                        Source, Array.AsReadOnly(refs.ToArray())));
                    if (headerValid)
                        evidence[id] = new NativeMediaFileEvidence(identity,
                            Convert.ToHexString(SHA256.HashData(data)), data.Length);
                }
            }

            Visit(root, "", 0);
            Check();
            status = InventoryScanStatus.Complete;
        }
        catch (OperationCanceledException) { Diagnostic("MEDIA_INDEX_CANCELLED", "MEDIAS"); }
        catch (RsAStopException ex)
        {
            status = InventoryScanStatus.Partial;
            Diagnostic(ex.Message, "MEDIAS");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or System.ComponentModel.Win32Exception or DllNotFoundException
            or EntryPointNotFoundException or PlatformNotSupportedException)
        {
            status = observed.Count > 0 ? InventoryScanStatus.Partial : InventoryScanStatus.Failed;
            Diagnostic("MEDIA_INDEX_NATIVE_DENIED_OR_OFFLINE", "MEDIAS");
            if (availability == InventoryAvailability.Unknown)
                availability = InventoryAvailability.Offline;
        }
        if (token.IsCancellationRequested && status == InventoryScanStatus.Complete)
            status = InventoryScanStatus.Partial;
        var ordered = observed.OrderBy(x => x.RelativePath, StringComparer.Ordinal).ToArray();
        var result = new MediaIndexSnapshot(run, grant.RootId, began,
            status, availability, status == InventoryScanStatus.Complete
                ? InventoryFreshness.Fresh : InventoryFreshness.Stale,
            Array.AsReadOnly(new[] { new InventoryCoverage("MEDIAS", status) }),
            Array.AsReadOnly(ordered), Array.AsReadOnly(diagnostics.ToArray()),
            ordered.Length, status == InventoryScanStatus.Complete ? ordered.Length : null);
        return new NativeMediaIndexResult(result, evidence);
    }

    /// <summary>Read precisely one previously indexed image through native
    /// parent-relative read-data handles, and decode with the existing bounded
    /// Skia PNG/JPEG pipeline. Never reconstruct an absolute source path.</summary>
    internal static MediaPreviewResult Preview(IdealMediaGrantSession session,
        IdealMediaReadGrant grant, MediaObservation media,
        NativeMediaFileEvidence evidence, MediaPreviewRequest request,
        long generation, CancellationToken token)
    {
        MediaPreviewResult Refuse(MediaPreviewStatus status, string code) =>
            new(status, code, media.MediaObservationId, Source, null, null, null, 0, null);
        if (!OperatingSystem.IsWindows() ||
            request.TargetPixelSize is < 1 or > MaxPreviewSide ||
            media.SizeBytes is null or < 33 or > MaxPreviewBytes ||
            media.Width is not > 0 or > MaxSourceSide ||
            media.Height is not > 0 or > MaxSourceSide ||
            (long)media.Width.Value * media.Height.Value > MaxPixels ||
            !media.HeaderValidated ||
            media.DetectedFormat is not (MediaImageFormat.Png or MediaImageFormat.Jpeg) ||
            media.SourceKind != Source ||
            !media.RelativePath.StartsWith("MEDIAS/", StringComparison.Ordinal))
            return Refuse(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_BUDGET_OR_SOURCE_DENIED");
        var components = media.RelativePath.Split('/');
        if (components.Length < 3 || components[0] != "MEDIAS" ||
            components.Skip(1).Any(x => x.Length == 0 || x is "." or ".." ||
                x.Contains(':') || x.Contains('\\') || x.Contains('\0') ||
                x.Length > 255))
            return Refuse(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_COMPONENT_REFUSED");
        using var root = session.ClaimRoot(grant, IdealMediaOperation.Preview,
            generation, request.MediaRunId, request.MediaObservationId);
        SafeFileHandle parent = root;
        var nested = new List<SafeFileHandle>();
        try
        {
            for (var i = 1; i < components.Length - 1; i++)
            {
                token.ThrowIfCancellationRequested();
                if (!grant.Valid(IdealMediaOperation.Preview, grant.RootId, generation))
                    return Refuse(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_GRANT_REVOKED");
                var child = RsAAnchoredReader.OpenChild(parent, components[i],
                    isDirectory: true);
                nested.Add(child);
                parent = child;
            }
            using var handle = RsAAnchoredReader.OpenMediaFile(parent, components[^1]);
            if (RsAAnchoredReader.Identity(handle) != evidence.Identity ||
                RsAAnchoredReader.Size(handle) != media.SizeBytes)
                return Refuse(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_NATIVE_IDENTITY_CHANGED");
            var data = new byte[(int)media.SizeBytes.Value];
            using (var stream = new FileStream(handle, FileAccess.Read, 8192, isAsync: false))
            {
                var offset = 0;
                var clock = Stopwatch.StartNew();
                while (offset < data.Length)
                {
                    token.ThrowIfCancellationRequested();
                    if (!grant.Valid(IdealMediaOperation.Preview, grant.RootId, generation))
                        return Refuse(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_GRANT_REVOKED");
                    if (clock.Elapsed >= TimeSpan.FromSeconds(10))
                        return Refuse(MediaPreviewStatus.Cancelled, "MEDIA_PREVIEW_READ_TIMEOUT");
                    var count = stream.Read(data, offset,
                        Math.Min(8192, data.Length - offset));
                    if (count == 0)
                        return Refuse(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_TRUNCATED");
                    offset += count;
                }
                if (stream.Length != data.Length)
                    return Refuse(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_LENGTH_CHANGED");
            }
            if (evidence.HeaderBytes > data.Length ||
                Convert.ToHexString(SHA256.HashData(data.AsSpan(0, evidence.HeaderBytes))) !=
                evidence.HeaderSha256)
                return Refuse(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_HEADER_CHANGED");
            token.ThrowIfCancellationRequested();
            var result = MediaPreviewService.Decode(data, media,
                request.TargetPixelSize, token, started: null);
            if (token.IsCancellationRequested ||
                !grant.Valid(IdealMediaOperation.Preview, grant.RootId, generation))
            {
                result.Dispose();
                return Refuse(MediaPreviewStatus.Cancelled, "MEDIA_PREVIEW_REVOKED_AFTER_DECODE");
            }
            return result;
        }
        finally
        {
            for (var i = nested.Count - 1; i >= 0; i--) nested[i].Dispose();
        }
    }
}
