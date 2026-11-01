using RomothequeManager.V2.Inventory;

namespace RomothequeManager.V2.Inventory.Application;

// NEXT-03: application orchestration in existing V2 assembly only. InventorySession remains the sole filesystem scanner.
// No Avalonia, ViewModel, XML importer, persistent library identity or progress percentage.
/// <summary>Observable lifecycle of one explicit inventory selection and scan, independent of GUI rendering.</summary>
public enum InventoryRunPhase
{
    /// <summary>No local scope has been explicitly chosen; scanning cannot be started.</summary>
    NoSelection,
    /// <summary>A syntactically valid local scope was selected, but no scan permission has yet been granted.</summary>
    Ready,
    /// <summary>The single active bounded observation attempt is executing.</summary>
    Running,
    /// <summary>Cancellation has been requested and the active attempt must settle without discarding previous Current.</summary>
    CancellationRequested,
    /// <summary>An attempt finished; inspect its ScanStatus before treating coverage as complete.</summary>
    Completed,
    /// <summary>An attempted transition was denied without invoking the filesystem scanner.</summary>
    Rejected,
    /// <summary>An unexpected failure occurred; the safe diagnostic code replaces raw exception details.</summary>
    Faulted,
}

/// <summary>Immutable, thread-neutral projection of selection, progress, Current and LastAttempt; no exposure of a real scan permission.</summary>
public sealed record InventoryApplicationState(
    long SelectionVersion,
    InventoryRunPhase Phase,
    string? SelectedLocalRoot,
    InventorySnapshot? Current,
    InventorySnapshot? LastAttempt,
    string? SafeDiagnosticCode,
    bool ProgressIsIndeterminate)
{
    // Source-backed dimensions. Never infer coverage, availability or freshness from another field.
    /// <summary>Status from the most recent attempt; null until an attempt exists and not inferred from Current.</summary>
    public InventoryScanStatus? LastScanStatus => LastAttempt?.ScanStatus;
    /// <summary>Reachability reported by LastAttempt, or Unknown when no attempt has observed the root.</summary>
    public InventoryAvailability LastAvailability =>
        LastAttempt?.Availability ?? InventoryAvailability.Unknown;
    /// <summary>Freshness reported by LastAttempt; a missing snapshot is explicitly Unknown.</summary>
    public InventoryFreshness LastFreshness =>
        LastAttempt?.Freshness ?? InventoryFreshness.Unknown;
    /// <summary>Metadata-only observations from LastAttempt; empty does not prove that the selected scope contains no files.</summary>
    public IReadOnlyList<FileObservation> Observations =>
        LastAttempt?.Observations ?? Array.Empty<FileObservation>();
    /// <summary>Safe per-attempt diagnostics without exposing exception paths or treating refusal as complete enumeration.</summary>
    public IReadOnlyList<InventoryDiagnostic> Diagnostics =>
        LastAttempt?.Diagnostics ?? Array.Empty<InventoryDiagnostic>();
    /// <summary>Partition coverage evidence for the last attempt, never synthesized from an observation count.</summary>
    public IReadOnlyList<InventoryCoverage> CoverageProof =>
        LastAttempt?.CoverageProof ?? Array.Empty<InventoryCoverage>();
    /// <summary>Total files only when the underlying attempt established that count; null means not known.</summary>
    public int? TotalFileCount => LastAttempt?.TotalFileCount;
    /// <summary>Known confirmed-game total from the attempted snapshot; null is not interpreted as zero games.</summary>
    public int? ConfirmedGameCount => LastAttempt?.ConfirmedGameCount;
}

/// <summary>Result of a folder selection preflight, including a safe denial code and version for future consent checks.</summary>
public sealed record InventorySelectionResult(
    bool Accepted, string? SafeDiagnosticCode, long SelectionVersion);

/// <summary>Binds one explicit consent to the currently selected scope version; consent alone is never a real-library permission.</summary>
public sealed record InventoryRunRequest(long SelectionVersion, bool ExplicitUserConsent);

/// <summary>Application boundary for selecting and observing a synthetic library without exposing scanner access to a ViewModel.</summary>
public interface IInventoryApplicationService : IAsyncDisposable
{
    /// <summary>Current immutable application state; UI consumers must observe transitions rather than infer filesystem success.</summary>
    InventoryApplicationState State { get; }

    // Fired on the calling or worker thread. Avalonia ViewModels must dispatch to the UI thread.
    /// <summary>Event emitted on the invoking or worker thread; UI observers must marshal notifications to the UI dispatcher.</summary>
    event EventHandler<InventoryApplicationState>? StateChanged;

    // A restricted metadata-only preflight AFTER an explicit folder-picker selection.
    // Never enumerates ROMS or parses game XML; cancellation of a picker does not call this.
    /// <summary>Metadata-only scope preflight after an explicit picker selection; does not enumerate ROMS or grant scan consent.</summary>
    InventorySelectionResult PrepareSelection(InventoryScope selectedScope);
    /// <summary>Attempt one scan only when selected version, one-shot consent and synthetic scope permission all match.</summary>
    Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default);
    /// <summary>Request cancellation of the active attempt without replacing the previous complete Current snapshot.</summary>
    bool RequestCancellation();
    /// <summary>Clear an idle selected scope and invalidate the prior selection version; active operations remain protected.</summary>
    bool ClearSelection();
}

/// <summary>Single-flight inventory coordinator with versioned consent and a fail-closed public constructor.</summary>
public sealed class InventoryApplicationService : IInventoryApplicationService
{
    private readonly object _gate = new();
    private readonly Queue<InventoryApplicationState> _pendingNotifications = new();
    private bool _publishingNotifications;
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
    /// <summary>Construct the distributed service with all real-library scanning disabled, regardless of user consent.</summary>
    public InventoryApplicationService() : this(static _ => false) { }

