using Avalonia.Media.Imaging;

namespace RomothequeManager.V2;

/// <summary>UI-visible result phases for one explicitly accepted ES-DE media reference.
/// A refused or unavailable reference never establishes that a media file is absent.</summary>
internal enum LegacyMediaPhase { Idle, Loading, Ready, Refused, Unavailable, Cancelled, Stale }

/// <summary>One bounded request against the exact accepted session and ES-DE snapshot.
/// References and their order originate only from the selected game's declared XML media.</summary>
internal sealed record LegacyMediaRequest(
    EsDeImportSession Session, EsDeCatalogueSnapshot Snapshot, EsDeGame Game,
    IReadOnlyList<EsDeMedia> Candidates, LegacyImageSlot Slot, bool ExplicitConsent);

/// <summary>Disposable ownership envelope; the decoder bitmap remains owned until
/// <see cref="TakeBitmap"/> explicitly transfers it to the Avalonia View.</summary>
internal sealed class LegacyMediaLoadResult : IDisposable
{
    private Bitmap? _owned;

    /// <summary>Outcome of the read, validation and bounded decode, never a media association.</summary>
    internal LegacyMediaPhase Phase { get; }
    /// <summary>Neutral diagnostic code, never an absolute path or exception message.</summary>
    internal string Reason { get; }
    /// <summary>Estimated retained RGBA bytes if the View adopts the decoded bitmap.</summary>
    internal long RetainedBytes { get; }

    private LegacyMediaLoadResult(LegacyMediaPhase phase, string reason,
        Bitmap? owned, long retainedBytes)
    {
        Phase = phase;
        Reason = reason;
        _owned = owned;
        RetainedBytes = retainedBytes;
    }

    /// <summary>Create a result that owns no native bitmap.</summary>
    internal static LegacyMediaLoadResult WithoutImage(LegacyMediaPhase phase, string reason) =>
        new(phase, reason, null, 0);

    /// <summary>Wrap one decoded bitmap; its ownership is not yet transferred to a View.</summary>
    internal static LegacyMediaLoadResult WithImage(Bitmap bitmap, long retainedBytes) =>
        new(LegacyMediaPhase.Ready, "DECODE_OK", bitmap, retainedBytes);

    /// <summary>Transfer the decoded bitmap exactly once; a later Dispose cannot dispose
    /// a bitmap now owned by the View. Returns null for refusal or repeated transfer.</summary>
    internal Bitmap? TakeBitmap()
    {
        var bitmap = _owned;
        _owned = null;
        return bitmap;
    }

    /// <summary>Release an untransferred bitmap exactly once on Cancel, Stale or refusal.</summary>
    public void Dispose()
    {
        var bitmap = _owned;
        _owned = null;
        bitmap?.Dispose();
    }
}

/// <summary>Injectable bounded loader; no UI composition, path resolution or permission
/// grant is permitted at this boundary.</summary>
internal interface ILegacyImportedMediaLoader : IDisposable
{
    /// <summary>Open only via EsDeImportSession.TryOpenMedia and decode within #141 budgets.</summary>
    Task<LegacyMediaLoadResult> LoadAsync(LegacyMediaRequest request, CancellationToken token);

    /// <summary>Number of native decoder entries for synthetic Windows diagnostics.</summary>
    int NativeDecoderEntered { get; }

    /// <summary>Peak simultaneous decodes; the per-window single-flight contract requires one.</summary>
    int PeakActiveDecodes { get; }
}

/// <summary>Serialized read-only ES-DE media loader for one window. The existing session
/// is the only opener and LegacyImportedImageBudget is the only decoder/budget policy.</summary>
internal sealed class LegacyImportedMediaLoader : ILegacyImportedMediaLoader
{
    private readonly object _lifetime = new();
    private readonly SemaphoreSlim _singleFlight = new(1, 1);
    private bool _disposed;
    private int _outstanding;
    private int _nativeDecoderEntered;
    private int _activeDecodes;
    private int _peakActiveDecodes;

    /// <summary>Actual guarded decoder entries, not merely successful file opens.</summary>
    public int NativeDecoderEntered => Volatile.Read(ref _nativeDecoderEntered);

    /// <summary>Peak concurrently active guarded decode operations.</summary>
    public int PeakActiveDecodes => Volatile.Read(ref _peakActiveDecodes);

