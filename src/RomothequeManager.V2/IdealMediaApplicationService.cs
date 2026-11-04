using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.Inventory.Application;

/// <summary>Independent production media reader. Requires a separately selected
/// local MEDIAS root and distinct affirmative user actions for INDEX and PREVIEW.
/// This service never reads ROMS, restores a grant or accesses XML paths.</summary>
public sealed class IdealMediaApplicationService : IDisposable
{
    private readonly object _sync = new();
    private readonly IdealMediaGrantSession _session = new();
    private MediaRootScope? _scope;
    private IdealMediaReadGrant? _indexGrant;
    private IdealMediaReadGrant? _previewGrant;
    private CancellationTokenSource? _cancelIndex;
    private CancellationTokenSource? _cancelPreview;
    private Task<MediaIndexSnapshot>? _activeIndex;
    private bool _indexRunning, _previewRunning, _disposed;
    private MediaIndexSnapshot? _current;
    private MediaIndexSnapshot? _lastAttempt;
    private IReadOnlyDictionary<string, NativeMediaFileEvidence> _fileEvidence =
        new Dictionary<string, NativeMediaFileEvidence>();

    /// <summary>Creates a closed media service with no selected source or grant.</summary>
    public IdealMediaApplicationService() { }

    /// <summary>Last complete, online native MEDIAS generation from the currently
    /// selected root; incomplete attempts never overwrite this snapshot.</summary>
    public MediaIndexSnapshot? Current { get { lock (_sync) return _current; } }

    /// <summary>Last attempted index, including refusals, partial and failed scans.</summary>
    public MediaIndexSnapshot? LastAttempt { get { lock (_sync) return _lastAttempt; } }

    /// <summary>Select MEDIAS independently. This operation validates only a
    /// local root syntax; it never opens files or issues a read capability.</summary>
    public bool PrepareMediaSelection(MediaRootScope scope, string mediaRootId)
    {
        ArgumentNullException.ThrowIfNull(scope);
        lock (_sync)
        {
            if (_disposed) return false;
            _indexGrant?.Revoke();
            _previewGrant?.Revoke();
            _session.Revoke();
            if (_indexRunning || _previewRunning)
            {
                _cancelIndex?.Cancel();
                _cancelPreview?.Cancel();
                return false;
            }
            _scope = null;
            _current = null;
            _lastAttempt = null;
            _fileEvidence = new Dictionary<string, NativeMediaFileEvidence>();
            _indexGrant = null;
            _previewGrant = null;
            try
            {
                _session.Select(scope.MediaRoot, mediaRootId);
                _scope = scope;
                return true;
            }
            catch (Exception error) when (error is ArgumentException
                or UnauthorizedAccessException or IOException
                or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
            { return false; }
        }
    }

    /// <summary>Issue a volatile, index-only capability after a distinct user
    /// click. A ROMS consent or generic Start confirmation is never sufficient.</summary>
    public bool AuthorizeMediaIndex(bool explicitUserAction)
    {
        lock (_sync)
        {
            if (_disposed || _scope is null || _indexRunning || _previewRunning)
                return false;
            _indexGrant?.Revoke();
            _indexGrant = null;
            try
            {
                _indexGrant = _session.AuthorizeIndex(explicitUserAction,
                    DateTimeOffset.UtcNow.AddMinutes(2));
                return true;
            }
            catch (Exception error) when (error is ArgumentException
                or UnauthorizedAccessException or IOException
                or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
            { return false; }
        }
    }

    /// <summary>Run a single budgeted native MEDIAS scan with a previously granted,
    /// one-shot INDEX capability. It returns a failed snapshot without source I/O
    /// if the user never separately authorized indexing.</summary>
    public Task<MediaIndexSnapshot> StartIndexAsync(CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_disposed || _scope is null || _indexRunning ||
                _previewRunning || _indexGrant is null ||
                !_indexGrant.Valid(IdealMediaOperation.Index, _indexGrant.RootId,
                    _session.Generation))
                return Task.FromResult(Denied("MEDIA_INDEX_EXPLICIT_GRANT_REQUIRED"));
            var grant = _indexGrant;
            _indexGrant = null;
            _previewGrant?.Revoke();
            _previewGrant = null;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _cancelIndex = cts;
            _indexRunning = true;
            var generation = _session.Generation;
            var scope = _scope;
            return _activeIndex = RunIndexAsync(grant, scope, generation, cts);
        }
    }

