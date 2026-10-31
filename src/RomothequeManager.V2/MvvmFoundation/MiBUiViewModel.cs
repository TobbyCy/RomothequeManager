using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory.Media.MiB;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>UI-only adapter for a CI-owned MEDIAS fixture. No paths or grants are exposed to the View.</summary>
internal sealed class MiBUiViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly IMiBFixtureMediaApplicationService _service;
    private readonly Action<Action> _dispatch;
    private readonly string _fixtureRoot;
    private readonly InventoryCommand _select, _confirm, _start, _cancel, _showCurrent, _showAttempt;
    private MiBProjectionState _state;
    private bool _consent, _showLast, _disposed;
    private long _uiGeneration;
    internal MiBUiViewModel(IMiBFixtureMediaApplicationService service, string fixtureRoot, Action<Action> dispatch)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _fixtureRoot = fixtureRoot ?? throw new ArgumentNullException(nameof(fixtureRoot));
        _dispatch = dispatch ?? throw new ArgumentNullException(nameof(dispatch));
        _state = service.State;
        _select = new InventoryCommand(_ => Select(), _ => CanSelect);
        _confirm = new InventoryCommand(_ => Confirm(), _ => CanConfirm);
        _start = new InventoryCommand(_ => _ = StartAsync(), _ => CanStart);
        _cancel = new InventoryCommand(_ => Cancel(), _ => CanCancel);
        _showCurrent = new InventoryCommand(_ => { _showLast = false; Publish(); }, _ => CurrentComplete is not null);
        _showAttempt = new InventoryCommand(_ => { _showLast = true; Publish(); }, _ => LastAttempt is not null);
        _service.StateChanged += OnStateChanged;
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand SelectCommand => _select;
    public ICommand ConfirmCommand => _confirm;
    public ICommand StartCommand => _start;
    public ICommand CancelCommand => _cancel;
    public ICommand ShowCurrentCommand => _showCurrent;
    public ICommand ShowAttemptCommand => _showAttempt;
    public MiBProjectionState State => _state;
    public MiBRunView? CurrentComplete => _state.CurrentComplete;
    public MiBRunView? LastAttempt => _state.LastAttempt;
    public MiBRunView? Displayed => _showLast && LastAttempt is not null ? LastAttempt : CurrentComplete ?? LastAttempt;
    public bool IsBusy => _state.Phase is MiBPhase.Running or MiBPhase.CancellationRequested;
    public bool CanSelect => !_disposed && !IsBusy;
    public bool CanConfirm => !_disposed && !_consent && _state.Phase is (MiBPhase.Ready or MiBPhase.Completed or MiBPhase.Rejected or MiBPhase.Faulted);
    public bool CanStart => !_disposed && _consent && _state.Phase == MiBPhase.Ready;
    public bool CanCancel => !_disposed && _state.Phase == MiBPhase.Running;
    public bool ConsentConfirmed => _consent;
    public string SelectionLabel => "Sélection MEDIAS · version " + _state.SelectionVersion + " · aucun chemin affiché";
    public string DisplayModeLabel => _showLast ? "Affichage : LastAttempt" : "Affichage : CurrentComplete";
    public string ConsentLabel => _consent ? "Consentement temporaire confirmé pour cette sélection." : "Aucun consentement actif ; sélectionner ne donne aucun droit.";
    private bool LastAttemptCancelled => LastAttempt?.SafeDiagnosticCodes.Contains("MI_A_CANCELLED") == true;
    private bool LastAttemptDenied => LastAttempt?.SafeDiagnosticCodes.Contains("MI_A_DENIED") == true;
    // CurrentIsHistorical also means a retained Complete after a same-root cancellation.
    // It does NOT prove Offline/Stale. Only an explicit archived fixture with source facts
    // can render the offline banner; the backend's MI_A_CANCELLED always takes precedence.
    private bool IsOfflineStaleArchive => Displayed is { IsHistoricalFixture: true } run &&
        (run.Availability == RomothequeManager.V2.Inventory.InventoryAvailability.Offline ||
         run.SourceFreshness == RomothequeManager.V2.Inventory.InventoryFreshness.Stale);
    public string PhaseLabel => _state.Phase switch
        {
            MiBPhase.Completed when LastAttemptCancelled =>
                "Annulée (MI_A_CANCELLED) · dernier complet conservé",
            MiBPhase.Completed when LastAttemptDenied => "Refusée (MI_A_DENIED)",
            _ when IsOfflineStaleArchive =>
                "Historique OFFLINE / STALE · aucune revalidation",
            MiBPhase.NoSelection => "Aucune sélection MEDIAS",
            MiBPhase.Ready => "Prêt · consentement explicite requis",
            MiBPhase.Running => "Analyse des en-têtes en cours",
            MiBPhase.CancellationRequested => "Annulation demandée · révocation en cours",
            MiBPhase.Rejected => "Accès refusé · aucun scan implicite",
            MiBPhase.Faulted => "Échec technique · aucune absence déduite",
            MiBPhase.Completed when LastAttempt?.ScanStatus == RomothequeManager.V2.Inventory.InventoryScanStatus.Partial =>
                "Tentative partielle · couverture incomplète",
            MiBPhase.Completed when LastAttempt?.ScanStatus == RomothequeManager.V2.Inventory.InventoryScanStatus.Failed =>
                "Tentative échouée · couverture inconnue",
            MiBPhase.Completed => "Analyse terminée · résultat observé",
            _ => "Statut non vérifié"
        };
    public string DiagnosticLabel => LastAttemptCancelled ? "MI_A_CANCELLED" :
        LastAttemptDenied ? "MI_A_DENIED" :
        _state.SafeNoticeCode ?? LastAttempt?.SafeDiagnosticCodes.FirstOrDefault() ?? "Aucun diagnostic";
    public string ProgressLabel => IsBusy ? "Lecture des en-têtes · progression indéterminée · total inconnu" :
        "Observations : " + (Displayed?.ObservedMediaCount.ToString() ?? "inconnu") +
        " · total connu : " + (Displayed?.KnownObservedMediaTotal?.ToString() ?? "inconnu");
    public string AvailabilityLabel => Displayed is null ? "Aucun résultat" :
        Displayed.Availability + " / " + Displayed.SourceFreshness +
        (Displayed.IsHistoricalFixture ? " · aucune revalidation" : "");
    public IReadOnlyList<MiBMediaRow> Rows => Displayed?.Rows ?? Array.Empty<MiBMediaRow>();
    public bool ProgressIndeterminate => _state.ProgressIsIndeterminate;
    internal bool Select()
    {
        if (!CanSelect) return false;
        ++_uiGeneration;
        _consent = false;
        _showLast = false;
        var selected = _service.SelectFixtureRoot(_fixtureRoot);
        _state = _service.State;
        Publish();
        return selected.Accepted;
    }
    internal bool Confirm()
    {
        if (!CanConfirm) return false;
        _consent = _service.ConfirmMediaIndexConsent(_state.SelectionVersion, true);
        _state = _service.State;
        Publish();
        return _consent;
    }
    internal async Task StartAsync()
    {
        if (!CanStart) return;
        _consent = false; // one-shot, before the first await
        var generation = _uiGeneration;
        var version = _state.SelectionVersion;
        Publish();
        try
        {
            var result = await _service.StartConfirmedAsync(version).ConfigureAwait(false);
            _dispatch(() => {
                if (_disposed || generation != _uiGeneration || version != _service.State.SelectionVersion) return;
                _state = result;
                Publish();
            });
        }
        catch (Exception)
        {
            _dispatch(() => { if (!_disposed && generation == _uiGeneration) { _state = _service.State; Publish(); } });
        }
    }
    internal bool Cancel() => CanCancel && _service.RequestCancellation();
    private void OnStateChanged(object? sender, MiBProjectionState state) =>
        _dispatch(() => {
            if (_disposed || state.SelectionVersion != _service.State.SelectionVersion ||
                !ReferenceEquals(state, _service.State)) return;
            if (state.SelectionVersion != _state.SelectionVersion)
            {
                ++_uiGeneration;
                _consent = false;
                _showLast = false;
            }
            _state = state;
            Publish();
        });
    private void Publish()
    {
        foreach (var property in new[] { nameof(State), nameof(CurrentComplete), nameof(LastAttempt),
            nameof(Displayed), nameof(IsBusy), nameof(CanSelect), nameof(CanConfirm), nameof(CanStart),
            nameof(CanCancel), nameof(ConsentConfirmed), nameof(ConsentLabel), nameof(SelectionLabel), nameof(DisplayModeLabel), nameof(PhaseLabel), nameof(DiagnosticLabel),
            nameof(ProgressLabel), nameof(AvailabilityLabel), nameof(Rows), nameof(ProgressIndeterminate) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
        foreach (var command in new[] { _select, _confirm, _start, _cancel, _showCurrent, _showAttempt })
            command.NotifyCanExecuteChanged();
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        ++_uiGeneration;
        _consent = false;
        _service.StateChanged -= OnStateChanged;
        await _service.DisposeAsync();
    }
}