    /// <summary>Validate accepted membership, serialize source opening and bounded decoding,
    /// then return an owned result. No stream escapes this service.</summary>
    public async Task<LegacyMediaLoadResult> LoadAsync(LegacyMediaRequest request,
        CancellationToken token)
    {
        lock (_lifetime)
        {
            if (_disposed)
                return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Refused,
                    "MEDIA_LOADER_CLOSED");
            _outstanding++;
        }
        var entered = false;
        try
        {
            if (!IsDeclaredAccepted(request))
                return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Refused,
                    "MEDIA_NOT_AUTHORIZED");
            await _singleFlight.WaitAsync(token).ConfigureAwait(false);
            entered = true;
            return await Task.Run(() => LoadUnderGate(request, token), token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Cancelled,
                "MEDIA_CANCELLED");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or ArgumentException or InvalidDataException or InvalidOperationException
            or NotSupportedException or ObjectDisposedException)
        {
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Refused,
                "MEDIA_READ_REFUSED");
        }
        finally
        {
            if (entered) _singleFlight.Release();
            lock (_lifetime)
            {
                --_outstanding;
                if (_disposed && _outstanding == 0) _singleFlight.Dispose();
            }
        }
    }

    private LegacyMediaLoadResult LoadUnderGate(LegacyMediaRequest request,
        CancellationToken token)
    {
        var last = "MEDIA_NOT_AVAILABLE";
        if (token.IsCancellationRequested)
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Cancelled,
                "MEDIA_CANCELLED");
        if (!IsDeclaredAccepted(request))
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Stale,
                "MEDIA_STALE_GENERATION");

        foreach (var candidate in request.Candidates)
        {
            token.ThrowIfCancellationRequested();
            if (!IsDeclaredAccepted(request))
                return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Stale,
                    "MEDIA_STALE_GENERATION");
            // Exists in an earlier XML import is not permission or proof of live presence.
            if (!candidate.Exists) continue;
            if (!request.Session.TryOpenMedia(candidate, out var stream) || stream is null)
                continue;
            LegacyDecodeResult decode;
            using (stream)
            {
                var active = Interlocked.Increment(ref _activeDecodes);
                var peak = Volatile.Read(ref _peakActiveDecodes);
                while (active > peak &&
                    Interlocked.CompareExchange(ref _peakActiveDecodes, active, peak) != peak)
                    peak = Volatile.Read(ref _peakActiveDecodes);
                try
                {
                    decode = LegacyImportedImageBudget.Decode(stream, request.Slot, token,
                        () => Interlocked.Increment(ref _nativeDecoderEntered));
                }
                finally { Interlocked.Decrement(ref _activeDecodes); }
            }
            last = decode.Reason;
            if (decode.Bitmap is null) continue;
            if (token.IsCancellationRequested || !IsDeclaredAccepted(request))
            {
                decode.Bitmap.Dispose();
                return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Stale,
                    "MEDIA_STALE_GENERATION");
            }
            return LegacyMediaLoadResult.WithImage(decode.Bitmap, decode.RetainedBytes);
        }
        if (token.IsCancellationRequested)
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Cancelled,
                "MEDIA_CANCELLED");
        return LegacyMediaLoadResult.WithoutImage(
            last == "MEDIA_NOT_AVAILABLE" ? LegacyMediaPhase.Unavailable : LegacyMediaPhase.Refused,
            last);
    }

    private static bool IsDeclaredAccepted(LegacyMediaRequest? request)
    {
        if (request is null || !request.ExplicitConsent ||
            !ReferenceEquals(request.Session.Current, request.Snapshot) ||
            !request.Snapshot.Games.Any(game => ReferenceEquals(game, request.Game)) ||
            request.Candidates is null || request.Candidates.Count is < 1 or > 50)
            return false;
        foreach (var media in request.Candidates)
        {
            if (media is null || !request.Game.Media.Any(x => ReferenceEquals(x, media)))
                return false;
            if (request.Slot == LegacyImageSlot.Gallery)
            {
                if (media.Kind is not ("image" or "thumbnail" or "marquee"))
                    return false;
            }
            else if (media.Kind is not ("image" or "thumbnail"))
                return false;
        }
        return true;
    }

    /// <summary>Stop accepting work; already returned images remain owned by the caller.
    /// The semaphore is released after all pending loads exit, never during active decoding.</summary>
    public void Dispose()
    {
        lock (_lifetime)
        {
            if (_disposed) return;
            _disposed = true;
            if (_outstanding == 0) _singleFlight.Dispose();
        }
    }
}
