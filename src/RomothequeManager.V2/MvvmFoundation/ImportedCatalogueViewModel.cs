using System.ComponentModel;
using System.Windows.Input;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>
/// One explicit ES-DE selection confirmed by the native Avalonia dialogue.
/// This is not a filesystem scan grant or a MEDIA/PREVIEW permission.
/// </summary>
public sealed record ConfirmedEsDeImportRequest(string SelectedRoot, bool ExplicitUserConsent);

/// <summary>
/// Observable shell/import presentation model. ES-DE parsing stays in the injected
/// application service; the View retains only picker, confirmation and drawing.
/// </summary>
public sealed class ImportedCatalogueViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IEsDeImportApplicationService _service;
    private readonly InventoryCommand _import;
    private readonly InventoryCommand _refresh;
    private readonly InventoryCommand _cancel;
    private CancellationTokenSource? _operation;
    private bool _disposed;
    private bool _busy;
    private int _revision;
    private long _operationGeneration;
    private string _status = "Idle";

    /// <summary>Creates a model for the existing presentation state and injectable import service.</summary>
    public ImportedCatalogueViewModel(ImportedCatalogueUiState state,
        IEsDeImportApplicationService? service = null)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
        _service = service ?? state.ApplicationService;
        _import = new InventoryCommand(arg =>
        {
            if (arg is ConfirmedEsDeImportRequest request)
                _ = ImportConfirmedAsync(request.SelectedRoot, request.ExplicitUserConsent);
        }, arg => CanImport && arg is ConfirmedEsDeImportRequest
            { ExplicitUserConsent: true, SelectedRoot: { Length: > 0 } });
        _refresh = new InventoryCommand(_ => _ = RefreshAsync(), _ => CanRefresh);
        _cancel = new InventoryCommand(_ => Cancel(), _ => CanCancel);
    }

    /// <summary>
    /// Notifies the View after an accepted presentation transition. No notification
    /// is sent between committing the backend session and adopting the UI snapshot.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Existing UI-only selection, filtering and diagnostics state.</summary>
    public ImportedCatalogueUiState State { get; }
    /// <summary>Last accepted ES-DE snapshot; failed/cancelled jobs cannot replace it.</summary>
    public EsDeCatalogueSnapshot? Current => State.Snapshot;
    /// <summary>True only while one explicit import/refresh is running.</summary>
    public bool IsBusy => _busy;
    /// <summary>Last import attempt state (not an inventory Current/LastAttempt scan).</summary>
    public string ImportStatus => _status;
    /// <summary>Monotonic notification for collection, filter, selection and status changes.</summary>
    public int StateRevision => _revision;
    /// <summary>Can start a user-confirmed ES-DE import, never an automatic scan.</summary>
    public bool CanImport => !_disposed && !_busy;
    /// <summary>Can refresh an already accepted session, without re-selecting a root.</summary>
    public bool CanRefresh => CanImport && State.Session is not null;
    /// <summary>Can request cancellation of the active UI operation.</summary>
    public bool CanCancel => !_disposed && _busy && _operation is { IsCancellationRequested: false };
    /// <summary>Command requires a confirmed explicit local root as its parameter.</summary>
    public ICommand ImportCommand => _import;
    /// <summary>Refreshes the accepted session only while idle.</summary>
    public ICommand RefreshCommand => _refresh;
    /// <summary>Requests cancellation without adopting any late completion.</summary>
    public ICommand CancelCommand => _cancel;

    /// <summary>Start a read-only import only after the View's explicit picker and dialogue.</summary>
    public Task<bool> ImportConfirmedAsync(string selectedRoot, bool explicitlyConfirmed)
    {
        if (!explicitlyConfirmed || string.IsNullOrWhiteSpace(selectedRoot) || !CanImport)
            return Task.FromResult(false);
        return RunAsync(async (token, generation) =>
        {
            var prepared = await _service.PrepareAsync(selectedRoot, token);
            if (!IsCurrentOperation(token, generation)) return false;
            State.Adopt(prepared); // no await/notification between final guard and adoption
            return true;
        });
    }

    /// <summary>Refresh an existing accepted session, preserving filter/page/selection.</summary>
    public Task<bool> RefreshAsync()
    {
        if (!CanRefresh || State.Session is not { } session)
            return Task.FromResult(false);
        var accepted = State.Snapshot;
        return RunAsync(async (token, generation) =>
        {
            var prepared = await _service.RefreshAsync(session, token);
            // Both the accepted projection and TryOpenMedia session authority must
            // remain A until this synchronous commit is explicitly authorized.
            if (!IsCurrentOperation(token, generation) ||
                !ReferenceEquals(State.Session, session) ||
                !ReferenceEquals(State.Snapshot, accepted) ||
                !ReferenceEquals(session.Current, accepted))
                return false;
            // Prepare every UI projection before the guarded session commit;
            // the state method has no await, callbacks or notifications between
            // session.TryCommitPreparedRefresh and adopting the exact same snapshot.
            if (!State.TryApplyPreparedRefresh(session, prepared))
                throw new InvalidDataException("ESDE_REFRESH_STALE_PROPOSAL");
            return true;
        });
    }

    private bool IsCurrentOperation(CancellationToken token, long generation) =>
        !_disposed && !token.IsCancellationRequested &&
        _operation is not null && _operation.Token == token &&
        _operationGeneration == generation;

    private async Task<bool> RunAsync(Func<CancellationToken, long, Task<bool>> operation)
    {
        if (!CanImport) return false;
        var source = new CancellationTokenSource();
        var generation = ++_operationGeneration;
        _operation = source;
        _busy = true;
        SetStatus("Running");
        OnChanged(nameof(IsBusy));
        NotifyCommands();
        try
        {
            var accepted = await operation(source.Token, generation);
            if (!IsCurrentOperation(source.Token, generation))
            {
                if (!_disposed && (source.IsCancellationRequested || _operationGeneration != generation))
                    SetStatus("Cancelled");
                return false;
            }
            SetStatus(accepted ? "Complete" : "Cancelled");
            StateChanged();
            return accepted;
        }
        catch (OperationCanceledException) when (source.IsCancellationRequested)
        {
            if (!_disposed) SetStatus("Cancelled");
            return false;
        }
        catch (Exception error) when (ImportedCatalogueUiState.IsExpectedImportError(error))
        {
            if (!_disposed && !source.IsCancellationRequested)
            {
                State.ImportFailed(error); // redacted diagnostic; no private root leaked
                SetStatus("Failed");
                StateChanged();
            }
            return false;
        }
        finally
        {
            if (ReferenceEquals(_operation, source))
            {
                _operation = null;
                _busy = false;
                if (!_disposed)
                {
                    OnChanged(nameof(IsBusy));
                    NotifyCommands();
                }
            }
            source.Dispose();
        }
    }

    /// <summary>Reject a late import result, without interrupting the backend parser.</summary>
    public void Cancel()
    {
        if (!CanCancel) return;
        ++_operationGeneration;
        _operation!.Cancel();
        SetStatus("CancellationRequested");
        NotifyCommands();
    }

    /// <summary>Return to an empty shell without discarding the last accepted import.</summary>
    public void ShowWelcome()
    {
        if (_disposed) return;
        Cancel();
        State.ShowWelcome();
        StateChanged();
    }

    /// <summary>Enter the explicitly selected synthetic demonstration mode.</summary>
    public void ShowDemo()
    {
        if (_disposed) return;
        Cancel();
        State.ShowDemo();
        StateChanged();
    }

    /// <summary>Resume a previously accepted import without a new read operation.</summary>
    public bool ShowImported()
    {
        if (_disposed) return false;
        var shown = State.ShowImported();
        StateChanged();
        return shown;
    }

    /// <summary>Change only the selected platform; never infer it from a ROM filename.</summary>
    public bool SelectPlatform(string? platform)
    {
        if (_disposed) return false;
        var changed = State.SelectPlatform(platform);
        if (changed) StateChanged();
        return changed;
    }

    /// <summary>Apply a local text filter and reset pagination as before.</summary>
    public void Search(string? value)
    {
        if (_disposed) return;
        State.Search(value);
        StateChanged();
    }

    /// <summary>Advance one collection page, keeping the 50-card limit.</summary>
    public bool NextPage()
    {
        if (_disposed || !State.NextPage()) return false;
        StateChanged();
        return true;
    }

    /// <summary>Return one collection page.</summary>
    public bool PreviousPage()
    {
        if (_disposed || !State.PreviousPage()) return false;
        StateChanged();
        return true;
    }

    /// <summary>Open a game only if it belongs to Current.</summary>
    public bool Open(string? gameId)
    {
        if (_disposed || !State.Open(gameId)) return false;
        StateChanged();
        return true;
    }

    /// <summary>Navigate within the currently imported catalogue.</summary>
    public void Navigate(POCView view)
    {
        if (_disposed) return;
        State.Navigate(view);
        StateChanged();
    }

    private void SetStatus(string value)
    {
        if (_status == value) return;
        _status = value;
        OnChanged(nameof(ImportStatus));
    }

    private void StateChanged()
    {
        _revision++;
        OnChanged(nameof(Current));
        OnChanged(nameof(StateRevision));
        NotifyCommands();
    }

    private void NotifyCommands()
    {
        OnChanged(nameof(CanImport));
        OnChanged(nameof(CanRefresh));
        OnChanged(nameof(CanCancel));
        _import.NotifyCanExecuteChanged();
        _refresh.NotifyCanExecuteChanged();
        _cancel.NotifyCanExecuteChanged();
    }

    private void OnChanged(string name)
    {
        if (!_disposed)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    /// <summary>Cancel the active operation and suppress every subsequent UI notification.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ++_operationGeneration;
        _operation?.Cancel(); // the operation's own finally disposes the CTS
        PropertyChanged = null;
    }
}
