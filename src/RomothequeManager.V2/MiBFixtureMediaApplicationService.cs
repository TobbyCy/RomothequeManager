using RomothequeManager.V2.Inventory.Media.MiA;

namespace RomothequeManager.V2.Inventory.Media.MiB;

// NEXT-05 MI-B: internal fixture-only presentation; never a public media permission.
internal enum MiBPhase
{
    NoSelection, Ready, Running, CancellationRequested, Completed, Rejected, Faulted
}

internal sealed record MiBMediaRow(
    string ObservationRef, string DisplayLabel, string PlatformKey, MediaKind Kind,
    MediaImageFormat Format, bool HeaderValidated,
    long? SizeBytes, int? Width, int? Height,
    IReadOnlyList<string> SafeDiagnosticCodes);

internal sealed record MiBRunView(
    string RunRef, string MediaRootRef, DateTimeOffset CapturedAtUtc,
    InventoryScanStatus ScanStatus, InventoryAvailability Availability,
    InventoryFreshness SourceFreshness, IReadOnlyList<InventoryCoverage> Coverage,
    int ObservedMediaCount, int? KnownObservedMediaTotal,
    IReadOnlyList<MiBMediaRow> Rows, IReadOnlyList<string> SafeDiagnosticCodes,
    bool IsHistoricalFixture);

internal sealed record MiBProjectionState(
    long SelectionVersion, MiBPhase Phase, bool ProgressIsIndeterminate,
    MiBRunView? CurrentComplete, MiBRunView? LastAttempt,
    bool CurrentIsHistorical, string? SafeNoticeCode);

internal sealed record MiBSelectionResult(
    bool Accepted, long SelectionVersion, string? SafeDiagnosticCode);

