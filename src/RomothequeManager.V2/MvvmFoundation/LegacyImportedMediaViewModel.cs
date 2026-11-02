using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Media.Imaging;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>One immutable, generation-bound presentation request. It contains only accepted
/// ES-DE identities and never a path, file stream, decoder or filesystem permission.</summary>
internal sealed class LegacyMediaWork
{
    private int _completed;

    /// <summary>The already accepted session, snapshot, game and declared media candidates.</summary>
    internal LegacyMediaRequest Request { get; }
    /// <summary>View generation at the moment this request was queued.</summary>
    internal int Generation { get; }
    /// <summary>Cancellation from the current view generation; it is never persisted.</summary>
    internal CancellationToken Token { get; }

    /// <summary>Bind one work item to a previously validated generation.</summary>
    internal LegacyMediaWork(LegacyMediaRequest request, int generation, CancellationToken token)
    {
        Request = request;
        Generation = generation;
        Token = token;
    }

    /// <summary>Mark completion once so repeated callbacks never decrement counters twice.</summary>
    internal bool TryComplete() => Interlocked.Exchange(ref _completed, 1) == 0;
}

/// <summary>Presentation-only coordinator for historical ES-DE media. The injected loader
/// owns all reads and decoding; only the Avalonia View can own transferred bitmaps.</summary>
internal sealed class LegacyImportedMediaViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly ImportedCatalogueUiState _state;
    private readonly ImportedCatalogueViewModel _importVm;
    private readonly ILegacyImportedMediaLoader _loader;
    private readonly InventoryCommand _cancel;
    private readonly List<CancellationTokenSource> _retired = new();
    private CancellationTokenSource _generationCancellation = new();
    private int _generation;
    private int _pending;
    private int _outstanding;
    private long _retainedBytes;
    private bool _disposed;
    private LegacyMediaPhase _phase = LegacyMediaPhase.Idle;
    private string _diagnostic = "MEDIA_NOT_REQUESTED";

    /// <summary>Bind accepted import state to an injectable loader; construction does not load any media.</summary>
    internal LegacyImportedMediaViewModel(ImportedCatalogueUiState state,
        ImportedCatalogueViewModel importVm, ILegacyImportedMediaLoader? loader = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _importVm = importVm ?? throw new ArgumentNullException(nameof(importVm));
        _loader = loader ?? new LegacyImportedMediaLoader();
        _cancel = new InventoryCommand(_ => CancelOutstanding(), _ => CanCancel);
    }

    /// <summary>UI notifications for request status, cancellation and budget accounting.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Explicitly invalidates pending visual loads without modifying the catalogue.</summary>
    internal ICommand CancelCommand => _cancel;

    /// <summary>Most recent presentation status; not proof of a file's current existence.</summary>
    internal LegacyMediaPhase Phase => _phase;

    /// <summary>Neutral status code without media path or exception detail.</summary>
    internal string Diagnostic => _diagnostic;

    /// <summary>Whether this view currently has requests eligible for cancellation.</summary>
    internal bool CanCancel => !_disposed && _pending > 0;

    /// <summary>Monotonic visual generation guarding navigation, refresh and late results.</summary>
    internal int Generation => _generation;

    /// <summary>Number of queued/active requests for the current view; at most fifty.</summary>
    internal int Pending => _pending;

    /// <summary>Logical RGBA bytes owned by the current View after successful transfer.</summary>
    internal long RetainedBytes => _retainedBytes;

    /// <summary>Native decoder entry counter from the injected single-flight loader.</summary>
    internal int NativeDecoderEntered => _loader.NativeDecoderEntered;

    /// <summary>Peak active decode count from the injected per-window loader.</summary>
    internal int PeakActiveDecodes => _loader.PeakActiveDecodes;

    /// <summary>Queue only media declared by a game belonging to both accepted UI snapshot
    /// and its underlying session Current. Imported mode requires a previous explicit import
    /// through the View's folder picker and read-only confirmation (or an internal CI fixture).</summary>
    internal bool TryBegin(EsDeGame game, IReadOnlyList<EsDeMedia> candidates,
        LegacyImageSlot slot, out LegacyMediaWork? work)
    {
        work = null;
        var session = _state.Session;
        var snapshot = _state.Snapshot;
        if (_disposed || _importVm.IsBusy || !_state.IsImported || session is null ||
            snapshot is null || !ReferenceEquals(session.Current, snapshot) ||
            !snapshot.Games.Any(g => ReferenceEquals(g, game)) ||
            candidates is null || candidates.Count is < 1 or > 50 || _pending >= 50 ||
            candidates.Any(c => c is null || !game.Media.Any(m => ReferenceEquals(m, c))))
        {
            if (!_disposed) SetPhase(LegacyMediaPhase.Refused, "MEDIA_NOT_AUTHORIZED_OR_QUEUE_FULL");
            return false;
        }

        // The accepted imported state is the result of the explicit UI read-only
        // consent, not a new grant. The service revalidates every individual opener.
        var request = new LegacyMediaRequest(session, snapshot, game,
            candidates.ToArray(), slot, ExplicitConsent: true);
        work = new LegacyMediaWork(request, _generation, _generationCancellation.Token);
        _pending++;
        _outstanding++;
        SetPhase(LegacyMediaPhase.Loading, "MEDIA_LOADING");
        Publish(nameof(Pending));
        return true;
    }

    /// <summary>Request the injected service and suppress/Dispose any result that becomes stale
    /// after cancellation, navigation, an accepted refresh or View disposal.</summary>
    internal async Task<LegacyMediaLoadResult> LoadAsync(LegacyMediaWork work)
    {
        if (!IsCurrent(work))
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Stale,
                "MEDIA_STALE_GENERATION");
        LegacyMediaLoadResult result;
        try
        {
            result = await _loader.LoadAsync(work.Request, work.Token);
        }
        catch (OperationCanceledException)
        {
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Cancelled,
                "MEDIA_CANCELLED");
        }
        catch (Exception)
        {
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Refused,
                "MEDIA_READ_REFUSED");
        }

        if (!IsCurrent(work))
        {
            result.Dispose();
            return LegacyMediaLoadResult.WithoutImage(LegacyMediaPhase.Stale,
                "MEDIA_STALE_GENERATION");
        }
        SetPhase(result.Phase, result.Reason);
        return result;
    }

    /// <summary>Atomically reserve per-view retained bytes and transfer a decoded bitmap to
    /// the calling View. The View alone must attach, detach and Dispose that bitmap.</summary>
    internal bool TryTransfer(LegacyMediaWork work, LegacyMediaLoadResult result,
        out Bitmap? bitmap, out long reservedBytes)
    {
        bitmap = null;
        reservedBytes = 0;
        if (!IsCurrent(work) || result.Phase != LegacyMediaPhase.Ready ||
            result.RetainedBytes <= 0 ||
            result.RetainedBytes > LegacyImportedImageBudget.MaxViewBytes - _retainedBytes)
        {
            if (IsCurrent(work)) SetPhase(LegacyMediaPhase.Refused,
                "MEDIA_BUDGET_EXHAUSTED_OR_STALE");
            return false;
        }
        bitmap = result.TakeBitmap();
        if (bitmap is null) return false;
        reservedBytes = result.RetainedBytes;
        _retainedBytes += reservedBytes;
        Publish(nameof(RetainedBytes));
        return true;
    }

    /// <summary>Roll back one reservation if the View could not attach its transferred bitmap.</summary>
    internal void ReleaseTransfer(LegacyMediaWork work, long reservedBytes)
    {
        if (work.Generation != _generation || reservedBytes <= 0) return;
        _retainedBytes = Math.Max(0, _retainedBytes - reservedBytes);
        Publish(nameof(RetainedBytes));
    }

    /// <summary>Finish one request exactly once; an obsolete generation cannot change
    /// current counters or notify an already disposed window.</summary>
    internal void Complete(LegacyMediaWork work)
    {
        if (!work.TryComplete()) return;
        _outstanding = Math.Max(0, _outstanding - 1);
        if (work.Generation == _generation && !_disposed)
        {
            _pending = Math.Max(0, _pending - 1);
            Publish(nameof(Pending));
            if (_pending == 0 && _phase == LegacyMediaPhase.Loading)
                SetPhase(LegacyMediaPhase.Idle, "MEDIA_READY_FOR_SELECTION");
        }
        ReleaseRetiredSourcesIfIdle();
    }

    /// <summary>Cancel and invalidate the entire view generation before the View detaches
    /// and disposes its own bitmaps. No session, snapshot or media identity is changed.</summary>
    internal void CancelOutstanding()
    {
        ++_generation;
        var prior = _generationCancellation;
        prior.Cancel();
        _retired.Add(prior);
        _generationCancellation = new CancellationTokenSource();
        _pending = 0;
        _retainedBytes = 0;
        if (!_disposed)
        {
            SetPhase(LegacyMediaPhase.Cancelled, "MEDIA_GENERATION_CANCELLED");
            Publish(nameof(Generation));
            Publish(nameof(Pending));
            Publish(nameof(RetainedBytes));
        }
        ReleaseRetiredSourcesIfIdle();
    }

    private bool IsCurrent(LegacyMediaWork? work) =>
        !_disposed && work is not null && work.Generation == _generation &&
        !work.Token.IsCancellationRequested && !_importVm.IsBusy &&
        _state.IsImported &&
        ReferenceEquals(_state.Session, work.Request.Session) &&
        ReferenceEquals(_state.Snapshot, work.Request.Snapshot) &&
        ReferenceEquals(work.Request.Session.Current, work.Request.Snapshot) &&
        work.Request.Snapshot.Games.Any(g => ReferenceEquals(g, work.Request.Game));

    private void SetPhase(LegacyMediaPhase phase, string diagnostic)
    {
        if (_disposed) return;
        _phase = phase;
        _diagnostic = diagnostic;
        Publish(nameof(Phase));
        Publish(nameof(Diagnostic));
        _cancel.NotifyCanExecuteChanged();
    }

    private void Publish(string property)
    {
        if (!_disposed) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    }

    private void ReleaseRetiredSourcesIfIdle()
    {
        if (_outstanding != 0) return;
        foreach (var source in _retired) source.Dispose();
        _retired.Clear();
    }

    /// <summary>Cancel all generations, disable callbacks and dispose the loader after
    /// marking the View closed. Already-transferred bitmap lifetimes belong only to the View.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        CancelOutstanding();
        _disposed = true;
        _generationCancellation.Dispose();
        _loader.Dispose();
        PropertyChanged = null;
    }
}
