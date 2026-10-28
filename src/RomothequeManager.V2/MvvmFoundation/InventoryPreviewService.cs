using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

namespace RomothequeManager.V2.MvvmFoundation;

// PRESENTATION SIMULATOR ONLY. It never instantiates InventorySession and never
// opens/stat/enumerates any root, file, gamelist, ROM, media, network or USB device.
// Replace this class with DEV 1's reviewed service before a real integration.
// Result data are invented previews, explicitly labelled NEXT03_UI_SIMULATION.
internal enum InventoryPreviewOutcome { Complete, Partial, Failed, Offline, Stale }

internal sealed class InventoryPreviewService : IInventoryApplicationService
{
    private readonly object _gate = new();
    private InventoryApplicationState _state = new(
        0, InventoryRunPhase.NoSelection, null, null, null, null, false);
    private CancellationTokenSource? _cancel;
    private Task<InventoryApplicationState>? _running;
    private bool _disposed;
    private InventoryPreviewOutcome _outcome;

    public event EventHandler<InventoryApplicationState>? StateChanged;
    public InventoryApplicationState State { get { lock (_gate) return _state; } }

    public bool SelectPreviewOutcome(InventoryPreviewOutcome outcome)
    {
        lock (_gate)
        {
            if (_disposed || Busy(_state.Phase)) return false;
            _outcome = outcome;
            return true;
        }
    }

    public InventorySelectionResult PrepareSelection(InventoryScope settings)
    {
        var root = settings.LibraryRoot;
        // Pure *syntax* validation. Even Directory.Exists would access a real
        // library, which this preview is prohibited from doing.
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathFullyQualified(root) ||
            root.Contains("://", StringComparison.Ordinal) ||
            root.StartsWith(@"\\", StringComparison.Ordinal) ||
            root.StartsWith("//", StringComparison.Ordinal))
            return new(false, "SIMULATION_LOCAL_PATH_REQUIRED", State.SelectionVersion);

