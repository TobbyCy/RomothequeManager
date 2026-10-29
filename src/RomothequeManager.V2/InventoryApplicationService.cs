using RomothequeManager.V2.Inventory;

namespace RomothequeManager.V2.Inventory.Application;

// NEXT-03: application orchestration in existing V2 assembly only. InventorySession remains the sole filesystem scanner.
// No Avalonia, ViewModel, XML importer, persistent library identity or progress percentage.
/// <summary>Phase de l'orchestration applicative, distincte du résultat du scan.</summary>
/// <remarks><c>Completed</c> signifie que la tentative est terminée, y compris avec un
/// résultat <c>Partial</c> ou <c>Failed</c> dans son snapshot. Ce n'est pas une
/// garantie de couverture complète.</remarks>
public enum InventoryRunPhase
{
    NoSelection, Ready, Running, CancellationRequested, Completed, Rejected, Faulted
}

/// <summary>État publié au consommateur de l'inventaire pour une sélection locale.</summary>
/// <param name="SelectionVersion">Version de sélection à comparer avant d'appliquer un résultat asynchrone.</param>
/// <param name="Phase">Phase de l'orchestration, pas statut de couverture des fichiers.</param>
/// <param name="SelectedLocalRoot">Chemin local sensible destiné à l'interface ; ne pas publier en télémétrie ou CI.</param>
/// <param name="Current">Dernier snapshot complet valide de la session sélectionnée, éventuellement absent.</param>
/// <param name="LastAttempt">Dernière tentative, pouvant être partielle ou échouée, sans effacer <paramref name="Current"/>.</param>
/// <param name="SafeDiagnosticCode">Code de refus ou d'échec sans détail de chemin ou d'exception.</param>
/// <param name="ProgressIsIndeterminate">Aucun pourcentage d'avancement déduit artificiellement.</param>
/// <remarks>Les comptes de fichiers et de jeux proviennent du seul snapshot observé.
/// Une valeur <c>null</c> signifie « inconnu », et non zéro. Les observations ne
/// confirment ni des jeux identifiés ni des suppressions.</remarks>
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
/// <summary>Statut du dernier essai, ou <c>null</c> en l'absence de tentative.</summary>
    public InventoryScanStatus? LastScanStatus => LastAttempt?.ScanStatus;
/// <summary>Disponibilité constatée par la dernière tentative, ou <c>Unknown</c> sans preuve.</summary>
    public InventoryAvailability LastAvailability =>
        LastAttempt?.Availability ?? InventoryAvailability.Unknown;
/// <summary>Fraîcheur issue de la dernière tentative, jamais inférée de la disponibilité.</summary>
    public InventoryFreshness LastFreshness =>
        LastAttempt?.Freshness ?? InventoryFreshness.Unknown;
/// <summary>Observations de fichiers de <c>LastAttempt</c>, pas le catalogue de jeux.</summary>
    public IReadOnlyList<FileObservation> Observations =>
        LastAttempt?.Observations ?? Array.Empty<FileObservation>();
    public IReadOnlyList<InventoryDiagnostic> Diagnostics =>
        LastAttempt?.Diagnostics ?? Array.Empty<InventoryDiagnostic>();
/// <summary>Preuves de couverture de <c>LastAttempt</c> ; une liste vide ne démontre pas une couverture complète.</summary>
    public IReadOnlyList<InventoryCoverage> CoverageProof =>
        LastAttempt?.CoverageProof ?? Array.Empty<InventoryCoverage>();
/// <summary>Total de fichiers uniquement lorsque la couverture le justifie ; sinon <c>null</c>.</summary>
    public int? TotalFileCount => LastAttempt?.TotalFileCount;
/// <summary>Nombre de jeux confirmés seulement si établi par une source probante ; normalement inconnu ici.</summary>
    public int? ConfirmedGameCount => LastAttempt?.ConfirmedGameCount;
}

/// <summary>Accusé de réception d'un préflight, distinct de toute autorisation de scan.</summary>
/// <param name="Accepted">Indique que la sélection a passé les contrôles bornés, pas qu'un scan est permis.</param>
/// <param name="SafeDiagnosticCode">Code de diagnostic non sensible lors d'un refus.</param>
/// <param name="SelectionVersion">Version à réutiliser pour le consentement explicite de la tentative.</param>
public sealed record InventorySelectionResult(
    bool Accepted, string? SafeDiagnosticCode, long SelectionVersion);

/// <summary>Consentement explicite lié à une version précise de sélection.</summary>
/// <param name="SelectionVersion">Version retournée lors du préflight accepté.</param>
/// <param name="ExplicitUserConsent">Confirmation requise pour cette tentative ; elle ne contourne jamais le blocage des bibliothèques réelles.</param>
public sealed record InventoryRunRequest(long SelectionVersion, bool ExplicitUserConsent);

