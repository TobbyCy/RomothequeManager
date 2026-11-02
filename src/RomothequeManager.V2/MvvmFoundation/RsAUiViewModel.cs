using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.RsA;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Fixture-only UI adapter for RS-A; never grants access to a personal ROMS root.</summary>
/// <remarks>Only the CI fixture composition can create this internal ViewModel. The
/// actual rights, root identity, traversal, limits and cancellation stay in RS-A.
/// No filesystem access, persistence or permissions are implemented by Avalonia.</remarks>
public sealed class RsAUiViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly RsAFixtureRomSession _scanner;
    private readonly Action<Action> _postToUi;
    private readonly InventoryCommand _start;
    private readonly InventoryCommand _cancel;
    private readonly InventoryCommand _showComplete;
    private readonly InventoryCommand _showAttempt;
    private CancellationTokenSource? _cancellation;
    private InventorySnapshot? _current;
    private InventorySnapshot? _attempt;
    private RsAUiPhase _phase = RsAUiPhase.Idle;
    private bool _consent;
    private bool _selected;
    private bool _offline;
    private bool _preferAttempt;
    private bool _disposed;
    private long _generation;
    private int _observedEntries;
    private string _safeDiagnostic = "Aucune sélection.";

    /// <summary>Internal fixture-only RS-A presentation; the scanner owns authorization, root checks and read-only file observations.</summary>
    internal RsAUiViewModel(Action<Action> postToUi, RsAFixtureRomSession? scanner = null)
    {
        _postToUi = postToUi ?? throw new ArgumentNullException(nameof(postToUi));
        _scanner = scanner ?? new RsAFixtureRomSession();
        _start = new InventoryCommand(_ => _ = StartConfirmedAsync(), _ => CanStart);
        _cancel = new InventoryCommand(_ => RequestCancellation(), _ => CanCancel);
        _showComplete = new InventoryCommand(_ => ShowAttempt(false), _ => _current is not null);
        _showAttempt = new InventoryCommand(_ => ShowAttempt(true), _ => _attempt is not null);
    }

    /// <summary>Raises UI state notifications for accepted fixture scan and cancellation transitions.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Begins a one-shot explicitly authorized fixture scan when permitted.</summary>
    public ICommand StartCommand => _start;
    /// <summary>Requests cancellation and revokes the scoped fixture grant.</summary>
    public ICommand CancelCommand => _cancel;
    /// <summary>Presents the last complete fixture scan without promoting failed attempts.</summary>
    public ICommand ShowCompleteCommand => _showComplete;
    /// <summary>Presents the latest attempt, including refused, failed and cancelled evidence.</summary>
    public ICommand ShowAttemptCommand => _showAttempt;
    /// <summary>Current fixture-only RS-A lifecycle state.</summary>
    public RsAUiPhase Phase => _phase;
    /// <summary>Indicates a synthetic ROMS root was explicitly selected.</summary>
    public bool HasSelection => _selected;
    /// <summary>True while scanning or waiting for cancellation to complete.</summary>
    public bool IsBusy => _phase is RsAUiPhase.Running or RsAUiPhase.Cancelling;
    /// <summary>Disallows changing fixture roots during a run or after disposal.</summary>
    public bool CanSelect => !_disposed && !IsBusy;
    /// <summary>Requires fixture selection, one-shot consent, online status and no active operation.</summary>
    public bool CanStart => !_disposed && _selected && _consent && !_offline && !IsBusy;
    /// <summary>Permits cancellation only for an active fixture scan.</summary>
    public bool CanCancel => !_disposed && _phase == RsAUiPhase.Running;
    /// <summary>One-shot confirmation for an already selected internal fixture root; never a real library grant.</summary>
    public bool ConsentConfirmed
    {
        get => _consent;
        set
        {
            if (_disposed || IsBusy || !_selected || _consent == value) return;
            _consent = value;
            Publish();
        }
    }

    /// <summary>Last successful complete snapshot retained across later failed or partial attempts.</summary>
    public InventorySnapshot? CurrentComplete => _current;
    /// <summary>Most recent fixture scan evidence regardless of its completion state.</summary>
    public InventorySnapshot? LastAttempt => _attempt;
    /// <summary>Shows selected latest attempt or preserved complete evidence.</summary>
    public InventorySnapshot? DisplayedSnapshot => _preferAttempt && _attempt is not null
        ? _attempt : _current ?? _attempt;
    /// <summary>Marks a simulated offline state without rechecking the filesystem.</summary>
    public bool IsOffline => _offline;
    /// <summary>Redacted fixture-root selection label; no personal absolute path is shown.</summary>
    public string SelectedRootLabel => _selected
        ? "ROMS · racine fictive sélectionnée (aucun chemin personnel affiché)"
        : "Aucune racine ROMS sélectionnée.";
    /// <summary>Safe localized lifecycle text, distinguishing cancellation from OFFLINE and refusal.</summary>
    public string PhaseLabel => _phase switch
    {
        RsAUiPhase.Idle => "En attente de sélection explicite.",
        RsAUiPhase.AwaitingConsent => "Racine fictive choisie. Autorisation temporaire à confirmer.",
        RsAUiPhase.Running => "RS-A · inventaire de métadonnées en cours.",
        RsAUiPhase.Cancelling => "Annulation demandée ; attente de l'arrêt sécurisé.",
        RsAUiPhase.Cancelled => "Annulée par l'utilisateur ; dernier inventaire complet conservé.",
        RsAUiPhase.Complete => "Inventaire complet de la fixture uniquement.",
        RsAUiPhase.Partial => "Tentative partielle ; dernier inventaire complet conservé.",
        RsAUiPhase.Refused => "Accès refusé par RS-A ; aucun droit déduit du choix UI.",
        RsAUiPhase.Failed => "Tentative échouée ; aucune absence de fichier déduite.",
        RsAUiPhase.Offline => "OFFLINE / STALE · présence non revérifiée, sans auto-scan.",
        _ => "Statut non vérifié."
    };
    /// <summary>Shows examined-entry count with an unknown total, never a fabricated percentage.</summary>
    public string ProgressLabel => IsBusy
        ? "Entrées examinées : " + Volatile.Read(ref _observedEntries) +
          " · total inconnu (progression indéterminée)."
        : "Dernière tentative : " + AttemptStatusLabel(_attempt) +
          " · entrées examinées : " + Volatile.Read(ref _observedEntries) + ".";
    /// <summary>Neutral scanner diagnostic code without private paths or exception details.</summary>
    public string SafeDiagnostic => _safeDiagnostic;
    /// <summary>Historical capture and stale/offline label, not evidence of current availability.</summary>
    public string SnapshotLabel => DisplayedSnapshot is { } snapshot
        ? "Source RS-A synthétique : " + AttemptStatusLabel(snapshot) +
          " · capturé " + snapshot.CapturedAtUtc.ToString("u") +
          (_offline ? " · OFFLINE / STALE" : "") +
          " · jeux identifiés : inconnu"
        : "Aucune génération RS-A consultable.";

    // Backend cancellation may be Failed (no entries) or Partial (some entries).
    // Only an explicit RS_A_CANCELLED diagnostic permits the UX cancellation label.
    private static bool IsCancelled(InventorySnapshot? snapshot) =>
        snapshot is { ScanStatus: InventoryScanStatus.Failed or InventoryScanStatus.Partial } &&
        snapshot.Diagnostics.Any(d => d.Code == "RS_A_CANCELLED");

    private static string AttemptStatusLabel(InventorySnapshot? snapshot) =>
        snapshot is null ? "aucune" : IsCancelled(snapshot)
            ? "Annulée (RS_A_CANCELLED)" : snapshot.ScanStatus.ToString();

    /// <summary>Called by the internal CI harness only after creating its own disposable
    /// rm-rsa-fixture-{GUID}/ROMS. Selection performs no enumeration and grants no access.</summary>
    internal bool SelectFixture(string romsRoot, string rootId)
    {
        if (!CanSelect) return false;
        _scanner.Revoke();
        _generation++;
        _consent = false;
        _selected = false;
        _offline = false;
        _preferAttempt = false;
        _current = null; // never reinterpret the prior root's Current as this root
        _attempt = null;
        Interlocked.Exchange(ref _observedEntries, 0);
        try
        {
            _scanner.SelectFixture(romsRoot, rootId);
            _selected = true;
            _phase = RsAUiPhase.AwaitingConsent;
            _safeDiagnostic = "RS_A_SELECTION_ONLY";
        }
        catch (Exception ex) when (ex is ArgumentException or UnauthorizedAccessException
            or PlatformNotSupportedException or IOException)
        {
            _phase = RsAUiPhase.Refused;
            _safeDiagnostic = "RS_A_FIXTURE_SCOPE_REJECTED";
        }
        Publish();
        return _selected;
    }

    /// <summary>One explicit user confirmation gives a volatile, scoped internal grant.
    /// Production RealRomInventoryGate remains unconditionally closed.</summary>
    internal async Task StartConfirmedAsync()
    {
        if (!CanStart)
        {
            if (!_disposed && !IsBusy)
            {
                _phase = RsAUiPhase.Refused;
                _safeDiagnostic = "RS_A_CONSENT_REQUIRED";
                Publish();
            }
            return;
        }

        _consent = false; // never persist or reuse the confirmation
        var generation = ++_generation;
        Interlocked.Exchange(ref _observedEntries, 0);
        RsAReadGrant grant;
        try
        {
            grant = _scanner.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(2));
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or
            System.ComponentModel.Win32Exception or PlatformNotSupportedException)
        {
            _phase = RsAUiPhase.Refused;
            _safeDiagnostic = "RS_A_GRANT_REFUSED";
            Publish();
            return;
        }

        var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        _phase = RsAUiPhase.Running;
        _safeDiagnostic = "RS_A_RUNNING";
        Publish();
        try
        {
            // All actual Windows I/O and permission checks remain in DEV 1's scanner.
            var result = await Task.Run(() => _scanner.Scan(grant, cancellation.Token, _ =>
            {
                var count = Interlocked.Increment(ref _observedEntries);
                if (count == 1 || count % 25 == 0)
                    _postToUi(() =>
                    {
                        if (!_disposed && _generation == generation) Notify(nameof(ProgressLabel));
                    });
            }));
            _postToUi(() =>
            {
                if (_disposed || _generation != generation) return;
                _attempt = result;
                if (result.ScanStatus == InventoryScanStatus.Complete &&
                    result.Availability == InventoryAvailability.Online &&
                    result.Freshness == InventoryFreshness.Fresh)
                    _current = result;
                _phase = result.ScanStatus switch
                {
                    _ when IsCancelled(result) => RsAUiPhase.Cancelled,
                    InventoryScanStatus.Complete => RsAUiPhase.Complete,
                    InventoryScanStatus.Partial => RsAUiPhase.Partial,
                    _ when result.Diagnostics.Any(d => d.Code == "RS_A_DENIED") =>
                        RsAUiPhase.Refused,
                    _ => RsAUiPhase.Failed
                };
                _safeDiagnostic = result.Diagnostics.FirstOrDefault()?.Code ?? "RS_A_RESULT";
                Publish();
            });
        }
        catch (Exception)
        {
            _postToUi(() =>
            {
                if (_disposed || _generation != generation) return;
                _phase = RsAUiPhase.Failed;
                _safeDiagnostic = "RS_A_UI_UNEXPECTED_FAILURE";
                Publish(); // never synthesize a successful InventorySnapshot
            });
        }
        finally
        {
            _scanner.Revoke();
            _cancellation = null;
            cancellation.Dispose();
        }
    }

    /// <summary>Requests cancellation and revokes the one-shot grant; no further scan rights are inferred.</summary>
    internal bool RequestCancellation()
    {
        if (!CanCancel || _cancellation is null) return false;
        _phase = RsAUiPhase.Cancelling;
        _safeDiagnostic = "RS_A_CANCEL_REQUESTED";
        _cancellation.Cancel();
        _scanner.Revoke();
        Publish();
        return true;
    }

    /// <summary>CI-only simulated disconnect. Historical data stays visible but
    /// stale; it never reauthorizes the scanner or proves current availability.</summary>
    internal void MarkOfflineForCi()
    {
        if (_disposed || IsBusy) return;
        _scanner.Revoke();
        _consent = false;
        _offline = true;
        _phase = RsAUiPhase.Offline;
        _safeDiagnostic = "RS_A_SIMULATED_OFFLINE";
        Publish();
    }

    private void ShowAttempt(bool attempt)
    {
        if (_disposed) return;
        _preferAttempt = attempt;
        Publish();
    }

    private void Publish()
    {
        foreach (var key in new[] { nameof(Phase), nameof(HasSelection),
            nameof(IsBusy), nameof(CanSelect), nameof(CanStart), nameof(CanCancel),
            nameof(ConsentConfirmed), nameof(CurrentComplete), nameof(LastAttempt),
            nameof(DisplayedSnapshot), nameof(IsOffline), nameof(SelectedRootLabel),
            nameof(PhaseLabel), nameof(ProgressLabel), nameof(SafeDiagnostic),
            nameof(SnapshotLabel) }) Notify(key);
        _start.NotifyCanExecuteChanged();
        _cancel.NotifyCanExecuteChanged();
        _showComplete.NotifyCanExecuteChanged();
        _showAttempt.NotifyCanExecuteChanged();
    }

    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    /// <summary>Revokes fixture permission and cancels outstanding work; no late UI adoption is authorized.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _scanner.Revoke();
        _cancellation?.Cancel();
        _consent = false;
        _selected = false;
    }
}

/// <summary>Fixture presentation phases distinguishing consent, running, cancelled, refused and offline snapshots.</summary>
public enum RsAUiPhase
{
    Idle, AwaitingConsent, Running, Cancelling, Cancelled, Complete, Partial,
    Refused, Failed, Offline
}