    private async Task<MediaIndexSnapshot> RunIndexAsync(IdealMediaReadGrant grant,
        MediaRootScope scope, long generation, CancellationTokenSource cts)
    {
        NativeMediaIndexResult? result = null;
        try
        {
            result = await Task.Run(() =>
                NativeIdealMediaReader.Index(_session, grant, scope,
                    generation, cts.Token)).ConfigureAwait(false);
        }
        catch (Exception) { /* native failures are safe, sanitized in the result */ }
        lock (_sync)
        {
            var wasCancelled = cts.IsCancellationRequested;
            grant.Revoke();
            _cancelIndex = null;
            _indexRunning = false;
            cts.Dispose();
            if (_disposed || generation != _session.Generation) return Denied("MEDIA_INDEX_STALE");
            var snapshot = result?.Snapshot ?? Denied("MEDIA_INDEX_NATIVE_FAILED");
            if (wasCancelled && snapshot.ScanStatus == InventoryScanStatus.Complete)
                snapshot = snapshot with {
                    ScanStatus = InventoryScanStatus.Partial,
                    Freshness = InventoryFreshness.Stale,
                    TotalFileCount = null,
                    CoverageProof = Array.AsReadOnly(new[] {
                        new InventoryCoverage("MEDIAS", InventoryScanStatus.Partial) })
                };
            _lastAttempt = snapshot;
            if (snapshot.ScanStatus == InventoryScanStatus.Complete &&
                snapshot.Availability == InventoryAvailability.Online &&
                !wasCancelled && result is not null &&
                _session.AcceptNativeIndex(snapshot, generation))
            {
                _current = snapshot;
                _fileEvidence = result.FileEvidence;
            }
            return snapshot;
        }
    }

