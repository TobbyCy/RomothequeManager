using System.Diagnostics;
using System.Security;
using SkiaSharp;

namespace RomothequeManager.V2.Inventory.Media;

/// <summary>Independent preview outcome; Ready proves only decoding of an explicitly chosen file.</summary>
public enum MediaPreviewStatus
{
    /// <summary>PNG/JPEG decoding succeeded under a fixture grant; no game or cover is approved.</summary>
    Ready,
    /// <summary>Known but unsupported format, including WebP, rejected before Skia decoding.</summary>
    Unsupported,
    /// <summary>Permission, source membership, path or format validation failed closed.</summary>
    Rejected,
    /// <summary>The selected fixture file is unavailable or cannot be read at this attempt.</summary>
    Unavailable,
    /// <summary>Caller cancellation or timeout prevented adoption of decoded pixel data.</summary>
    Cancelled,
    /// <summary>Corrupt, inconsistent or otherwise undecodable selected media was refused.</summary>
    DecodeFailed,
    /// <summary>Snapshot generation, file metadata or selected root changed; stale pixels were discarded.</summary>
    Stale
}

/// <summary>Preview request contains only run-scoped IDs, never absolute paths or URIs.</summary>
public sealed record MediaPreviewRequest(
    string MediaRunId, string MediaRootRef, string MediaObservationId, int TargetPixelSize);

/// <summary>Owned, bounded RGBA8888 pixel buffer for a later UI adapter.</summary>
/// <remarks>Transfers no filesystem handle or Skia object to the ViewModel.
/// The recipient must dispose it when selection changes. The View must never read files.</remarks>
public sealed class DecodedMediaPreview : IDisposable
{
    private byte[]? _rgba;

    /// <summary>Adopts an already bounded RGBA8888 buffer for transfer to the read-only UI adapter.</summary>
    /// <param name="width">Decoded image width validated against the preview size limit.</param>
    /// <param name="height">Decoded image height validated against the preview size limit.</param>
    /// <param name="rgba">Owned pixel buffer cleared on Dispose; no source file handle is transferred.</param>
    internal DecodedMediaPreview(int width, int height, byte[] rgba)
    {
        Width = width;
        Height = height;
        _rgba = rgba;
    }

    /// <summary>Decoded preview width, at most 512 pixels.</summary>
    public int Width { get; }

    /// <summary>Decoded preview height, at most 512 pixels.</summary>
    public int Height { get; }

    /// <summary>Immutable-facing RGBA8888 data; invalidated on Dispose.</summary>
    public ReadOnlyMemory<byte> Pixels => _rgba ?? ReadOnlyMemory<byte>.Empty;

    /// <summary>Zero owned pixels and release the managed buffer reference.</summary>
    public void Dispose()
    {
        var pixels = Interlocked.Exchange(ref _rgba, null);
        if (pixels is not null) Array.Clear(pixels);
    }
}

/// <summary>Preview result, provenance and buffer-budget evidence; never a game decision.</summary>
/// <remarks>BufferBytesUpperBound covers explicitly managed compressed input/copy,
/// decoded RGBA and output allocations; native Skia internal workspace is not included
/// and must be measured separately before real-world enablement.</remarks>
public sealed record MediaPreviewResult(
    MediaPreviewStatus Status, string DiagnosticCode, string? MediaObservationId,
    string? SourceKind, int? SourceWidth, int? SourceHeight,
    DecodedMediaPreview? Image, long BufferBytesUpperBound,
    long? PrivateBytesDelta) : IDisposable
{
    /// <summary>Dispose the optional decoded buffer; no source media is altered.</summary>
    public void Dispose() => Image?.Dispose();
}