    // Only the friend synthetic test assembly can grant per-scope permission.
    // There is deliberately no public runtime switch, env var or manifest-based bypass.
    /// <summary>Test-only injection seam for explicitly allowed fixture scopes; no public runtime bypass exists.</summary>
    internal InventoryApplicationService(Func<InventoryScope, bool> syntheticPermission) =>
        _syntheticPermission = syntheticPermission ??
            throw new ArgumentNullException(nameof(syntheticPermission));

    public event EventHandler<InventoryApplicationState>? StateChanged;

    /// <summary>Thread-safe immutable snapshot of the current application state including any last attempt.</summary>
    public InventoryApplicationState State
    {
        get { lock (_gate) return _state; }
    }

    /// <summary>Validate local root and ROMS metadata, reject reparse ancestors, freeze aliases and issue a new selection version.</summary>
    public InventorySelectionResult PrepareSelection(InventoryScope selectedScope)
    {
        ArgumentNullException.ThrowIfNull(selectedScope);
        InventoryScope safeScope;
        InventorySession candidate;
        lock (_gate)
        {
            if (_disposed) return new(false, "SERVICE_DISPOSED", _version);
            if (_running) return new(false, "SCAN_ALREADY_RUNNING", _version);
        }

        try
        {
            // InventorySession checks the root syntax and all scan budgets, without scanning.
            var root = InventorySession.ValidateRootSyntax(selectedScope.LibraryRoot);
            var attrs = File.GetAttributes(root);
            if ((attrs & FileAttributes.Directory) == 0 ||
                HasLinkedAncestor(root))
                return RejectSelection("ROOT_UNAVAILABLE");

            var roms = Path.Combine(root, "ROMS");
            attrs = File.GetAttributes(roms);
            if ((attrs & FileAttributes.Directory) == 0 ||
                HasLinkedAncestor(roms))
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

    private static bool HasLinkedAncestor(string selectedPath)
    {
        // Metadata-only ancestor check; never enumerate ROMS content.
        for (DirectoryInfo? directory = new(selectedPath); directory is not null;
             directory = directory.Parent)
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private InventorySelectionResult RejectSelection(string diagnostic)
    {
        lock (_gate) return new(false, diagnostic, _version);
    }

    /// <summary>Reject absent or stale consent and real-library access; only fixture-authorized requests can execute a single bounded scan.</summary>
    public Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default)
    {
        InventorySession? session = null;
        CancellationTokenSource? cts = null;
        TaskCompletionSource<bool>? completion = null;
        InventoryApplicationState? runningState = null;
        InventoryApplicationState? rejectedState = null;
        bool notifyRejection = false;

        lock (_gate)
        {
            string? refusal = null;
            if (_disposed) refusal = "SERVICE_DISPOSED";
            else if (_running) refusal = "SCAN_ALREADY_RUNNING";
            else if (_session is null || _scope is null) refusal = "NO_EXPLICIT_SELECTION";
            else if (request.SelectionVersion != _version || !request.ExplicitUserConsent)
                refusal = "CONSENT_OR_SELECTION_REQUIRED";
            else
            {
                bool permitted;
                try { permitted = _syntheticPermission(_scope); }
                catch (Exception) { permitted = false; }
                if (!permitted) refusal = "REAL_LIBRARY_SCAN_HOLD";
            }

            if (refusal is not null)
            {
                rejectedState = Rejected(refusal);
                // A refused attempt must be observable, but must never downgrade
                // Running/CancellationRequested or mutate a disposed service.
                if (!_running && !_disposed)
                {
                    _state = rejectedState;
                    notifyRejection = true;
                }
            }
            else
            {
                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _activeCancellation = cts;
                _finished = completion;
                _running = true;
                session = _session!;
                runningState = _state = _state with {
                    Phase = InventoryRunPhase.Running,
                    SafeDiagnosticCode = null,
                    ProgressIsIndeterminate = true
                };
            }
        }

        if (rejectedState is not null)
        {
            if (notifyRejection) Publish(rejectedState);
            return Task.FromResult(rejectedState);
        }

        Publish(runningState!);
        return ExecuteAsync(session!, cts!, completion!, runningState!.SelectionVersion);
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

    /// <summary>Signal cancellation to the active operation while retaining its prior complete snapshot for later inspection.</summary>
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

    /// <summary>Drop the selected scope only when allowed by the current lifecycle, invalidating any earlier consent token.</summary>
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

    /// <summary>Cancel an active attempt and await safe shutdown so no late event can resurrect disposed state.</summary>
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
        lock (_gate)
        {
            // Keep callbacks ordered even if an observer asks for cancellation
            // from inside a Running callback. Do not emit superseded states.
            if (_disposed || !ReferenceEquals(_state, state)) return;
            _pendingNotifications.Enqueue(state);
            if (_publishingNotifications) return;
            _publishingNotifications = true;
        }

        while (true)
        {
            EventHandler<InventoryApplicationState>? listeners;
            InventoryApplicationState next;
            lock (_gate)
            {
                if (_disposed)
                {
                    _pendingNotifications.Clear();
                    _publishingNotifications = false;
                    return;
                }
                if (_pendingNotifications.Count == 0)
                {
                    _publishingNotifications = false;
                    return;
                }
                next = _pendingNotifications.Dequeue();
                if (!ReferenceEquals(_state, next)) continue;
                listeners = StateChanged;
            }

            if (listeners is null) continue;
            foreach (EventHandler<InventoryApplicationState> listener in listeners.GetInvocationList())
            {
                try { listener(this, next); }
                catch (Exception) { /* An observer cannot break or reenter the scan engine. */ }
            }
        }
    }
}