        InventoryApplicationState next;
        lock (_gate)
        {
            if (_disposed || Busy(_state.Phase))
                return new(false, "SIMULATION_BUSY", _state.SelectionVersion);
            next = new(_state.SelectionVersion + 1, InventoryRunPhase.Ready, root,
                null, null, null, false);
            _state = next;
        }
        StateChanged?.Invoke(this, next);
        return new(true, null, next.SelectionVersion);
    }

    public Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default)
    {
        InventoryApplicationState running;
        Task<InventoryApplicationState> task;
        lock (_gate)
        {
            if (_disposed || Busy(_state.Phase) || _state.SelectedLocalRoot is null ||
                request.SelectionVersion != _state.SelectionVersion ||
                !request.ExplicitUserConsent || cancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("SIMULATION_RUN_REFUSED");

            _cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            running = _state with
            {
                Phase = InventoryRunPhase.Running,
                SafeDiagnosticCode = null, ProgressIsIndeterminate = true
            };
            _state = running;
            // Simulates only elapsed time and manufactured snapshots. NO I/O.
            task = SimulateAsync(_state.SelectionVersion, _outcome, _cancel.Token);
            _running = task;
        }
        StateChanged?.Invoke(this, running);
        return task;
    }

    public bool RequestCancellation()
    {
        InventoryApplicationState next;
        lock (_gate)
        {
            if (_disposed || _state.Phase != InventoryRunPhase.Running || _cancel is null)
                return false;
            next = _state with { Phase = InventoryRunPhase.CancellationRequested };
            _state = next;
            _cancel.Cancel();
        }
        StateChanged?.Invoke(this, next);
        return true;
    }

    public bool ClearSelection()
    {
        InventoryApplicationState next;
        lock (_gate)
        {
            if (_disposed || Busy(_state.Phase)) return false;
            next = new(_state.SelectionVersion + 1, InventoryRunPhase.NoSelection,
                null, null, null, null, false);
            _state = next;
        }
        StateChanged?.Invoke(this, next);
        return true;
    }

    private async Task<InventoryApplicationState> SimulateAsync(
        long generation, InventoryPreviewOutcome outcome, CancellationToken token)
    {
        bool cancelled = false;
        try { await Task.Delay(TimeSpan.FromSeconds(3), token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { cancelled = true; }

        InventoryApplicationState next;
        CancellationTokenSource? toDispose;
        lock (_gate)
        {
            // The preview never creates a physical scanner or concurrent job.
            // An unexpected obsolete continuation must not overwrite new context.
            if (_disposed || _state.SelectionVersion != generation)
                return _state;

            var snapshot = SimulatedSnapshot(generation, cancelled, outcome);
            next = _state with
            {
                Phase = InventoryRunPhase.Completed,
                Current = snapshot.ScanStatus == InventoryScanStatus.Complete
                    ? snapshot : _state.Current,
                LastAttempt = snapshot,
                SafeDiagnosticCode = null, ProgressIsIndeterminate = false
            };
            _state = next;
            toDispose = _cancel;
            _cancel = null;
            _running = null;
        }
        toDispose?.Dispose();
        StateChanged?.Invoke(this, next);
        return next;
    }

    private static bool Busy(InventoryRunPhase phase) =>
        phase is InventoryRunPhase.Running or InventoryRunPhase.CancellationRequested;

    private static InventorySnapshot SimulatedSnapshot(
        long generation, bool cancelled, InventoryPreviewOutcome outcome)
    {
        var now = DateTimeOffset.UtcNow;
        var phase = cancelled ? InventoryScanStatus.Partial : outcome switch
        {
            InventoryPreviewOutcome.Complete or InventoryPreviewOutcome.Stale =>
                InventoryScanStatus.Complete,
            InventoryPreviewOutcome.Partial => InventoryScanStatus.Partial,
            _ => InventoryScanStatus.Failed
        };
        var availability = !cancelled && outcome == InventoryPreviewOutcome.Offline
            ? InventoryAvailability.Offline : InventoryAvailability.Online;
        var freshness = !cancelled && outcome == InventoryPreviewOutcome.Stale
            ? InventoryFreshness.Stale : InventoryFreshness.Unknown;
        var count = phase == InventoryScanStatus.Failed ? 0 :
            phase == InventoryScanStatus.Partial ? 1 : 3;
        var simulatedNames = new[] { "ROMS/simulation/alpha.gba",
            "ROMS/simulation/discs.m3u", "ROMS/simulation/unknown.bin" };
        var files = Enumerable.Range(0, count).Select(i => new FileObservation(
            "sim-" + generation + "-" + i,
            "sim-location-" + generation + "-" + i, "UNKNOWN", simulatedNames[i],
            i == 1 ? InventoryKind.Playlist : i == 0
                ? InventoryKind.PotentialRom : InventoryKind.ArchiveCandidate,
            Path.GetExtension(simulatedNames[i]), 16L + i,
            now, now, "NEXT03_UI_SIMULATION", InventoryAvailability.Unknown,
            Array.Empty<string>())).ToArray();
        var diagnostic = phase == InventoryScanStatus.Partial
            ? cancelled ? "CANCELLED" : "LIMIT_REACHED"
            : phase == InventoryScanStatus.Failed ? "ROOT_UNAVAILABLE" : null;
        var diagnostics = diagnostic is null
            ? Array.Empty<InventoryDiagnostic>()
            : new[] { new InventoryDiagnostic(diagnostic, InventorySeverity.Warning,
                "ROMS", "Scénario simulé : aucune lecture de dossier", "NEXT03_UI_SIMULATION", now) };
        return new InventorySnapshot(
            "next03-ui-preview/1", "simulation-" + generation, Guid.NewGuid().ToString("N"),
            now, phase, availability, freshness,
            new[] { new InventoryCoverage("ROMS", phase) }, files, diagnostics,
            Array.Empty<InventoryPlaylist>(), Array.Empty<PotentialDuplicateGroup>(),
            "NOT_A_REAL_FINGERPRINT", files.Length,
            phase == InventoryScanStatus.Complete ? files.Length : null, null);
    }

    public async ValueTask DisposeAsync()
    {
        Task<InventoryApplicationState>? task;
        lock (_gate)
        {
            if (_disposed) return;
            _cancel?.Cancel();
            task = _running;
        }
        if (task is not null)
        {
            try { await task.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        lock (_gate) _disposed = true;
    }
}