/// <summary>Fail-closed decoder: raw media paths never enter this public interface.</summary>
public interface IMediaPreviewService
{
    /// <summary>Load only a selected media observation from its previously bound run.</summary>
    Task<MediaPreviewResult> LoadAsync(MediaPreviewRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>Bounded PNG/JPEG preview service, with internal synthetic-fixture permission only.</summary>
/// <remarks>Production constructor refuses all image reads with REAL_MEDIA_PREVIEW_HOLD.
/// Preview permission is separate from scan permission. Does not scan, edit or persist.
/// Native codec allocation and TOCTOU on physical SSDs remain independent HOLD gates.</remarks>
public sealed class MediaPreviewService : IMediaPreviewService
{
    private const int MaxInputBytes = 8 * 1024 * 1024;
    private const int MaxSourceSide = 2048;
    private const int MaxSourcePixels = 3_500_000; // stricter than approved 4 Mpx bound
    private const int MaxPreviewSide = 512;
    private const int MaxRgbaBytes = 16 * 1024 * 1024;
    private static readonly TimeSpan DecodeTimeLimit = TimeSpan.FromSeconds(10);
    private readonly SemaphoreSlim _singleDecode = new(1, 1);
    private readonly object _bindingLock = new();
    private readonly string? _fixtureMediaRoot;
    private MediaIndexSnapshot? _snapshot;
    private long _revision;

    // Only present in friend assembly's test-only constructor, never the public ZIP.
    private readonly Action? _readChunkForTest;
    private readonly Action? _decodeStartedForTest;

    /// <summary>Product entry point: no permission, no root, no media read.</summary>
    public MediaPreviewService() { }

    /// <summary>Test-only fixture permission, restricted to a generated temp MEDIAS root.</summary>
    /// <remarks>This constructor is internal; no environment variable, CLI option or UI
    /// consent can enable it. Do not reuse this path in production composition.</remarks>
    internal MediaPreviewService(string syntheticMediaRoot, MediaIndexSnapshot snapshot,
        Action? readChunkForTest = null, Action? decodeStartedForTest = null)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var root = InventorySession.ValidateRootSyntax(syntheticMediaRoot);
        var fixtureParent = Path.GetDirectoryName(root);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var comparison = OperatingSystem.IsWindows() ?
            StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFileName(root), "MEDIAS", comparison) ||
            fixtureParent is null ||
            !Path.GetFileName(fixtureParent).StartsWith("rm-next06-fixture-", StringComparison.Ordinal) ||
            !string.Equals(Path.GetDirectoryName(fixtureParent)?.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                temp, comparison))
            throw new ArgumentException("Only owned temporary NEXT-06 synthetic MEDIAS fixtures are permitted.");
        _fixtureMediaRoot = root;
        _snapshot = snapshot;
        _readChunkForTest = readChunkForTest;
        _decodeStartedForTest = decodeStartedForTest;
    }

    /// <summary>Test-only generation change, used to prove stale replies cannot be published.</summary>
    internal void ReplaceSyntheticSnapshot(MediaIndexSnapshot newer)
    {
        ArgumentNullException.ThrowIfNull(newer);
        if (_fixtureMediaRoot is null) throw new InvalidOperationException("No test permission.");
        lock (_bindingLock)
        {
            _snapshot = newer;
            _revision++;
        }
    }

    /// <summary>Returns a bounded preview only for an exact observation in the selected, comparable fixture run.</summary>
    /// <remarks>Public construction denies all real paths. PNG/JPEG read and Skia decode are single-flight with limits, reparse checks and stale-result rejection; returned pixels are owned and disposable.</remarks>
    /// <param name="request">Run-scoped source IDs and target size, never an absolute path.</param>
    /// <param name="cancellationToken">Cancellation that must prevent adoption even if native decoding has already started.</param>
    /// <returns>A ready RGBA image or an explicit refusal/status without granting filesystem access.</returns>
    public async Task<MediaPreviewResult> LoadAsync(MediaPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        MediaPreviewResult Status(MediaPreviewStatus state, string diagnostic,
            string? id = null, string? source = null) =>
            new(state, diagnostic, id, source, null, null, null, 0, null);

        // Public service never resolves, stats or opens any path.
        if (_fixtureMediaRoot is null) return Status(MediaPreviewStatus.Rejected, "REAL_MEDIA_PREVIEW_HOLD");
        if (request.TargetPixelSize is < 1 or > MaxPreviewSide)
            return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_TARGET_LIMIT");

        MediaIndexSnapshot? snapshot;
        long version;
        lock (_bindingLock) { snapshot = _snapshot; version = _revision; }
        if (snapshot is null || snapshot.RunId != request.MediaRunId ||
            snapshot.MediaRootRef != request.MediaRootRef ||
            snapshot.ScanStatus != InventoryScanStatus.Complete ||
            snapshot.Availability != InventoryAvailability.Online ||
            snapshot.Freshness != InventoryFreshness.Fresh)
            return Status(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_RUN_NOT_COMPARABLE");

        var matches = snapshot.Observations.Where(o =>
            o.MediaObservationId == request.MediaObservationId &&
            o.MediaRootRef == request.MediaRootRef).Take(2).ToArray();
        if (matches.Length != 1) return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_UNKNOWN_OBSERVATION");
        var media = matches[0];
        if (media.DetectedFormat == MediaImageFormat.Webp)
            return Status(MediaPreviewStatus.Unsupported, "UNSUPPORTED_FORMAT",
                media.MediaObservationId, media.SourceKind);
        if (!media.HeaderValidated || media.SourceKind != "FILESYSTEM_MEDIA" ||
            media.Availability != InventoryAvailability.Online ||
            media.DetectedFormat is not (MediaImageFormat.Png or MediaImageFormat.Jpeg))
            return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_HEADER_UNTRUSTED",
                media.MediaObservationId, media.SourceKind);
        if (!ValidRelativePath(media.RelativePath, media.Extension, media.DetectedFormat))
            return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_PATH_REFUSED",
                media.MediaObservationId, media.SourceKind);
        if (media.Width is not > 0 or > MaxSourceSide ||
            media.Height is not > 0 or > MaxSourceSide ||
            (long)media.Width.Value * media.Height.Value > MaxSourcePixels ||
            media.SizeBytes is null or < 1 or > MaxInputBytes)
            return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_SOURCE_LIMIT",
                media.MediaObservationId, media.SourceKind);

        var partial = media.RelativePath["MEDIAS/".Length..];
        var full = Path.GetFullPath(Path.Combine(_fixtureMediaRoot,
            partial.Replace('/', Path.DirectorySeparatorChar)));
        var compare = OperatingSystem.IsWindows() ?
            StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!full.StartsWith(Path.TrimEndingDirectorySeparator(_fixtureMediaRoot) +
            Path.DirectorySeparatorChar, compare))
            return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_OUTSIDE_ROOT");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DecodeTimeLimit);
        var token = timeout.Token;
        var entered = false;
        try
        {
            await _singleDecode.WaitAsync(token).ConfigureAwait(false);
            entered = true;
            token.ThrowIfCancellationRequested();
            if (HasReparseComponent(full))
                return Status(MediaPreviewStatus.Rejected, "MEDIA_PREVIEW_REPARSE_REFUSED");
            var info = new FileInfo(full);
            if (!info.Exists) return Status(MediaPreviewStatus.Unavailable, "MEDIA_PREVIEW_FILE_OFFLINE");
            if (info.Length != media.SizeBytes ||
                info.LastWriteTimeUtc != media.ModifiedUtc?.UtcDateTime)
                return Status(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_FILE_CHANGED");
            var length = checked((int)info.Length);
            var bytes = new byte[length];
            await using (var stream = new FileStream(full, FileMode.Open,
                FileAccess.Read, FileShare.Read, 8192, FileOptions.Asynchronous))
            {
                if (stream.Length != info.Length ||
                    HasReparseComponent(full))
                    return Status(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_RACED_ENTRY");
                var offset = 0;
                while (offset < bytes.Length)
                {
                    token.ThrowIfCancellationRequested();
                    var n = await stream.ReadAsync(bytes.AsMemory(offset,
                        Math.Min(8192, bytes.Length - offset)), token).ConfigureAwait(false);
                    if (n == 0) return Status(MediaPreviewStatus.DecodeFailed, "MEDIA_PREVIEW_TRUNCATED");
                    offset += n;
                    _readChunkForTest?.Invoke();
                }
                if (stream.Length != info.Length)
                    return Status(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_CHANGED_DURING_READ");
            }

            token.ThrowIfCancellationRequested();
            if (new FileInfo(full).LastWriteTimeUtc != info.LastWriteTimeUtc ||
                HasReparseComponent(full))
                return Status(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_CHANGED_AFTER_READ");
            if (!MatchesMagic(bytes, media.DetectedFormat))
                return Status(MediaPreviewStatus.DecodeFailed, "MEDIA_PREVIEW_SIGNATURE_CHANGED");

            // One bounded decoder at a time. A native decode itself is not interruptible;
            // cancelled or stale results are dropped BEFORE being published to a ViewModel.
            var beforePrivate = Process.GetCurrentProcess().PrivateMemorySize64;
            var decoded = await Task.Run(() => Decode(bytes, media, request.TargetPixelSize,
                token, _decodeStartedForTest), CancellationToken.None).ConfigureAwait(false);
            var afterPrivate = Process.GetCurrentProcess().PrivateMemorySize64;
            if (token.IsCancellationRequested)
            {
                decoded.Image?.Dispose();
                return Status(MediaPreviewStatus.Cancelled, "MEDIA_PREVIEW_CANCELLED");
            }
            lock (_bindingLock)
            {
                if (version != _revision || _snapshot?.RunId != request.MediaRunId ||
                    _snapshot?.MediaRootRef != request.MediaRootRef)
                {
                    decoded.Image?.Dispose();
                    return Status(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_GENERATION_CHANGED");
                }
            }
            return decoded with { PrivateBytesDelta = afterPrivate - beforePrivate };
        }
        catch (OperationCanceledException)
        {
            return Status(MediaPreviewStatus.Cancelled, "MEDIA_PREVIEW_CANCELLED");
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or SecurityException)
        {
            return Status(MediaPreviewStatus.Unavailable, "MEDIA_PREVIEW_UNREADABLE");
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or
            NotSupportedException or OverflowException or TypeInitializationException or
            DllNotFoundException)
        {
            return Status(MediaPreviewStatus.DecodeFailed, "MEDIA_PREVIEW_DECODE_ERROR");
        }
        finally
        {
            if (entered) _singleDecode.Release();
        }
    }

    private static MediaPreviewResult Decode(byte[] bytes, MediaObservation media,
        int target, CancellationToken token, Action? started)
    {
        token.ThrowIfCancellationRequested();
        started?.Invoke();
        token.ThrowIfCancellationRequested();
        using var data = SKData.CreateCopy(bytes);
        using var codec = SKCodec.Create(data);
        if (codec is null) return Invalid("MEDIA_PREVIEW_INVALID_CODEC");
        if (codec.EncodedFormat != (media.DetectedFormat == MediaImageFormat.Png ?
                SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg))
            return Invalid("MEDIA_PREVIEW_CODEC_FORMAT_MISMATCH");
        var actual = codec.Info;
        if (actual.Width != media.Width || actual.Height != media.Height ||
            actual.Width < 1 || actual.Height < 1 ||
            actual.Width > MaxSourceSide || actual.Height > MaxSourceSide ||
            (long)actual.Width * actual.Height > MaxSourcePixels ||
            codec.FrameCount > 1)
            return Invalid("MEDIA_PREVIEW_DECODE_DIMENSION_LIMIT");

        int w = actual.Width, h = actual.Height;
        var outputW = Math.Max(1, (int)Math.Floor(w *
            Math.Min(1d, Math.Min((double)target / w, (double)target / h))));
        var outputH = Math.Max(1, (int)Math.Floor(h *
            Math.Min(1d, Math.Min((double)target / w, (double)target / h))));
        var fullBytes = checked(w * h * 4);
        var outputBytes = checked(outputW * outputH * 4);
        var buffers = checked((long)bytes.Length * 2 + fullBytes +
            (outputW == w && outputH == h ? 0 : outputBytes));
        if (fullBytes + (outputW == w && outputH == h ? 0 : outputBytes) > MaxRgbaBytes ||
            outputW > MaxPreviewSide || outputH > MaxPreviewSide)
            return Invalid("MEDIA_PREVIEW_BUFFER_LIMIT");
        var info = new SKImageInfo(w, h, SKColorType.Rgba8888, SKAlphaType.Premul);
        var rgba = new byte[fullBytes];
        var decodeResult = codec.GetPixels(info, rgba);
        if (decodeResult != SKCodecResult.Success)
        {
            Array.Clear(rgba);
            return Invalid("MEDIA_PREVIEW_INVALID_PIXEL_STREAM");
        }
        token.ThrowIfCancellationRequested();
        byte[] pixels;
        if (outputW == w && outputH == h) pixels = rgba;
        else
        {
            pixels = new byte[outputBytes];
            for (var y = 0; y < outputH; y++)
            {
                token.ThrowIfCancellationRequested();
                var sourceY = Math.Min(h - 1, (int)((long)y * h / outputH));
                for (var x = 0; x < outputW; x++)
                {
                    var sourceX = Math.Min(w - 1, (int)((long)x * w / outputW));
                    Buffer.BlockCopy(rgba, (sourceY * w + sourceX) * 4,
                        pixels, (y * outputW + x) * 4, 4);
                }
            }
            Array.Clear(rgba);
        }
        token.ThrowIfCancellationRequested();
        return new MediaPreviewResult(MediaPreviewStatus.Ready,
            "MEDIA_PREVIEW_DECODED_ONLY_NOT_ASSOCIATED", media.MediaObservationId,
            media.SourceKind, w, h, new DecodedMediaPreview(outputW, outputH, pixels),
            buffers, null);

        MediaPreviewResult Invalid(string diagnostic) => new(MediaPreviewStatus.DecodeFailed,
            diagnostic, media.MediaObservationId, media.SourceKind, null, null, null, 0, null);
    }

    private static bool MatchesMagic(byte[] b, MediaImageFormat format) =>
        format switch
        {
            MediaImageFormat.Png => b.Length >= 33 &&
                b.AsSpan(0, 8).SequenceEqual(new byte[] {137, 80, 78, 71, 13, 10, 26, 10}),
            MediaImageFormat.Jpeg => b.Length >= 4 &&
                b[0] == 255 && b[1] == 216,
            _ => false
        };

    private static bool ValidRelativePath(string relative, string extension,
        MediaImageFormat format)
    {
        if (!relative.StartsWith("MEDIAS/", StringComparison.Ordinal) ||
            relative.IndexOf('\\') >= 0 || relative.Contains("://", StringComparison.Ordinal) ||
            relative.Contains('\0') || relative.StartsWith("//", StringComparison.Ordinal) ||
            Path.IsPathRooted(relative))
            return false;
        var parts = relative.Split('/');
        if (parts.Length < 3 || parts.Any(p => p.Length == 0 || p is "." or ".." ||
            p.Contains(':') || p is "ROMOTHEQUE_MANAGER" or "backups" or "logs" or "data" or ".git"))
            return false;
        var ext = Path.GetExtension(relative);
        return string.Equals(ext, extension, StringComparison.OrdinalIgnoreCase) &&
            (format == MediaImageFormat.Png ? ext.Equals(".png", StringComparison.OrdinalIgnoreCase) :
             ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
             ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase));
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
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }
}
