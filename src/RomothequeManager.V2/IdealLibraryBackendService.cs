using System.Security.Cryptography;
using System.Text;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.RsA;
using RomothequeManager.V2.Persistence;

namespace RomothequeManager.V2.Inventory.Application;

/// <summary>Independent file-access operations. ROMS approval never permits a
/// MEDIAS enumeration, an ES-DE XML read or image preview.</summary>
public enum IdealReadOperation
{
    /// <summary>Metadata-only ROMS inventory.</summary>
    RomsInventory,
    /// <summary>Separate MEDIAS index operation (denied until its own grant backend).</summary>
    MediasIndex,
    /// <summary>Separate image preview operation (denied until its own grant backend).</summary>
    MediaPreview
}

/// <summary>Production backend for an explicitly picker-selected IDEAL library.
/// The selection and read consent are separate commands; no CLI, CI flag, XML or
/// persisted state can confer a grant. All scan reads use anchored Windows handles.</summary>
public sealed class IdealLibraryBackendService : IInventoryApplicationService
{
    private readonly object _gate = new();
    private readonly InventoryApplicationService _preflight = new();
    private readonly SqliteCatalogStore _store;
    private readonly bool _syntheticFixtureOnly;
    private readonly Action<string>? _beforeChildOpenForTest;
    private RsAFixtureRomSession? _reader;
    private RsAReadGrant? _pendingGrant;
    private RsAReadGrant? _activeGrant;
    private CancellationTokenSource? _cancel;
    private Task<InventoryApplicationState>? _task;
    private bool _running;
    private bool _disposed;
    private long _version;
    private string? _rootId;
    private InventoryApplicationState _state =
        new(0, InventoryRunPhase.NoSelection, null, null, null, null, true);

    /// <summary>Construct standard backend with fixed, application-local SQLite.
    /// Construction does not enumerate any library or issue any access grant.</summary>
    public IdealLibraryBackendService()
        : this(SqliteCatalogStore.OpenApplicationLocal(), syntheticFixtureOnly: false) { }

    // Friend test-assembly seam: SQLite and RS-A are restricted to disposable fixtures.
    // No public constructor, environment flag or CLI path enables this scope.
    internal IdealLibraryBackendService(SqliteCatalogStore store, bool syntheticFixtureOnly,
        Action<string>? beforeChildOpenForTest = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _syntheticFixtureOnly = syntheticFixtureOnly;
        _beforeChildOpenForTest = beforeChildOpenForTest;
    }

    /// <summary>Snapshots are immutable. UI consumers must marshal worker notifications.</summary>
    public event EventHandler<InventoryApplicationState>? StateChanged;

    /// <summary>Latest lifecycle projection; historical restored snapshots are OFFLINE/STALE.</summary>
    public InventoryApplicationState State
    {
        get { lock (_gate) return _state; }
    }

    /// <summary>Read saved catalogues from app SQLite alone. Does not touch library
    /// volumes, look up current paths or restore permissions.</summary>
    public IReadOnlyList<CatalogLastKnown> LoadSavedLibraries()
    {
        lock (_gate)
        {
            ThrowIfDisposed();
            return _store.LoadSavedRoms();
        }
    }

    /// <summary>Open historical data without picker selection or scan.
    /// Revalidation requires a new user selection and fresh grant.</summary>
    public bool RestoreSaved(string storageRootId)
    {
        InventoryApplicationState state;
        lock (_gate)
        {
            if (_disposed || _running) return false;
            if (!Guid.TryParse(storageRootId, out _)) return false;
            RevokeLocked();
            CatalogLastKnown saved;
            try { saved = _store.LoadLastKnown(storageRootId); }
            catch (Exception error) when (error is ArgumentException or InvalidDataException
                or InvalidOperationException or KeyNotFoundException)
            {
                return false;
            }
            if (saved.StorageRoot.Role != "ROMS") return false;
            _reader = null;
            _rootId = null;
            _preflight.ClearSelection();
            _version++;
            state = _state = new(_version, InventoryRunPhase.NoSelection,
                null, Offline(saved.CurrentComplete), Offline(saved.LastAttempt),
                null, false);
        }
        Publish(state);
        return true;
    }

