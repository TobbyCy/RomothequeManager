using System.Runtime.CompilerServices;
using RomothequeManager.V2.Inventory;

[assembly: InternalsVisibleTo("RomothequeManager.Inventory.Core.Smoke")]

namespace RomothequeManager.V2.Inventory.Application;

// NEXT-03: application orchestration ONLY. InventorySession remains the sole filesystem scanner.
// No Avalonia, ViewModel, XML importer, persistent library identity or progress percentage.
public enum InventoryRunPhase
{
    NoSelection, Ready, Running, CancellationRequested, Completed, Rejected, Faulted
}

public sealed record InventoryApplicationState(
    long SelectionVersion,
    InventoryRunPhase Phase,
    string? SelectedLocalRoot,
    InventorySnapshot? Current,
    InventorySnapshot? LastAttempt,
    string? SafeDiagnosticCode,
    bool ProgressIsIndeterminate);

public sealed record InventorySelectionResult(
    bool Accepted, string? SafeDiagnosticCode, long SelectionVersion);

public sealed record InventoryRunRequest(long SelectionVersion, bool ExplicitUserConsent);

public interface IInventoryApplicationService : IAsyncDisposable
{
    InventoryApplicationState State { get; }

    // Fired on the calling or worker thread. Avalonia ViewModels must dispatch to the UI thread.
    event EventHandler<InventoryApplicationState>? StateChanged;

    // A restricted metadata-only preflight AFTER an explicit folder-picker selection.
    // Never enumerates ROMS or parses game XML; cancellation of a picker does not call this.
    InventorySelectionResult PrepareSelection(InventoryScope selectedScope);
    Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default);
    bool RequestCancellation();
    bool ClearSelection();
}

public sealed class InventoryApplicationService : IInventoryApplicationService
{
    private readonly object _gate = new();
    private readonly Func<InventoryScope, bool> _syntheticPermission;
    private InventorySession? _session;
    private InventoryScope? _scope;
    private CancellationTokenSource? _activeCancellation;
    private TaskCompletionSource<bool>? _finished;
    private bool _running;
    private bool _disposed;
    private long _version;
    private InventoryApplicationState _state =
        new(0, InventoryRunPhase.NoSelection, null, null, null, null, true);

    // Fail-closed in a distributed app: user consent alone is NOT authorization to scan real ROMs.
    // This constructor never executes Scan(), even when the user confirms.
    public InventoryApplicationService() : this(static _ => false) { }

    // Only the trusted synthetic fixture smoke runner can grant per-scope permission.
    // There is deliberately no public runtime switch, env var or manifest-based bypass.
    internal InventoryApplicationService(Func<InventoryScope, bool> syntheticPermission) =>
        _syntheticPermission = syntheticPermission ??
            throw new ArgumentNullException(nameof(syntheticPermission));

    public event EventHandler<InventoryApplicationState>? StateChanged;

    public InventoryApplicationState State
    {
        get { lock (_gate) return _state; }
    }