/// <summary>Contrat d'orchestration de l'unique moteur NEXT-02 pour une bibliothèque locale sélectionnée.</summary>
/// <remarks>Le choix du dossier et le consentement sont explicites. La sélection ne
/// déclenche pas d'énumération. Les appels concurrents sont refusés et le
/// constructeur public du service concret bloque les scans réels, même consentis.
/// La gestion des événements ne garantit aucun thread UI.</remarks>
public interface IInventoryApplicationService : IAsyncDisposable
{
/// <summary>Dernier état disponible ; ne pas confondre un événement reçu tardivement avec l'état courant.</summary>
    InventoryApplicationState State { get; }

    // Fired on the calling or worker thread. Avalonia ViewModels must dispatch to the UI thread.
/// <summary>Notification de changement d'état, éventuellement sur le thread appelant ou travailleur.</summary>
/// <remarks>Les abonnés doivent dispatcher vers le thread UI et ignorer les versions
/// ou références d'état dépassées. Un observateur ne doit pas piloter la cohérence
/// du moteur depuis cet événement.</remarks>
    event EventHandler<InventoryApplicationState>? StateChanged;

    // A restricted metadata-only preflight AFTER an explicit folder-picker selection.
    // Never enumerates ROMS or parses game XML; cancellation of a picker does not call this.
/// <summary>Vérifie les métadonnées de la racine et de <c>ROMS/</c> sans énumérer leur contenu.</summary>
/// <param name="selectedScope">Portée locale choisie volontairement, avec limites bornées.</param>
/// <returns>Acceptation et version, ou code de refus sans détails sensibles.</returns>
/// <remarks>Un sélecteur de dossier annulé ne doit pas appeler cette méthode.
/// Un scan actif bloque le remplacement de la sélection.</remarks>
    InventorySelectionResult PrepareSelection(InventoryScope selectedScope);
/// <summary>Demande une seule tentative asynchrone, après consentement et validation de la version.</summary>
/// <param name="request">Version de sélection et confirmation explicite de cette tentative.</param>
/// <param name="cancellationToken">Demande d'annulation transmise au moteur de lecture.</param>
/// <returns>État final ou état de refus ; <c>Completed</c> ne signifie pas scan complet.</returns>
/// <remarks>Appel concurrent ou version obsolète : refus. Le service distribué
/// refuse toujours les bibliothèques personnelles (<c>REAL_LIBRARY_SCAN_HOLD</c>).
/// Seuls les tests internes peuvent autoriser des racines synthétiques bornées.
/// L'annulation peut laisser <c>LastAttempt</c> partiel sans remplacer <c>Current</c>.</remarks>
    Task<InventoryApplicationState> StartConfirmedScanAsync(
        InventoryRunRequest request, CancellationToken cancellationToken = default);
/// <summary>Demande au plus une fois l'annulation de la tentative active.</summary>
/// <returns><c>true</c> uniquement pour une nouvelle demande ; <c>false</c> sans scan actif ou si déjà demandée.</returns>
/// <remarks>Une demande tardive n'annule pas rétroactivement un résultat déjà terminé.</remarks>
    bool RequestCancellation();
/// <summary>Efface sélection et snapshots de l'état applicatif lorsqu'aucun scan n'est actif.</summary>
/// <returns><c>false</c> si le service est disposé ou qu'un scan est en cours.</returns>
    bool ClearSelection();
}

/// <summary>Implémentation fail-closed de l'orchestrateur, sans dépendance Avalonia dans ce service.</summary>
/// <remarks>L'instance publique ne possède aucune permission de scanner des ROM personnelles.
/// L'injection de permission est interne à l'assembly de test synthétique autorisée.
/// La lecture n'écrit, ne déplace et ne supprime aucun fichier source.</remarks>
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
/// <summary>Crée un orchestrateur sans permission de scan, y compris après consentement utilisateur.</summary>
    public InventoryApplicationService() : this(static _ => false) { }

    // Only the friend synthetic test assembly can grant per-scope permission.
    // There is deliberately no public runtime switch, env var or manifest-based bypass.
    internal InventoryApplicationService(Func<InventoryScope, bool> syntheticPermission) =>
        _syntheticPermission = syntheticPermission ??
            throw new ArgumentNullException(nameof(syntheticPermission));

/// <inheritdoc />
    public event EventHandler<InventoryApplicationState>? StateChanged;

/// <inheritdoc />
    public InventoryApplicationState State
    {
        get { lock (_gate) return _state; }
    }

/// <inheritdoc />
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

/// <inheritdoc />
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

/// <inheritdoc />
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

/// <inheritdoc />
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