    /// <summary>Preflight the explicit picker selection, never a scan. Changing
    /// scope revokes all previous permissions even if the new choice is rejected.</summary>
    public InventorySelectionResult PrepareSelection(InventoryScope selectedScope)
    {
        ArgumentNullException.ThrowIfNull(selectedScope);
        InventoryApplicationState state;
        lock (_gate)
        {
            if (_disposed) return new(false, "SERVICE_DISPOSED", _version);
            RevokeLocked();
            if (_running)
            {
                _cancel?.Cancel();
                return new(false, "SCAN_ALREADY_RUNNING", _version);
            }
            string root;
            RsAFixtureRomSession candidate;
            string id;
            try
            {
                root = InventorySession.ValidateRootSyntax(selectedScope.LibraryRoot);
                if (!_syntheticFixtureOnly)
                {
                    var appData = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)));
                    if (Overlaps(root, appData))
                        return new(false, "APP_STORAGE_SCOPE_REFUSED", _version);
                }
                id = StableUuid("ROMS", root);
                candidate = new RsAFixtureRomSession(new RsABudget(
                    Math.Min(selectedScope.MaxDepth, 16),
                    Math.Min(selectedScope.MaxFiles, 30000),
                    Math.Min(selectedScope.MaxDirectories, 100000),
                    selectedScope.TimeLimit, 0));
                var roms = Path.Combine(root, "ROMS");
                if (_syntheticFixtureOnly)
                    candidate.SelectFixture(roms, id);
                else
                    candidate.SelectAuthorizedLocalRoot(roms, id);
                var checkedScope = selectedScope with { LibraryRoot = root };
                var prepared = _preflight.PrepareSelection(checkedScope);
                if (!prepared.Accepted)
                    return new(false, prepared.SafeDiagnosticCode, _version);
                var libId = StableUuid("LIBRARY", root);
                _store.EnsureLibraryRoot(new CatalogLibrary(libId, "Bibliothèque IDEAL"),
                    new CatalogStorageRoot(id, libId, "ROMS", "ROMS"));
            }
            catch (Exception error) when (error is ArgumentException or IOException
                or UnauthorizedAccessException or System.Security.SecurityException
                or InvalidOperationException or PlatformNotSupportedException)
            {
                return new(false, "IDEAL_SELECTION_REFUSED", _version);
            }
            _reader = candidate;
            _rootId = id;
            _version++;
            CatalogLastKnown saved = _store.LoadLastKnown(id);
            state = _state = new(_version, InventoryRunPhase.Ready, root,
                Offline(saved.CurrentComplete), Offline(saved.LastAttempt), null, true);
        }
        Publish(state);
        return new(true, null, state.SelectionVersion);
    }

    /// <summary>Separate affirmative action after selection: issue a short-lived
    /// one-use ROMS-only capability bound to native root identity and generation.
    /// MEDIAS and PREVIEW must be authorized separately and cannot reuse this grant.</summary>
    public bool AuthorizeRomsRead(long selectionVersion, bool explicitUserAction) =>
        AuthorizeRead(IdealReadOperation.RomsInventory, selectionVersion, explicitUserAction);

    /// <summary>Only ROMS metadata enumeration is implemented by this boundary.
    /// No implicit MEDIAS/preview authority is inferred from a ROMS grant.</summary>
    public bool AuthorizeRead(IdealReadOperation operation, long selectionVersion,
        bool explicitUserAction)
    {
        lock (_gate)
        {
            if (_disposed || _running || _reader is null || _rootId is null ||
                _version != selectionVersion || !explicitUserAction ||
                operation != IdealReadOperation.RomsInventory)
                return false;
            _pendingGrant?.Revoke();
            _pendingGrant = null;
            try
            {
                _pendingGrant = _syntheticFixtureOnly
                    ? _reader.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(2))
                    : _reader.AuthorizeSelected(true, DateTimeOffset.UtcNow.AddMinutes(2));
                return true;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException
                or ArgumentException or PlatformNotSupportedException
                or System.ComponentModel.Win32Exception)
            {
                return false;
            }
        }
    }

    /// <summary>Start only with a separate, fresh, unconsumed matching ROMS grant.
    /// The scanner is the existing RS-A anchored metadata reader; not a path traversal
    /// of the legacy fixture reader. Current changes only on a complete scan.</summary>
    public Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default)
    {
        InventoryApplicationState? toPublish = null;
        Task<InventoryApplicationState> result;
        lock (_gate)
        {
            string? refusal = null;
            if (_disposed) refusal = "SERVICE_DISPOSED";
            else if (_running) refusal = "SCAN_ALREADY_RUNNING";
            else if (_reader is null || _rootId is null) refusal = "NO_EXPLICIT_SELECTION";
            else if (request.SelectionVersion != _version || !request.ExplicitUserConsent)
                refusal = "CONSENT_OR_SELECTION_REQUIRED";
            else if (_pendingGrant is null || !_pendingGrant.Valid(_rootId, _reader.SelectionVersion))
                refusal = "REAL_LIBRARY_SCAN_HOLD";

            if (refusal is not null)
            {
                if (!_running && !_disposed)
                    toPublish = _state = _state with {
                        Phase = InventoryRunPhase.Rejected, SafeDiagnosticCode = refusal
                    };
                result = Task.FromResult(_state);
            }
            else
            {
                var reader = _reader!;
                var grant = _pendingGrant!;
                _pendingGrant = null; // consume before any worker starts
                _activeGrant = grant;
                var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                _cancel = cts;
                _running = true;
                toPublish = _state = _state with {
                    Phase = InventoryRunPhase.Running, SafeDiagnosticCode = null,
                    ProgressIsIndeterminate = true
                };
                result = _task = ExecuteAsync(reader, grant, cts, _version, _rootId!);
            }
        }
        if (toPublish is not null) Publish(toPublish);
        return result;
    }

    private async Task<InventoryApplicationState> ExecuteAsync(
        RsAFixtureRomSession reader, RsAReadGrant grant, CancellationTokenSource cts,
        long version, string rootId)
    {
        InventorySnapshot? attempt = null;
        string? fault = null;
        try
        {
            // Task.Run never receives the token: a pre-cancelled request must
            // settle as a controlled failed/partial attempt, not become an orphan.
            attempt = await Task.Run(() => reader.Scan(grant, cts.Token, _beforeChildOpenForTest)).ConfigureAwait(false);
        }
        catch (Exception) { fault = "IDEAL_SCAN_FAILED"; }

        InventoryApplicationState result;
        lock (_gate)
        {
            // Capture cancellation BEFORE disposing the CTS. The worker may have
            // returned Complete immediately before the user clicked Cancel.
            var wasCancelled = cts.IsCancellationRequested ||
                _state.Phase == InventoryRunPhase.CancellationRequested;
            grant.Revoke();
            _activeGrant = null;
            _cancel = null;
            _running = false;
            cts.Dispose();
            // Do not save or adopt late results after Dispose, selection change or Cancel.
            // Cancelled partial/failed attempts remain LastAttempt when still selected.
            if (_disposed || version != _version || rootId != _rootId)
                return _state;
            if (attempt is not null && wasCancelled &&
                attempt.ScanStatus == InventoryScanStatus.Complete)
            {
                // A cancellation raced with the worker's final Complete result.
                // Never promote that late snapshot to Current.
                attempt = attempt with {
                    ScanStatus = InventoryScanStatus.Partial,
                    Freshness = InventoryFreshness.Stale,
                    TotalFileCount = null,
                    CoverageProof = Array.AsReadOnly(attempt.CoverageProof.Select(
                        proof => proof with { Status = InventoryScanStatus.Partial }).ToArray())
                };
            }
            if (attempt is not null)
            {
                try { _store.SaveAttempt(rootId, attempt); }
                catch (Exception) { fault = "IDEAL_SQLITE_WRITE_FAILED"; }
            }
            if (fault is not null)
            {
                result = _state = _state with {
                    Phase = InventoryRunPhase.Faulted, SafeDiagnosticCode = fault,
                    ProgressIsIndeterminate = false
                };
            }
            else
            {
                var current = attempt!.ScanStatus == InventoryScanStatus.Complete
                    ? attempt : _state.Current;
                result = _state = _state with {
                    Phase = InventoryRunPhase.Completed,
                    Current = current, LastAttempt = attempt,
                    SafeDiagnosticCode = null, ProgressIsIndeterminate = false
                };
            }
        }
        Publish(result);
        return result;
    }

    /// <summary>Immediately revoke the active capability and request cooperative
    /// cancellation. A late result cannot become a new complete Current.</summary>
    public bool RequestCancellation()
    {
        InventoryApplicationState state;
        lock (_gate)
        {
            if (_disposed || !_running || _cancel is null ||
                _state.Phase == InventoryRunPhase.CancellationRequested) return false;
            _activeGrant?.Revoke();
            _cancel.Cancel();
            state = _state = _state with { Phase = InventoryRunPhase.CancellationRequested };
        }
        Publish(state);
        return true;
    }

    /// <summary>Clear scope and all capabilities, retaining SQLite history only.</summary>
    public bool ClearSelection()
    {
        InventoryApplicationState state;
        lock (_gate)
        {
            if (_disposed) return false;
            RevokeLocked();
            if (_running)
            {
                _cancel?.Cancel();
                return false;
            }
            _reader = null;
            _rootId = null;
            _preflight.ClearSelection();
            _version++;
            state = _state = new(_version, InventoryRunPhase.NoSelection,
                null, null, null, null, true);
        }
        Publish(state);
        return true;
    }

    /// <summary>Revoke and settle active read before closing SQLite; no
    /// notification or grant can outlive this application service.</summary>
    public async ValueTask DisposeAsync()
    {
        Task<InventoryApplicationState>? wait;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            RevokeLocked();
            _cancel?.Cancel();
            wait = _task;
        }
        if (wait is not null)
        {
            try { await wait.ConfigureAwait(false); }
            catch (Exception) { /* disposal remains fail-closed */ }
        }
        await _preflight.DisposeAsync().ConfigureAwait(false);
        _store.Dispose();
        StateChanged = null;
    }

    private void RevokeLocked()
    {
        _pendingGrant?.Revoke();
        _activeGrant?.Revoke();
        _pendingGrant = null;
        _activeGrant = null;
        _reader?.Revoke();
    }

    private static InventorySnapshot? Offline(CatalogSavedSnapshot? saved)
    {
        var snapshot = saved?.Inventory;
        return snapshot is null ? null : snapshot with {
            Availability = InventoryAvailability.Offline,
            Freshness = InventoryFreshness.Stale,
            Observations = Array.AsReadOnly(snapshot.Observations.Select(
                observation => observation with { Availability = InventoryAvailability.Offline })
                .ToArray())
        };
    }

    private static bool Overlaps(string a, string b)
    {
        a = Path.TrimEndingDirectorySeparator(Path.GetFullPath(a));
        b = Path.TrimEndingDirectorySeparator(Path.GetFullPath(b));
        return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
            a.StartsWith(b + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            b.StartsWith(a + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    // No absolute path or persisted read capability in the identity tables.
    private static string StableUuid(string role, string root)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
            "romotheque-next05-identity/1|" + role + "|" +
            Path.TrimEndingDirectorySeparator(root).ToUpperInvariant()));
        return new Guid(bytes.AsSpan(0, 16)).ToString("D");
    }

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(IdealLibraryBackendService));
    }

    private void Publish(InventoryApplicationState state)
    {
        EventHandler<InventoryApplicationState>? listeners;
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(_state, state)) return;
            listeners = StateChanged;
        }
        if (listeners is null) return;
        foreach (EventHandler<InventoryApplicationState> subscriber in listeners.GetInvocationList())
        {
            try { subscriber(this, state); }
            catch (Exception) { /* observers never break backend IO */ }
        }
    }
}
