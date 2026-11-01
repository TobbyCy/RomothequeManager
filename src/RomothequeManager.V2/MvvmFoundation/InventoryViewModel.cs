using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

namespace RomothequeManager.V2.MvvmFoundation;

// NEXT-03 foundation: never constructs InventorySession, reads a folder, or uses Avalonia.
// DEV 1's application service is sole owner of scans, cancellation and Current/LastAttempt.
// The View injects a UI-thread dispatcher and owns the native folder picker.
/// <summary>Read-only inventory presentation adapter. The injected application service alone owns scans, read grants and immutable results.</summary>
public sealed class InventoryViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IInventoryApplicationService _service;
    private readonly Action<Action> _postToUi;
    private readonly InventoryCommand _start;
    private readonly InventoryCommand _cancel;
    private readonly InventoryCommand _selectObservation;
    private readonly InventoryCommand _showCurrent;
    private readonly InventoryCommand _showAttempt;
    private InventoryApplicationState _state;
    private long _latestVersion;
    private bool _disposed;
    private bool _consentConfirmed;
    private bool _preferLastAttempt;
    private string? _localErrorCode;
    private FileObservation? _selectedObservation;

    /// <summary>Read-only inventory presentation adapter. The injected application service alone owns scans, read grants and immutable results.</summary>
    public InventoryViewModel(IInventoryApplicationService service, Action<Action> postToUi)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _postToUi = postToUi ?? throw new ArgumentNullException(nameof(postToUi));
        _state = service.State;
        _latestVersion = _state.SelectionVersion;

        _start = new InventoryCommand(_ => _ = StartConfirmedAsync(), _ => CanStart);
        _cancel = new InventoryCommand(_ => RequestCancellation(), _ => CanCancel);
        _selectObservation = new InventoryCommand(SelectObservation, value =>
            value is FileObservation observation &&
            Observations.Any(x => x.ObservationId == observation.ObservationId));
        _showCurrent = new InventoryCommand(_ => ShowComplete(), _ => Current is not null);
        _showAttempt = new InventoryCommand(_ => ShowAttempt(), _ => LastAttempt is not null);
        _service.StateChanged += OnServiceChanged;
    }

    /// <summary>Notifies Avalonia bindings after service state transitions are accepted on the supplied UI dispatcher.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Starts one explicitly confirmed inventory request; selection alone cannot run a scan.</summary>
    public ICommand StartInventoryCommand => _start;
    /// <summary>Requests cancellation of an active scan through the application service; it does not discard the last complete snapshot.</summary>
    public ICommand CancelInventoryCommand => _cancel;
    /// <summary>Selects an observation only when its identity exists in the currently displayed snapshot.</summary>
    public ICommand SelectObservationCommand => _selectObservation;
    /// <summary>Switches presentation to the last accepted complete snapshot, if available.</summary>
    public ICommand ShowLastCompleteCommand => _showCurrent;
    /// <summary>Switches presentation to the last attempt, including partial or failed evidence.</summary>
    public ICommand ShowLastAttemptCommand => _showAttempt;

    /// <summary>Exposes the service-owned inventory lifecycle phase, not an inferred UI result.</summary>
    public InventoryRunPhase Phase => _state.Phase;
    /// <summary>Displays the user-selected local folder only in the UI; the path must never be logged.</summary>
    public string? SelectedLocalRoot => _state.SelectedLocalRoot; // local UI only; NEVER log
    /// <summary>Indicates a running scan or a cancellation request that has not finished.</summary>
    public bool IsBusy => Phase is InventoryRunPhase.Running or InventoryRunPhase.CancellationRequested;
    /// <summary>Prevents selection changes while running, cancelling or disposed.</summary>
    public bool CanChangeFolder => !_disposed && !IsBusy;
    /// <summary>Stores one-shot explicit user confirmation for the selected scope; changing selection clears consent.</summary>
    public bool ConsentConfirmed
    {
        get => _consentConfirmed;
        set
        {
            if (_disposed || IsBusy || value == _consentConfirmed) return;
            _consentConfirmed = value;
            Changed();
            Changed(nameof(CanStart));
            _start.NotifyCanExecuteChanged();
        }
    }
    /// <summary>Requires a selected root, one-shot consent and an eligible idle service phase; no implicit scanning.</summary>
    public bool CanStart => !_disposed && _consentConfirmed && !IsBusy &&
        !string.IsNullOrWhiteSpace(SelectedLocalRoot) &&
        (Phase is InventoryRunPhase.Ready or InventoryRunPhase.Completed or
            InventoryRunPhase.Rejected or InventoryRunPhase.Faulted);
    /// <summary>Permits cancellation only during an active run and before disposal.</summary>
    public bool CanCancel => !_disposed && Phase == InventoryRunPhase.Running;
    /// <summary>Indicates that scan progress has no reliable total.</summary>
    public bool IsProgressIndeterminate => IsBusy && _state.ProgressIsIndeterminate;
    /// <summary>Returns the last complete service snapshot, preserved when later attempts fail or are partial.</summary>
    public InventorySnapshot? Current => _state.Current;
    /// <summary>Returns evidence from the most recent attempt independently of Current.</summary>
    public InventorySnapshot? LastAttempt => _state.LastAttempt;
    /// <summary>Indicates that the user explicitly selected the last-attempt presentation.</summary>
    public bool ShowingLastAttempt => _preferLastAttempt && LastAttempt is not null;
    /// <summary>Returns the selected attempt or complete snapshot without promoting incomplete evidence to Current.</summary>
    public InventorySnapshot? DisplayedSnapshot => ShowingLastAttempt
        ? LastAttempt : Current ?? LastAttempt;

    /// <summary>Observations from the displayed generation only; their identifiers are not durable game identities.</summary>
    public IReadOnlyList<FileObservation> Observations =>
        DisplayedSnapshot?.Observations ?? Array.Empty<FileObservation>();
    /// <summary>Diagnostics from the displayed generation, with service-sanitized codes.</summary>
    public IReadOnlyList<InventoryDiagnostic> Diagnostics =>
        DisplayedSnapshot?.Diagnostics ?? Array.Empty<InventoryDiagnostic>();
    /// <summary>Coverage proof for the displayed generation; an incomplete scan does not prove absence.</summary>
    public IReadOnlyList<InventoryCoverage> Coverage =>
        DisplayedSnapshot?.CoverageProof ?? Array.Empty<InventoryCoverage>();
    /// <summary>Restricts selection to an observation in the currently displayed generation.</summary>
    public FileObservation? SelectedObservation
    {
        get => _selectedObservation;
        set
        {
            if (value is not null && !Observations.Any(x => x.ObservationId == value.ObservationId)) return;
            if (ReferenceEquals(_selectedObservation, value)) return;
            _selectedObservation = value;
            Changed();
        }
    }
    /// <summary>Human-readable source kind from the displayed snapshot, or an explicit unobserved label.</summary>
    public string SourceLabel => DisplayedSnapshot?.Observations.FirstOrDefault()?.SourceKind ??
        DisplayedSnapshot?.Diagnostics.FirstOrDefault()?.SourceKind ?? "Non observé";
    /// <summary>Distinguishes a historical complete snapshot from the last attempt.</summary>
    public string DisplayedGenerationText => DisplayedSnapshot is null ? "Aucune génération disponible" :
        ShowingLastAttempt || Current is null ? "Dernière tentative (historique)" :
        "Dernier inventaire complet (historique)";
    /// <summary>Shows timestamp, status, availability and freshness of the latest historical attempt.</summary>
    public string LatestAttemptText => LastAttempt is null
        ? "Dernière tentative : aucune"
        : "Dernière tentative : " + LastAttempt.ScanStatus + " · " +
            LastAttempt.CapturedAtUtc.ToString("u") + " · ROMS uniquement · " +
            "Disponibilité à la capture : " + LastAttempt.Availability +
            " · Fraîcheur à la capture : " + LastAttempt.Freshness;
    /// <summary>Distinguishes no snapshot, failed coverage and an observed empty result.</summary>
    public string EmptyObservationsText => Observations.Count == 0
        ? DisplayedSnapshot is null ? "Aucun inventaire disponible. Aucune donnée n'a été observée." :
            DisplayedSnapshot.ScanStatus == InventoryScanStatus.Failed
                ? "Lecture impossible : aucun total de fichiers établi."
                : "Aucune observation dans cette génération (la couverture reste à vérifier)."
        : "";
    /// <summary>Displays the observed count, or unknown when failure gives no defensible count.</summary>
    public string ObservedFilesText => DisplayedSnapshot is null ||
        (DisplayedSnapshot.ScanStatus == InventoryScanStatus.Failed &&
         DisplayedSnapshot.ObservedFileCount == 0)
        ? "Fichiers observés : Inconnu"
        : "Fichiers observés : " + DisplayedSnapshot.ObservedFileCount;
    /// <summary>Shows a total only when a complete scan supplies a supported file count.</summary>
    public string TotalFilesText => DisplayedSnapshot?.ScanStatus == InventoryScanStatus.Complete &&
        DisplayedSnapshot.TotalFileCount is int total
        ? "Total ROMS couvert : " + total
        : "Total ROMS : Inconnu (couverture non complète)";
    /// <summary>Shows the confirmed games count only when available; unknown is not zero.</summary>
    public string IdentifiedGamesText => DisplayedSnapshot?.ConfirmedGameCount is int games
        ? "Jeux identifiés : " + games
        : "Jeux identifiés : Inconnu";
    /// <summary>Presents the stored scan status and capture time without checking a current filesystem.</summary>
    public string ScanStatusText => DisplayedSnapshot is null
        ? "Aucune preuve disponible"
        : "Scan " + DisplayedSnapshot.ScanStatus + " · ROMS · " +
            DisplayedSnapshot.CapturedAtUtc.ToString("u");
    /// <summary>Availability captured by the displayed generation; does not revalidate the filesystem.</summary>
    public string AvailabilityText => DisplayedSnapshot?.Availability.ToString() ?? "Non vérifiée";
    /// <summary>Reports snapshot freshness or an explicit unverified label.</summary>
    public string FreshnessText => DisplayedSnapshot?.Freshness == InventoryFreshness.Unknown ||
        DisplayedSnapshot is null ? "Non vérifiée" : DisplayedSnapshot.Freshness.ToString();
    /// <summary>Neutral UI diagnostic for consent, cancellation, refusal and failure; never exposes exception paths.</summary>
    public string StatusMessage => _localErrorCode is not null
        ? "Sélection ou opération refusée : " + _localErrorCode
        : _state.SafeDiagnosticCode is not null
            ? "Diagnostic du service : " + _state.SafeDiagnosticCode
        : Phase switch
        {
            InventoryRunPhase.NoSelection => "Choisir une bibliothèque locale, sans scan automatique.",
            InventoryRunPhase.Ready => "Dossier sélectionné. Inventaire uniquement sur consentement explicite.",
            InventoryRunPhase.Running => "Inventaire en cours · progression exacte indisponible.",
            InventoryRunPhase.CancellationRequested => "Annulation demandée… attente de l'arrêt.",
            InventoryRunPhase.Completed => "Tentative terminée · consulter la couverture et la provenance.",
            InventoryRunPhase.Rejected => "Opération refusée · aucune preuve de fichiers effacée.",
            InventoryRunPhase.Faulted => "Lecture impossible · dernier complet conservé si disponible.",
            _ => "État non vérifié."
        };

    // Called ONLY after a native picker returned a candidate. The service checks the root,
    // without enumerating ROMS until separate consent. Reject preserves previous selection.
    /// <summary>Validates a picker-selected scope via the service without enumerating ROMS or retaining consent after rejection.</summary>
    public InventorySelectionResult PrepareSelectedFolder(InventoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (!CanChangeFolder)
            return new(false, "SCAN_RUNNING_OR_CLOSED", _latestVersion);
        var result = _service.PrepareSelection(scope);
        if (result.Accepted)
        {
            _latestVersion = Math.Max(_latestVersion, result.SelectionVersion);
            _consentConfirmed = false;
            _preferLastAttempt = false;
            _selectedObservation = null;
            _localErrorCode = null;
            Apply(_service.State);
        }
        else
        {
            // A rejected choice preserves the previous library but must never
            // preserve a confirmation that could now target the wrong folder.
            _consentConfirmed = false;
            _localErrorCode = result.SafeDiagnosticCode ?? "SELECTION_REJECTED";
            NotifyAll();
        }
        return result;
    }

    // Explicit user action; never invoked by constructor, changing tabs, or setting folder.
    /// <summary>Consumes one-shot user consent and delegates the actual scan and cancellation policy to the injected service.</summary>
    public async Task StartConfirmedAsync()
    {
        if (!CanStart) return;
        _localErrorCode = null;
        _consentConfirmed = false; // consent is one-shot, even if the service refuses.
        NotifyAll();
        try
        {
            // The service re-checks consent, version, roots, single-flight and deny-by-default.
            await _service.StartConfirmedScanAsync(
                new InventoryRunRequest(_latestVersion, ExplicitUserConsent: true));
            // StateChanged remains the authority for the display. This is only a fallback
            // for an implementation that completed without raising the last notification.
            var state = _service.State;
            _postToUi(() => ApplyIfCurrent(state));
        }
        catch (Exception)
        {
            // No exception messages: they may contain a personal absolute path.
            _postToUi(() =>
            {
                if (_disposed) return;
                _localErrorCode = "SERVICE_FAULT";
                NotifyAll(); // NEVER invent a Failed/Partial InventorySnapshot.
            });
        }
    }

    /// <summary>Requests cancellation through the service; returns false when no active run can be cancelled.</summary>
    public bool RequestCancellation()
    {
        if (!CanCancel) return false;
        var accepted = _service.RequestCancellation();
        if (accepted) ApplyIfCurrent(_service.State);
        return accepted;
    }

    // A picker cancel does not call this. Explicitly clearing is forbidden during a
    // scan, even when cancellation has been requested; the service owns that gate.
    /// <summary>Explicitly clears the selection only when no run or cancellation is active.</summary>
    public bool ClearSelection()
    {
        if (!CanChangeFolder || !_service.ClearSelection()) return false;
        _consentConfirmed = false;
        _localErrorCode = null;
        ApplyIfCurrent(_service.State);
        return true;
    }

    /// <summary>Reports native folder-picker failure through a neutral code instead of exposing a local path.</summary>
    public void ReportPickerError()
    {
        if (_disposed) return;
        _localErrorCode = "FOLDER_PICKER_UNAVAILABLE";
        NotifyAll();
    }

    /// <summary>Presents Current, when available, and resets the selected observation.</summary>
    public void ShowComplete()
    {
        if (Current is null) return;
        _preferLastAttempt = false;
        _selectedObservation = null;
        NotifyAll();
    }

    /// <summary>Presents LastAttempt without treating a partial or failed run as complete.</summary>
    public void ShowAttempt()
    {
        if (LastAttempt is null) return;
        _preferLastAttempt = true;
        _selectedObservation = null;
        NotifyAll();
    }

    private void SelectObservation(object? candidate)
    {
        if (candidate is not FileObservation observation) return;
        SelectedObservation = Observations.FirstOrDefault(x =>
            x.ObservationId == observation.ObservationId);
    }

    private void OnServiceChanged(object? sender, InventoryApplicationState state) =>
        _postToUi(() => ApplyIfCurrent(state));

    private void ApplyIfCurrent(InventoryApplicationState state)
    {
        // Queued dispatcher notifications may arrive after cancellation, completion
        // or a library switch *with the same selection version*. Only the service's
        // CURRENT immutable state is eligible; never restore an older Running frame.
        if (_disposed || state.SelectionVersion < _latestVersion ||
            !ReferenceEquals(state, _service.State)) return;
        _latestVersion = state.SelectionVersion;
        Apply(state);
    }

    private void Apply(InventoryApplicationState state)
    {
        if (_disposed) return;
        var contextChanged = state.SelectionVersion != _state.SelectionVersion;
        var snapshotChanged = !ReferenceEquals(state.Current, _state.Current) ||
            !ReferenceEquals(state.LastAttempt, _state.LastAttempt);
        _state = state;
        if (contextChanged || snapshotChanged)
        {
            _selectedObservation = null;
            _preferLastAttempt = false;
        }
        if (state.Phase is InventoryRunPhase.Running or InventoryRunPhase.CancellationRequested
            or InventoryRunPhase.Completed)
            _localErrorCode = null;
        NotifyAll();
    }

    private void NotifyAll()
    {
        foreach (var property in new[]
        {
            nameof(Phase), nameof(SelectedLocalRoot), nameof(ConsentConfirmed), nameof(IsBusy),
            nameof(CanChangeFolder), nameof(CanStart), nameof(CanCancel),
            nameof(IsProgressIndeterminate), nameof(Current), nameof(LastAttempt),
            nameof(ShowingLastAttempt), nameof(DisplayedSnapshot), nameof(Observations),
            nameof(Diagnostics), nameof(Coverage), nameof(SelectedObservation),
            nameof(SourceLabel), nameof(DisplayedGenerationText), nameof(LatestAttemptText),
            nameof(EmptyObservationsText), nameof(ObservedFilesText), nameof(TotalFilesText),
            nameof(IdentifiedGamesText), nameof(ScanStatusText),
            nameof(AvailabilityText), nameof(FreshnessText), nameof(StatusMessage)
        })
            Changed(property);
        _start.NotifyCanExecuteChanged();
        _cancel.NotifyCanExecuteChanged();
        _selectObservation.NotifyCanExecuteChanged();
        _showCurrent.NotifyCanExecuteChanged();
        _showAttempt.NotifyCanExecuteChanged();
    }

    private void Changed([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    /// <summary>Unsubscribes service notifications; the host retains responsibility for cancelling and disposing the service.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _service.StateChanged -= OnServiceChanged;
        // The application service lifetime belongs to its host. The host requests
        // cancellation and disposes the service AFTER the in-flight scan has finished.
        NotifyAll();
    }
}