/// <summary>Copies MI-A facts into bounded, path-free, immutable fixture UI data.
/// Does not access disk, decode images, claim absent files, or authorize a scan.</summary>
internal static class MiBMediaProjection
{
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "MI_A_CANCELLED", "MI_A_TIMEOUT", "MI_A_DIRECTORY_BUDGET",
        "MI_A_FILE_BUDGET", "MI_A_TOTAL_HEADER_BUDGET",
        "MI_A_HEADER_BUDGET", "MI_A_HEADER_NOT_READ",
        "MI_A_IMAGE_SIZE_LIMIT", "MI_A_SHORT_READ", "MI_A_DENIED",
        "MI_A_NATIVE_IO_FAILED", "MI_A_INVALID_PNG_HEADER",
        "MI_A_INVALID_JPEG_HEADER", "MI_A_DIMENSION_LIMIT",
        "MI_A_SIGNATURE_OR_EXTENSION_MISMATCH", "UNSUPPORTED_FORMAT"
    };

    internal static string SafeCode(string? code) =>
        code is not null && Codes.Contains(code) ? code : "MI_B_UNCLASSIFIED_DIAGNOSTIC";

    private static IReadOnlyList<string> SafeCodes(IEnumerable<string> codes) =>
        Array.AsReadOnly(codes.Select(SafeCode).Distinct(StringComparer.Ordinal).Take(16).ToArray());

    private static string SafeOpaque(string? value, string prefix)
    {
        if (value is null || value.Length > 96 || !value.StartsWith(prefix, StringComparison.Ordinal) ||
            value.Length == prefix.Length ||
            value.Skip(prefix.Length).Any(c =>
                !char.IsAsciiLetterOrDigit(c) && c is not ('-' or '_')))
            return prefix + "unknown";
        return value;
    }

    private static string SafeLabel(string? relative)
    {
        var parts = (relative ?? "").Split('/', '\\');
        var filename = parts[^1];
        if (string.IsNullOrWhiteSpace(filename)) return "media";
        var chars = filename.Take(64).Select(c =>
            char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.' ? c : '_').ToArray();
        var label = new string(chars).Trim('.');
        return label.Length == 0 ? "media" : label;
    }

    private static string SafePlatform(string? platform)
    {
        if (string.IsNullOrWhiteSpace(platform) || platform.Length > 32 ||
            !platform.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
            return "UNKNOWN";
        return platform;
    }

    internal static MiBRunView Project(MediaIndexSnapshot source, bool historicalFixture = false)
    {
        ArgumentNullException.ThrowIfNull(source);
        // The scanner's TotalFileCount refers to observed media, not all entries on disk.
        // A partial or unavailable scope is never proof of absence.
        var coverage = source.CoverageProof
            .Select(x => new InventoryCoverage(SafePlatform(x.PartitionKey), x.Status))
            .ToArray();
        bool exhaustive = source.ScanStatus == InventoryScanStatus.Complete &&
            source.Availability == InventoryAvailability.Online &&
            coverage.Length > 0 && coverage.All(x => x.Status == InventoryScanStatus.Complete);
        var rows = source.Observations.Select(o => new MiBMediaRow(
            SafeOpaque(o.MediaObservationId, "mia-observation-"),
            SafeLabel(o.RelativePath), SafePlatform(o.PlatformKey),
            o.Kind, o.DetectedFormat, o.HeaderValidated,
            o.SizeBytes, o.Width, o.Height,
            SafeCodes(o.DiagnosticRefs))).ToArray();
        var diagnosticCodes = SafeCodes(source.Diagnostics.Select(d => d.Code));
        return new MiBRunView(
            SafeOpaque(source.RunId, "mia-"),
            SafeOpaque(source.MediaRootRef, "mia-media-"), source.CapturedAtUtc,
            source.ScanStatus,
            historicalFixture ? InventoryAvailability.Offline : source.Availability,
            historicalFixture ? InventoryFreshness.Stale : source.Freshness,
            Array.AsReadOnly(coverage), rows.Length,
            exhaustive ? source.TotalFileCount : null,
            Array.AsReadOnly(rows), diagnosticCodes, historicalFixture);
    }

    /// <summary>Explicit fixture archive is data only: absolutely no scan, permission,
    /// file existence check, SQLite rebind, or physical device interaction.</summary>
    internal static MiBProjectionState HistoricalFixture(MediaIndexSnapshot archive)
    {
        var view = Project(archive, historicalFixture: true);
        if (archive.ScanStatus != InventoryScanStatus.Complete ||
            archive.Availability != InventoryAvailability.Online)
            return new(0, MiBPhase.NoSelection, false,
                null, null, false, "MI_B_ARCHIVE_NOT_COMPLETE");
        return new(0, MiBPhase.NoSelection, false,
            view, null, true, "MI_B_HISTORY_OFFLINE");
    }
}

internal interface IMiBFixtureMediaApplicationService : IAsyncDisposable
{
    MiBProjectionState State { get; }
    event EventHandler<MiBProjectionState>? StateChanged;
    MiBSelectionResult SelectFixtureRoot(string fixtureRoot);
    bool ConfirmMediaIndexConsent(long selectionVersion, bool explicitUserAction);
    Task<MiBProjectionState> StartConfirmedAsync(
        long selectionVersion, CancellationToken cancellationToken = default);
    bool RequestCancellation();
    bool ClearSelection();
}

/// <summary>Fixture-only adapter. Select is syntax-only; Confirm alone can open the
/// selected fixture root to attest its native identity; Start uses MI-A handles.
/// The interface exposes no path, token, handle, scanner hook or public grant.</summary>
internal sealed class MiBFixtureMediaApplicationService : IMiBFixtureMediaApplicationService
{
    private readonly object _gate = new();
    private readonly MiAFixtureMediaSession _session;
    private readonly Queue<MiBProjectionState> _notifications = new();
    private MiAReadGrant? _pendingGrant;
    private CancellationTokenSource? _activeCancellation;
    private TaskCompletionSource<bool>? _finished;
    private bool _publishing;
    private bool _running;
    private bool _selected;
    private bool _disposed;
    private long _version;
    private long _operationGeneration;
    private Guid _scopeNonce;
    private MiBProjectionState _state =
        new(0, MiBPhase.NoSelection, false, null, null, false, null);

    // Test-only injection, deliberately absent from the interface/DTO/ViewModel.
    internal Action<int>? AfterHeaderReadForTest { get; set; }

    internal MiBFixtureMediaApplicationService(MiABudget? budget = null) =>
        _session = new MiAFixtureMediaSession(budget);

    public event EventHandler<MiBProjectionState>? StateChanged;
    public MiBProjectionState State { get { lock (_gate) return _state; } }

    public MiBSelectionResult SelectFixtureRoot(string fixtureRoot)
    {
        string validated;
        try { validated = MiAAnchoredMediaReader.ValidateFixturePath(fixtureRoot); }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException
            or PlatformNotSupportedException or NotSupportedException)
        {
            lock (_gate) return new(false, _version, "MI_B_FIXTURE_SCOPE_REQUIRED");
        }

        MiBProjectionState selected;
        lock (_gate)
        {
            if (_disposed) return new(false, _version, "MI_B_DISPOSED");
            // A previous worker may still return: it cannot publish into the new scope.
            _activeCancellation?.Cancel();
            _pendingGrant?.Revoke();
            _session.SelectFixture(validated, Guid.NewGuid().ToString("D"));
            _pendingGrant = null;
            _selected = true;
            ++_version;
            ++_operationGeneration;
            _scopeNonce = Guid.NewGuid();
            selected = _state = new(_version, MiBPhase.Ready, false,
                null, null, false, _running ? "MI_B_PREVIOUS_SCAN_DRAINING" : null);
        }
        Publish(selected);
        return new(true, selected.SelectionVersion, null);
    }

    public bool ConfirmMediaIndexConsent(long selectionVersion, bool explicitUserAction)
    {
        MiBProjectionState? changed = null;
        bool success = false;
        lock (_gate)
        {
            if (_disposed || _running || !_selected ||
                !explicitUserAction || selectionVersion != _version)
                return false;
            _pendingGrant?.Revoke();
            _pendingGrant = null;
            try
            {
                // Native root identity only; does not enumerate or index media.
                _pendingGrant = _session.AuthorizeFixture(
                    true, DateTimeOffset.UtcNow.AddMinutes(2));
                changed = _state = _state with {
                    Phase = MiBPhase.Ready, SafeNoticeCode = null
                };
                success = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                or ArgumentException or PlatformNotSupportedException
                or System.ComponentModel.Win32Exception)
            {
                changed = _state = _state with {
                    Phase = MiBPhase.Rejected,
                    SafeNoticeCode = "MI_B_CONSENT_ROOT_UNAVAILABLE"
                };
            }
        }
        if (changed is not null) Publish(changed);
        return success;
    }

    public Task<MiBProjectionState> StartConfirmedAsync(
        long selectionVersion, CancellationToken cancellationToken = default)
    {
        MiBProjectionState? refused = null;
        MiBProjectionState? running = null;
        MiAReadGrant? grant = null;
        CancellationTokenSource? cts = null;
        TaskCompletionSource<bool>? completion = null;
        long generation = 0;
        Guid scopeNonce = default;
        Action<int>? testHook = null;
        lock (_gate)
        {
            if (_disposed) return Task.FromResult(_state);
            if (_running) return Task.FromResult(
                _state with { SafeNoticeCode = "MI_B_SCAN_ALREADY_RUNNING" });
            string? reason = null;
            if (!_selected) reason = "MI_B_SELECTION_REQUIRED";
            else if (selectionVersion != _version) reason = "MI_B_STALE_SELECTION";
            else if (_pendingGrant is null || !_pendingGrant.Valid(
                _pendingGrant.RootId, _pendingGrant.SelectionVersion, _pendingGrant.Generation))
                reason = "MI_B_CONSENT_REQUIRED";

            if (reason is not null)
            {
                _pendingGrant?.Revoke();
                _pendingGrant = null;
                refused = _state = _state with {
                    Phase = MiBPhase.Rejected, SafeNoticeCode = reason,
                    ProgressIsIndeterminate = false
                };
            }
            else
            {
                grant = _pendingGrant!;
                _pendingGrant = null; // One-shot consent, consumed by this run.
                cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                _activeCancellation = cts;
                _finished = completion;
                _running = true;
                generation = ++_operationGeneration;
                scopeNonce = _scopeNonce;
                testHook = AfterHeaderReadForTest;
                running = _state = _state with {
                    Phase = MiBPhase.Running, SafeNoticeCode = null,
                    ProgressIsIndeterminate = true
                };
            }
        }

        if (refused is not null)
        {
            Publish(refused);
            return Task.FromResult(refused);
        }
        Publish(running!);
        // Never pass the token to Task.Run: a pre-cancelled request must be
        // reflected by MI-A's own Partial/MI_A_CANCELLED snapshot.
        return ExecuteAsync(grant!, cts!, completion!,
            selectionVersion, generation, scopeNonce, testHook);
    }

    private async Task<MiBProjectionState> ExecuteAsync(
        MiAReadGrant grant, CancellationTokenSource cts,
        TaskCompletionSource<bool> completion, long version,
        long generation, Guid scopeNonce, Action<int>? testHook)
    {
        string? fault = null;
        MediaIndexSnapshot? captured = null;
        try
        {
            captured = await Task.Run(() => _session.Scan(grant, cts.Token,
                afterHeaderReadForTest: testHook)).ConfigureAwait(false);
        }
        catch (Exception)
        {
            fault = "MI_B_UNEXPECTED_FAILURE"; // No native exception/path in the DTO.
        }
        finally { grant.Revoke(); }

        MiBProjectionState answer;
        bool publish = false;
        lock (_gate)
        {
            _running = false;
            _activeCancellation = null;
            _finished = null;
            cts.Dispose();
            if (!_disposed && version == _version &&
                generation == _operationGeneration && scopeNonce == _scopeNonce)
            {
                // MI-A already quarantines old A/B generations; verify again here.
                var last = _session.LastAttempt;
                var current = _session.Current;
                if (captured is null || last?.RunId != captured.RunId ||
                    current is not null && current.MediaRootRef != captured.MediaRootRef)
                {
                    fault ??= "MI_B_GENERATION_NOT_PROVEN";
                    current = null;
                    last = null;
                }
                var currentView = current is null ? null : MiBMediaProjection.Project(current);
                var lastView = last is null ? null : MiBMediaProjection.Project(last);
                var historical = currentView is not null &&
                    lastView is not null && currentView.RunRef != lastView.RunRef;
                string? notice = fault;
                if (notice is null && last is not null)
                {
                    if (last.Diagnostics.Any(d => d.Code == "MI_A_CANCELLED"))
                        notice = "MI_B_CANCELLED";
                    else if (last.Diagnostics.Any(d => d.Code == "MI_A_DENIED"))
                        notice = "MI_B_DENIED";
                    else if (last.Availability == InventoryAvailability.Offline)
                        notice = "MI_B_OFFLINE";
                    else if (last.ScanStatus == InventoryScanStatus.Partial)
                        notice = "MI_B_PARTIAL";
                }
                answer = _state = _state with {
                    Phase = fault is null ? MiBPhase.Completed : MiBPhase.Faulted,
                    ProgressIsIndeterminate = false,
                    CurrentComplete = currentView, LastAttempt = lastView,
                    CurrentIsHistorical = historical, SafeNoticeCode = notice
                };
                publish = true;
            }
            else answer = _state; // Never publish a late completion from scope A into B.
            completion.TrySetResult(true);
        }
        if (publish) Publish(answer);
        return answer;
    }

    public bool RequestCancellation()
    {
        MiBProjectionState changed;
        lock (_gate)
        {
            if (_disposed || !_running || _activeCancellation is null ||
                _state.Phase != MiBPhase.Running) return false;
            changed = _state = _state with {
                Phase = MiBPhase.CancellationRequested,
                SafeNoticeCode = null
            };
            _activeCancellation.Cancel();
            _session.Revoke();
        }
        Publish(changed);
        return true;
    }

    public bool ClearSelection()
    {
        MiBProjectionState cleared;
        lock (_gate)
        {
            if (_disposed) return false;
            _activeCancellation?.Cancel();
            _session.Revoke();
            _pendingGrant?.Revoke();
            _pendingGrant = null;
            _selected = false;
            ++_version;
            ++_operationGeneration;
            _scopeNonce = Guid.NewGuid();
            cleared = _state = new(_version, MiBPhase.NoSelection, false,
                null, null, false, null);
        }
        Publish(cleared);
        return true;
    }

    public async ValueTask DisposeAsync()
    {
        Task wait;
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _activeCancellation?.Cancel();
            _session.Revoke();
            _pendingGrant?.Revoke();
            _pendingGrant = null;
            ++_operationGeneration;
            wait = _finished?.Task ?? Task.CompletedTask;
        }
        await wait.ConfigureAwait(false);
        lock (_gate)
        {
            _notifications.Clear();
            StateChanged = null;
            AfterHeaderReadForTest = null;
        }
    }

    private void Publish(MiBProjectionState candidate)
    {
        lock (_gate)
        {
            if (_disposed || !ReferenceEquals(candidate, _state)) return;
            _notifications.Enqueue(candidate);
            if (_publishing) return;
            _publishing = true;
        }

        while (true)
        {
            EventHandler<MiBProjectionState>? handlers;
            MiBProjectionState next;
            lock (_gate)
            {
                if (_disposed || _notifications.Count == 0)
                {
                    _notifications.Clear();
                    _publishing = false;
                    return;
                }
                next = _notifications.Dequeue();
                if (!ReferenceEquals(next, _state)) continue;
                handlers = StateChanged;
            }
            if (handlers is null) continue;
            foreach (EventHandler<MiBProjectionState> handler in handlers.GetInvocationList())
            {
                try
                {
                    lock (_gate)
                        if (_disposed || !ReferenceEquals(next, _state)) break;
                    handler(this, next);
                }
                catch (Exception) { /* An observer must never interrupt a scan/permission gate. */ }
            }
        }
    }
}
