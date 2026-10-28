using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

namespace RomothequeManager.V2.MvvmFoundation;

// NEXT-03 foundation: never constructs InventorySession, reads a folder, or uses Avalonia.
// DEV 1's application service is sole owner of scans, cancellation and Current/LastAttempt.
// The View injects a UI-thread dispatcher and owns the native folder picker.
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

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand StartInventoryCommand => _start;
    public ICommand CancelInventoryCommand => _cancel;
    public ICommand SelectObservationCommand => _selectObservation;
    public ICommand ShowLastCompleteCommand => _showCurrent;
    public ICommand ShowLastAttemptCommand => _showAttempt;

    public InventoryRunPhase Phase => _state.Phase;
    public string? SelectedLocalRoot => _state.SelectedLocalRoot; // local UI only; NEVER log
    public bool IsBusy => Phase is InventoryRunPhase.Running or InventoryRunPhase.CancellationRequested;
    public bool CanChangeFolder => !_disposed && !IsBusy;
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
    public bool CanStart => !_disposed && _consentConfirmed && !IsBusy &&
        !string.IsNullOrWhiteSpace(SelectedLocalRoot) &&
        (Phase is InventoryRunPhase.Ready or InventoryRunPhase.Completed or
            InventoryRunPhase.Rejected or InventoryRunPhase.Faulted);
    public bool CanCancel => !_disposed && Phase == InventoryRunPhase.Running;
    public bool IsProgressIndeterminate => IsBusy && _state.ProgressIsIndeterminate;
    public InventorySnapshot? Current => _state.Current;
    public InventorySnapshot? LastAttempt => _state.LastAttempt;
    public bool ShowingLastAttempt => _preferLastAttempt && LastAttempt is not null;
    public InventorySnapshot? DisplayedSnapshot => ShowingLastAttempt
        ? LastAttempt : Current ?? LastAttempt;

    public IReadOnlyList<FileObservation> Observations =>
        DisplayedSnapshot?.Observations ?? Array.Empty<FileObservation>();
    public IReadOnlyList<InventoryDiagnostic> Diagnostics =>
        DisplayedSnapshot?.Diagnostics ?? Array.Empty<InventoryDiagnostic>();
    public IReadOnlyList<InventoryCoverage> Coverage =>
        DisplayedSnapshot?.CoverageProof ?? Array.Empty<InventoryCoverage>();
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
    public string SourceLabel => DisplayedSnapshot?.Observations.FirstOrDefault()?.SourceKind ??
        DisplayedSnapshot?.Diagnostics.FirstOrDefault()?.SourceKind ?? "Non observé";
    public string DisplayedGenerationText => DisplayedSnapshot is null ? "Aucune génération disponible" :
        ShowingLastAttempt || Current is null ? "Dernière tentative (historique)" :
        "Dernier inventaire complet (historique)";
    public string LatestAttemptText => LastAttempt is null
        ? "Dernière tentative : aucune"
        : "Dernière tentative : " + LastAttempt.ScanStatus + " · " +
            LastAttempt.CapturedAtUtc.ToString("u") + " · ROMS uniquement";
    public string EmptyObservationsText => Observations.Count == 0
        ? DisplayedSnapshot is null ? "Aucun inventaire disponible. Aucune donnée n'a été observée." :
            DisplayedSnapshot.ScanStatus == InventoryScanStatus.Failed
                ? "Lecture impossible : aucun total de fichiers établi."
                : "Aucune observation dans cette génération (la couverture reste à vérifier)."
        : "";
    public string ObservedFilesText => DisplayedSnapshot is null ||
        (DisplayedSnapshot.ScanStatus == InventoryScanStatus.Failed &&
         DisplayedSnapshot.ObservedFileCount == 0)
        ? "Fichiers observés : Inconnu"
        : "Fichiers observés : " + DisplayedSnapshot.ObservedFileCount;
    public string TotalFilesText => DisplayedSnapshot?.ScanStatus == InventoryScanStatus.Complete &&
        DisplayedSnapshot.TotalFileCount is int total
        ? "Total ROMS couvert : " + total
        : "Total ROMS : Inconnu (couverture non complète)";
    public string IdentifiedGamesText => DisplayedSnapshot?.ConfirmedGameCount is int games
        ? "Jeux identifiés : " + games
        : "Jeux identifiés : Inconnu";
    public string ScanStatusText => DisplayedSnapshot is null
        ? "Aucune preuve disponible"
        : "Scan " + DisplayedSnapshot.ScanStatus + " · ROMS · " +
            DisplayedSnapshot.CapturedAtUtc.ToString("u");
    public string AvailabilityText => DisplayedSnapshot?.Availability.ToString() ?? "Non vérifiée";
    public string FreshnessText => DisplayedSnapshot?.Freshness == InventoryFreshness.Unknown ||
        DisplayedSnapshot is null ? "Non vérifiée" : DisplayedSnapshot.Freshness.ToString();
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

    public bool RequestCancellation()
    {
        if (!CanCancel) return false;
        var accepted = _service.RequestCancellation();
        if (accepted) ApplyIfCurrent(_service.State);
        return accepted;
    }

    // A picker cancel does not call this. Explicitly clearing is forbidden during a
    // scan, even when cancellation has been requested; the service owns that gate.
    public bool ClearSelection()
    {
        if (!CanChangeFolder || !_service.ClearSelection()) return false;
        _consentConfirmed = false;
        _localErrorCode = null;
        ApplyIfCurrent(_service.State);
        return true;
    }

    public void ReportPickerError()
    {
        if (_disposed) return;
        _localErrorCode = "FOLDER_PICKER_UNAVAILABLE";
        NotifyAll();
    }

    public void ShowComplete()
    {
        if (Current is null) return;
        _preferLastAttempt = false;
        _selectedObservation = null;
        NotifyAll();
    }

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