    public InventorySelectionResult PrepareSelection(InventoryScope selectedScope)
    {
        ArgumentNullException.ThrowIfNull(selectedScope);
        InventoryScope safeScope;
        InventorySession candidate;

        try
        {
            // InventorySession checks the root syntax and all scan budgets, without scanning.
            var root = InventorySession.ValidateRootSyntax(selectedScope.LibraryRoot);
            var attrs = File.GetAttributes(root);
            if ((attrs & FileAttributes.Directory) == 0 ||
                (attrs & FileAttributes.ReparsePoint) != 0)
                return RejectSelection("ROOT_UNAVAILABLE");

            var roms = Path.Combine(root, "ROMS");
            attrs = File.GetAttributes(roms);
            if ((attrs & FileAttributes.Directory) == 0 ||
                (attrs & FileAttributes.ReparsePoint) != 0)
                return RejectSelection("ROMS_UNAVAILABLE");

            // Freeze aliases against mutation by the picker/calling ViewModel.
            var aliases = selectedScope.PlatformAliases is null
                ? null
                : selectedScope.PlatformAliases.ToDictionary(
                    pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            safeScope = selectedScope with { LibraryRoot = root, PlatformAliases = aliases };
            candidate = new InventorySession(safeScope);
        }
        catch (Exception error) when (error is ArgumentException or IOException
            or UnauthorizedAccessException or System.Security.SecurityException
            or NotSupportedException)
        {
            return RejectSelection("INVALID_LOCAL_SCOPE");
        }

        InventoryApplicationState accepted;
        lock (_gate)
        {
            if (_disposed) return new(false, "SERVICE_DISPOSED", _version);
            if (_running) return new(false, "SCAN_ALREADY_RUNNING", _version);
            _session = candidate;
            _scope = safeScope;
            ++_version;
            accepted = _state = new(_version, InventoryRunPhase.Ready, safeScope.LibraryRoot,
                null, null, null, true);
        }

        Publish(accepted);
        return new(true, null, accepted.SelectionVersion);
    }

    private InventorySelectionResult RejectSelection(string diagnostic)
    {
        lock (_gate) return new(false, diagnostic, _version);
    }

    public Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default)
    {
        InventorySession session;
        CancellationTokenSource cts;
        TaskCompletionSource<bool> completion;
        InventoryApplicationState runningState;

        lock (_gate)
        {
            // A rejected second start must NOT change Running/CancelRequested into Rejected.
            if (_disposed) return Task.FromResult(Rejected("SERVICE_DISPOSED"));
            if (_running) return Task.FromResult(Rejected("SCAN_ALREADY_RUNNING"));
            if (_session is null || _scope is null)
                return Task.FromResult(Rejected("NO_EXPLICIT_SELECTION"));
            if (request.SelectionVersion != _version || !request.ExplicitUserConsent)
                return Task.FromResult(Rejected("CONSENT_OR_SELECTION_REQUIRED"));
            if (!_syntheticPermission(_scope))
                return Task.FromResult(Rejected("REAL_LIBRARY_SCAN_HOLD"));

            cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _activeCancellation = cts;
            _finished = completion;
            _running = true;
            session = _session;
            runningState = _state = _state with {
                Phase = InventoryRunPhase.Running,
                SafeDiagnosticCode = null,
                ProgressIsIndeterminate = true
            };
        }

        Publish(runningState);
        return ExecuteAsync(session, cts, completion, runningState.SelectionVersion);
    }

    private InventoryApplicationState Rejected(string code) =>
        _state with { Phase = InventoryRunPhase.Rejected, SafeDiagnosticCode = code };

    private async Task<InventoryApplicationState> ExecuteAsync(
        InventorySession session, CancellationTokenSource cts,
        TaskCompletionSource<bool> completion, long version)
    {
        string? faultCode = null;
        try
        {
            // Intentionally do not pass the token to Task.Run: even a pre-cancelled request
            // must enter the NEXT-02 reader and yield its controlled Partial/CANCELLED result.
            await Task.Run(() => session.Scan(cts.Token)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Never fabricate a Failed snapshot or publish path/exception details.
            faultCode = "UNEXPECTED_INVENTORY_FAILURE";
        }

        InventoryApplicationState finishedState;
        lock (_gate)
        {
            _running = false;
            _activeCancellation = null;
            _finished = null;
            finishedState = _state = _state with {
                SelectionVersion = version,
                Phase = faultCode is null ? InventoryRunPhase.Completed : InventoryRunPhase.Faulted,
                Current = session.Current,
                LastAttempt = session.LastAttempt,
                SafeDiagnosticCode = faultCode,
                ProgressIsIndeterminate = false
            };
            cts.Dispose();
        }

        if (!_disposed) Publish(finishedState);
        completion.TrySetResult(true);
        return finishedState;
    }

    public bool RequestCancellation()
    {
        InventoryApplicationState pending;
        lock (_gate)
        {
            if (!_running || _activeCancellation is null ||
                _state.Phase == InventoryRunPhase.CancellationRequested)
                return false;
            pending = _state = _state with {
                Phase = InventoryRunPhase.CancellationRequested,
                SafeDiagnosticCode = null
            };
            _activeCancellation.Cancel();
        }

        if (!_disposed) Publish(pending);
        return true;
    }

    public bool ClearSelection()
    {
        InventoryApplicationState cleared;
        lock (_gate)
        {
            if (_disposed || _running) return false;
            _session = null;
            _scope = null;
            ++_version;
            cleared = _state = new(_version, InventoryRunPhase.NoSelection,
                null, null, null, null, true);
        }
        Publish(cleared);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        Task wait = Task.CompletedTask;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            if (_running)
            {
                _activeCancellation?.Cancel();
                wait = _finished?.Task ?? Task.CompletedTask;
            }
        }
        await wait.ConfigureAwait(false);
        StateChanged = null;
    }

    private void Publish(InventoryApplicationState state)
    {
        var listeners = StateChanged;
        if (listeners is null) return;
        foreach (EventHandler<InventoryApplicationState> listener in listeners.GetInvocationList())
        {
            try { listener(this, state); }
            catch (Exception) { /* Observers cannot alter scanner state or fail a run. */ }
        }
    }
}