    /// <summary>Issue an independent PREVIEW-only capability for exactly one
    /// observation in Current. A valid INDEX grant cannot authorize a preview.</summary>
    public bool AuthorizeMediaPreview(MediaPreviewRequest request, bool explicitUserAction)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            if (_disposed || _indexRunning || _previewRunning || _current is null ||
                request.MediaRunId != _current.RunId ||
                request.MediaRootRef != _current.MediaRootRef ||
                !_fileEvidence.ContainsKey(request.MediaObservationId))
                return false;
            _previewGrant?.Revoke();
            _previewGrant = null;
            try
            {
                _previewGrant = _session.AuthorizePreview(request.MediaRunId,
                    request.MediaObservationId, explicitUserAction,
                    DateTimeOffset.UtcNow.AddMinutes(2));
                return true;
            }
            catch (Exception error) when (error is ArgumentException
                or UnauthorizedAccessException or IOException
                or PlatformNotSupportedException or System.ComponentModel.Win32Exception)
            { return false; }
        }
    }

    /// <summary>Read one approved image from native handles and decode a bounded
    /// PNG/JPEG preview. Does not use a path-based file opener or ROMS permission.</summary>
    public Task<MediaPreviewResult> LoadPreviewAsync(MediaPreviewRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        lock (_sync)
        {
            if (_disposed || _indexRunning || _previewRunning || _current is null ||
                request.MediaRunId != _current.RunId ||
                request.MediaRootRef != _current.MediaRootRef ||
                _previewGrant is null ||
                !_previewGrant.Valid(IdealMediaOperation.Preview,
                    request.MediaRootRef, _session.Generation) ||
                !_fileEvidence.TryGetValue(request.MediaObservationId, out var evidence))
                return Task.FromResult(Refused(MediaPreviewStatus.Rejected,
                    "MEDIA_PREVIEW_EXPLICIT_GRANT_REQUIRED"));
            var media = _current.Observations.SingleOrDefault(x =>
                x.MediaObservationId == request.MediaObservationId);
            if (media is null) return Task.FromResult(Refused(MediaPreviewStatus.Rejected,
                "MEDIA_PREVIEW_UNKNOWN_OBSERVATION"));
            var grant = _previewGrant;
            _previewGrant = null;
            var generation = _session.Generation;
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(10)); // results after deadline are discarded
            _cancelPreview = cts;
            _previewRunning = true;
            return RunPreviewAsync(grant, media, evidence, request, generation, cts);
        }
    }

    private async Task<MediaPreviewResult> RunPreviewAsync(IdealMediaReadGrant grant,
        MediaObservation media, NativeMediaFileEvidence evidence, MediaPreviewRequest request,
        long generation, CancellationTokenSource cts)
    {
        MediaPreviewResult outcome;
        try
        {
            outcome = await Task.Run(() => NativeIdealMediaReader.Preview(_session,
                grant, media, evidence, request, generation, cts.Token))
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { outcome = Refused(MediaPreviewStatus.Cancelled,
            "MEDIA_PREVIEW_CANCELLED"); }
        catch (Exception) { outcome = Refused(MediaPreviewStatus.Unavailable,
            "MEDIA_PREVIEW_NATIVE_DENIED_OR_UNAVAILABLE"); }
        lock (_sync)
        {
            var cancelled = cts.IsCancellationRequested;
            var grantStillValid = grant.Valid(IdealMediaOperation.Preview,
                request.MediaRootRef, generation);
            grant.Revoke();
            _cancelPreview = null;
            _previewRunning = false;
            cts.Dispose();
            if (cancelled || _disposed || !grantStillValid ||
                generation != _session.Generation ||
                _current?.RunId != request.MediaRunId)
            {
                outcome.Dispose();
                return Refused(MediaPreviewStatus.Stale, "MEDIA_PREVIEW_LATE_OR_REVOKED");
            }
            return outcome;
        }
    }

    /// <summary>Immediately revoke both media operations and request cooperative
    /// cancellation. A late index or image result cannot be adopted.</summary>
    public bool RequestCancellation()
    {
        lock (_sync)
        {
            if (_disposed) return false;
            var active = _indexRunning || _previewRunning;
            _session.Revoke();
            _indexGrant?.Revoke();
            _previewGrant?.Revoke();
            _cancelIndex?.Cancel();
            _cancelPreview?.Cancel();
            _indexGrant = null;
            _previewGrant = null;
            return active;
        }
    }

    private static MediaIndexSnapshot Denied(string diagnostic)
    {
        var at = DateTimeOffset.UtcNow;
        return new MediaIndexSnapshot("media-denied-" + Guid.NewGuid().ToString("N"),
            "no-media-grant", at, InventoryScanStatus.Failed,
            InventoryAvailability.Unknown, InventoryFreshness.Unknown,
            Array.AsReadOnly(new[] { new InventoryCoverage("MEDIAS", InventoryScanStatus.Failed) }),
            Array.AsReadOnly(Array.Empty<MediaObservation>()),
            Array.AsReadOnly(new[] { new InventoryDiagnostic(diagnostic,
                InventorySeverity.Warning, "MEDIAS", diagnostic, "IDEAL_NATIVE_MEDIA", at) }),
            0, null);
    }

    private static MediaPreviewResult Refused(MediaPreviewStatus status, string diagnostic) =>
        new(status, diagnostic, null, "IDEAL_NATIVE_MEDIA", null, null, null, 0, null);

    /// <summary>Close the session, revoke all capabilities and cancel active
    /// workers; no selected path, pixel buffer or handle is persisted.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            _disposed = true;
            _session.Dispose();
            _indexGrant?.Revoke();
            _previewGrant?.Revoke();
            _cancelIndex?.Cancel();
            _cancelPreview?.Cancel();
            _scope = null;
            _current = null;
            _lastAttempt = null;
            _fileEvidence = new Dictionary<string, NativeMediaFileEvidence>();
        }
    }
}
