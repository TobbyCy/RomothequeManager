using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Non-composed candidate UI for scoped MEDIAS index and preview operations.
/// No source is selected, indexed or previewed while backend security review is pending.</summary>
internal sealed class IdealMediaDraftViewModel : INotifyPropertyChanged, IDisposable
{
    // Composition gate only: never an INDEX/PREVIEW grant. Defaults false until
    // the owner-authorized production composition passes explicit true.
    private readonly bool _productMediaUiEnabled;
    private readonly IdealMediaApplicationService _service;
    private readonly IMediaPresentationService _presentation;
    private readonly InventoryCommand _authorizeIndex;
    private readonly InventoryCommand _startIndex;
    private readonly InventoryCommand _authorizePreview;
    private readonly InventoryCommand _loadPreview;
    private readonly InventoryCommand _cancel;
    private readonly InventoryCommand _clear;
    private CancellationTokenSource? _pending;
    private MediaIndexSnapshot? _current;
    private MediaUiSnapshot? _projection;
    private IReadOnlyList<MediaUiRow> _referenced = Array.Empty<MediaUiRow>();
    private IdealMediaCoverProposal? _selectedCover;
    private IReadOnlyList<IdealMediaCoverProposal> _coverProposals =
        Array.Empty<IdealMediaCoverProposal>();
    private MediaPreviewResult? _preview;
    private MediaIndexAttemptDiagnosticView? _attemptDiagnostic;
    private bool _selectedRoot, _indexGrant, _previewGrant, _busy, _disposed;
    private long _revision;
    private string _status = "MEDIAS / PREVIEW : Draft inactif ; validation sécurité #161 requise.";

    /// <summary>Take ownership of an unopened native backend and a pure presentation
    /// service. Neither construction nor UI initialization grants filesystem access.</summary>
    internal IdealMediaDraftViewModel(IdealMediaApplicationService service,
        IMediaPresentationService? presentation = null, bool productMediaUiEnabled = false)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _productMediaUiEnabled = productMediaUiEnabled;
        _presentation = presentation ?? new MediaPresentationService();
        _authorizeIndex = new InventoryCommand(_ => AuthorizeIndex(), _ => CanAuthorizeIndex);
        _startIndex = new InventoryCommand(_ => _ = StartIndexAsync(), _ => CanStartIndex);
        _authorizePreview = new InventoryCommand(_ => AuthorizePreview(), _ => CanAuthorizePreview);
        _loadPreview = new InventoryCommand(_ => _ = LoadPreviewAsync(), _ => CanLoadPreview);
        _cancel = new InventoryCommand(_ => Cancel(), _ => !_disposed && _busy);
        _clear = new InventoryCommand(_ => ClearPreview(), _ => CanClearPreview);
        if (SecurityApproved) _status = "Choisir le dossier MEDIAS ; aucune autorisation accordée.";
    }

    /// <summary>Change notification for the prospective disabled GUI.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Explicit fail-closed UI feature flag; cannot be set by a user, CLI or persisted state.</summary>
    public bool SecurityApproved => _productMediaUiEnabled;
    /// <summary>Safe user-facing mode and refusal text, without absolute local paths.</summary>
    public string Status => _status;
    public bool CanClearPreview => SecurityApproved && !_disposed &&
        (_preview is not null || _indexGrant || _previewGrant || _current is not null || _busy);
    public string ActiveStep => !_selectedRoot ? "1 · Choisir" :
        _current is null ? "2 · Analyser" :
        _coverProposals.Count == 0 ? "3 · Proposer les jaquettes" : "4 · Consulter";
    public string NextAction => _busy ? "Opération en cours : vous pouvez l’annuler." :
        _pending is not null ? "Attendre la fin de l’annulation avant une nouvelle autorisation." :
        !_selectedRoot ? "Choisir MEDIAS pour activer l’autorisation INDEX." :
        _current is null ? (_indexGrant ? "Lancer l’analyse MEDIAS." : "Autoriser INDEX pour analyser MEDIAS.") :
        _coverProposals.Count == 0 ? "Confirmer les plateformes puis proposer les jaquettes ; un inventaire ROMS complet et actuel est requis." :
        _selectedCover is null ? "Sélectionner une candidate puis choisir cette image." :
        _preview is not null ? "Effacer l’aperçu pour retirer les pixels et révoquer les autorisations MEDIAS." :
        _previewGrant ? "Afficher l’aperçu autorisé." : "Autoriser PREVIEW pour cette image.";
    public string IndexAttemptText => _attemptDiagnostic?.Text ?? "Dernière tentative INDEX MEDIAS : aucune.";
    public string CompleteIndexText => "Dernier INDEX MEDIAS complet conservé : " +
        (_current is null ? "non" : "oui") + " (sélection courante).";
    /// <summary>True only when the future security gate explicitly permits a new root selection.</summary>
    public bool CanChooseMedia => SecurityApproved && !_disposed && !_busy && _pending is null;
    /// <summary>INDEX permission requires an independent selected MEDIAS root and user click.</summary>
    public bool CanAuthorizeIndex => CanChooseMedia && _selectedRoot && !_indexGrant;
    /// <summary>Indexing requires a one-shot INDEX grant; a ROMS grant is never consulted.</summary>
    public bool CanStartIndex => CanChooseMedia && _selectedRoot && _indexGrant;
    /// <summary>A completed MEDIAS index can be projected without treating paths as game identities.</summary>
    public IReadOnlyList<MediaUiRow> Evidence =>
        _projection?.Rows ?? Array.Empty<MediaUiRow>();
    /// <summary>Only explicitly sourced and unambiguous references are selectable for preview.</summary>
    public IReadOnlyList<MediaUiRow> ReferencedCovers => _referenced;
    /// <summary>XML-optional, unapproved local cover choices from bounded native observations.</summary>
    public IReadOnlyList<IdealMediaCoverProposal> CoverProposals => _coverProposals;
    /// <summary>Independent media observations not linked to any reference.</summary>
    public IReadOnlyList<UnlinkedMediaRow> Unlinked =>
        _projection?.UnlinkedMedias ?? Array.Empty<UnlinkedMediaRow>();
    /// <summary>Preview permission is separate from both ROMS and INDEX.</summary>
    public bool CanAuthorizePreview => CanChooseMedia && _current is not null &&
        _selectedCover is not null && !_previewGrant;
    /// <summary>Loading requires its own explicit one-shot PREVIEW grant.</summary>
    public bool CanLoadPreview => CanChooseMedia && _previewGrant && _selectedCover is not null;
    /// <summary>Current native index is evidence only, never an access grant.</summary>
    internal MediaIndexSnapshot? CurrentMediaIndex => _current;

    /// <summary>Owned bounded pixel buffer; cleared on cancellation, navigation and close.</summary>
    public MediaPreviewResult? Preview => _preview;
    /// <summary>Independent action for MEDIAS INDEX grant.</summary>
    public ICommand AuthorizeIndexCommand => _authorizeIndex;
    /// <summary>Start an index only after positive INDEX grant.</summary>
    public ICommand StartIndexCommand => _startIndex;
    /// <summary>Independent action for one selected PREVIEW grant.</summary>
    public ICommand AuthorizePreviewCommand => _authorizePreview;
    /// <summary>Load an approved preview; never uses a filesystem path.</summary>
    public ICommand LoadPreviewCommand => _loadPreview;
    /// <summary>Cancel active work and revoke both grants.</summary>
    public ICommand CancelCommand => _cancel;
    /// <summary>Dispose displayed pixels and revoke pending preview rights.</summary>
    public ICommand ClearCommand => _clear;

    /// <summary>Accept only the local path obtained from a dedicated MEDIAS picker.
    /// The backend validates scope and native identity; selection is never a grant.</summary>
    internal bool SelectMediaRoot(string selectedPath)
    {
        if (!CanChooseMedia || string.IsNullOrWhiteSpace(selectedPath)) return false;
        Cancel();
        var accepted = _service.PrepareMediaSelection(
            new MediaRootScope(selectedPath,
                IdealPlatformFolderConventions.PlatformAliases,
                IdealPlatformFolderConventions.FolderKinds), Guid.NewGuid().ToString("D"));
        _selectedRoot = accepted;
        _current = null;
        _attemptDiagnostic = null;
        _projection = null;
        _referenced = Array.Empty<MediaUiRow>();
        _coverProposals = Array.Empty<IdealMediaCoverProposal>();
        _selectedCover = null;
        _status = accepted ? "MEDIAS sélectionné. Autoriser INDEX séparément." :
            "Racine MEDIAS refusée ou indisponible.";
        NotifyAll();
        return accepted;
    }

    /// <summary>An affirmative INDEX click never provides PREVIEW authority.</summary>
    internal bool AuthorizeIndex()
    {
        if (!CanAuthorizeIndex) return false;
        _indexGrant = _service.AuthorizeMediaIndex(explicitUserAction: true);
        _status = _indexGrant ? "INDEX autorisé une seule fois ; lancer séparément." :
            "Autorisation INDEX refusée.";
        NotifyAll();
        return _indexGrant;
    }

    /// <summary>Run and adopt only the complete Current generation; failures remain
    /// visible in LastAttempt without removing the prior complete evidence.</summary>
    internal async Task StartIndexAsync()
    {
        if (!CanStartIndex) return;
        _indexGrant = false;
        _previewGrant = false;
        _selectedCover = null;
        ReplacePreview(null);
        var revision = ++_revision;
        using var cancellation = new CancellationTokenSource();
        _pending = cancellation;
        _busy = true;
        _status = "INDEX MEDIAS en cours · lecture native bornée.";
        NotifyAll();
        try
        {
            var attempt = await _service.StartIndexAsync(cancellation.Token);
            if (_disposed || revision != _revision) return;
            _current = _service.Current;
            _attemptDiagnostic = MediaIndexAttemptDiagnosticView.From(attempt, _current);
            _projection = null; // Never carry links across INDEX generations.
            _referenced = Array.Empty<MediaUiRow>();
        _coverProposals = Array.Empty<IdealMediaCoverProposal>();
            _status = attempt.ScanStatus == InventoryScanStatus.Complete &&
                attempt.Availability == InventoryAvailability.Online
                ? "INDEX terminé · aucune jaquette attribuée automatiquement."
                : _attemptDiagnostic.Explanation;
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && revision == _revision) _status = "INDEX annulé.";
        }
        catch (Exception)
        {
            if (!_disposed && revision == _revision) _status = "INDEX indisponible.";
        }
        finally
        {
            if (ReferenceEquals(_pending, cancellation)) _pending = null;
            if (revision == _revision) _busy = false;
            if (!_disposed) NotifyAll();
        }
    }

    /// <summary>Pure projection: caller supplies independently verified root mapping,
    /// ROM evidence and an explicitly imported XML snapshot. It performs no I/O.</summary>
    internal void AttachExplicitEsDeEvidence(InventorySnapshot roms,
        EsDeCatalogueSnapshot? imported, VerifiedScopeMapping mapping)
    {
        if (!SecurityApproved || _disposed || _busy || _current is null)
            return;
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(mapping);
        if (_current.ScanStatus != InventoryScanStatus.Complete ||
            _current.Availability != InventoryAvailability.Online ||
            _current.Freshness != InventoryFreshness.Fresh ||
            roms.ScanStatus != InventoryScanStatus.Complete ||
            roms.Availability != InventoryAvailability.Online ||
            roms.Freshness != InventoryFreshness.Fresh ||
            mapping.RomScanId != roms.ScanId ||
            mapping.MediaScanId != _current.RunId ||
            mapping.MediaRootRef != _current.MediaRootRef)
        {
            _projection = null;
            _referenced = Array.Empty<MediaUiRow>();
            _coverProposals = Array.Empty<IdealMediaCoverProposal>();
            _selectedCover = null;
            ReplacePreview(null);
            _status = "Hors scope / OFFLINE / STALE : jaquettes non comparables.";
            NotifyAll();
            return;
        }
        var claims = EsDeDeclaredMediaClaims.Build(roms.Observations, imported);
        _projection = _presentation.Build(roms, _current, mapping, claims);
        _referenced = Array.AsReadOnly(_projection.Rows.Where(row =>
            row.State == MediaUiState.REFERENCED &&
            row.CandidateMediaObservationIds.Count == 1).ToArray());
        _coverProposals = IdealMediaCoverProposals.Build(roms, _current, _projection);
        _selectedCover = null;
        ReplacePreview(null);
        _status = "Jaquettes locales candidates : " + _coverProposals.Count +
            " · XML facultatif · aucune association approuvée.";
        NotifyAll();
    }

    /// <summary>Explicit user selection of a unique native observation. This
    /// grants neither PREVIEW nor permission to persist a cover association.</summary>
    internal bool SelectCoverForPreview(IdealMediaCoverProposal choice)
    {
        if (!SecurityApproved || _disposed || _busy || _pending is not null ||
            _projection is null || _current is null ||
            !_coverProposals.Contains(choice) ||
            choice.RomScanId != _projection.RomScanId ||
            choice.MediaRunId != _current.RunId ||
            choice.MediaRootRef != _current.MediaRootRef)
            return false;
        if (_previewGrant)
        {
            // A consumed or granted preview cannot be silently retargeted.
            Cancel();
            return false;
        }
        ++_revision;
        ReplacePreview(null);
        _selectedCover = choice;
        _status = "Candidate " + choice.State +
            " sélectionnée · autoriser PREVIEW séparément.";
        NotifyAll();
        return true;
    }

    private MediaPreviewRequest? SelectedRequest()
    {
        if (_current is null || _selectedCover is null) return null;
        var id = _selectedCover.MediaObservationId;
        if (id is null || !_current.Observations.Any(x =>
            x.MediaObservationId == id && x.HeaderValidated &&
            x.Kind == MediaKind.Cover)) return null;
        return new MediaPreviewRequest(_current.RunId, _current.MediaRootRef, id, 512);
    }

    /// <summary>One positive PREVIEW click for exactly the selected observation.</summary>
    internal bool AuthorizePreview()
    {
        if (!CanAuthorizePreview) return false;
        var request = SelectedRequest();
        _previewGrant = request is not null &&
            _service.AuthorizeMediaPreview(request, explicitUserAction: true);
        _status = _previewGrant ? "PREVIEW autorisé pour cette image uniquement." :
            "PREVIEW refusé : absent, corrompu, périmètre ou version non valable.";
        NotifyAll();
        return _previewGrant;
    }

    /// <summary>Native preview is adopted only for the same generation and row.
    /// A late or rejected image is immediately disposed, never shown or saved.</summary>
    internal async Task LoadPreviewAsync()
    {
        if (!CanLoadPreview) return;
        var request = SelectedRequest();
        if (request is null) return;
        _previewGrant = false;
        ReplacePreview(null);
        var revision = ++_revision;
        using var cancellation = new CancellationTokenSource();
        _pending = cancellation;
        _busy = true;
        _status = "PREVIEW en cours · aucune autre autorisation déduite.";
        NotifyAll();
        MediaPreviewResult? result = null;
        try
        {
            result = await _service.LoadPreviewAsync(request, cancellation.Token);
            if (_disposed || revision != _revision || _current?.RunId != request.MediaRunId ||
                _selectedCover?.MediaObservationId != request.MediaObservationId) return;
            ReplacePreview(result);
            _status = result.Status switch
            {
                MediaPreviewStatus.Ready => "Aperçu décodé en mémoire · jaquette non approuvée.",
                MediaPreviewStatus.DecodeFailed => "Image corrompue ou illisible.",
                MediaPreviewStatus.Unsupported => "Format indisponible.",
                MediaPreviewStatus.Stale => "Aperçu périmé (STALE).",
                MediaPreviewStatus.Cancelled => "Aperçu annulé.",
                MediaPreviewStatus.Unavailable => "Fichier absent ou inaccessible.",
                _ => "PREVIEW refusé / hors scope."
            };
            result = null;
        }
        catch (OperationCanceledException)
        {
            if (!_disposed && revision == _revision) _status = "Aperçu annulé.";
        }
        catch (Exception)
        {
            if (!_disposed && revision == _revision) _status = "PREVIEW indisponible.";
        }
        finally
        {
            result?.Dispose();
            if (ReferenceEquals(_pending, cancellation)) _pending = null;
            if (revision == _revision) _busy = false;
            if (!_disposed) NotifyAll();
        }
    }

    /// <summary>Invalidate pending async adoption and remove any decoded pixel data.</summary>
    internal void Cancel()
    {
        if (_disposed) return;
        ++_revision;
        _pending?.Cancel();
        _service.RequestCancellation();
        _indexGrant = false;
        _previewGrant = false;
        _busy = false;
        // RequestCancellation invalidates the backend's verified Current.
        // Never offer a stale observation for a new PREVIEW grant.
        _current = null;
        _projection = null;
        _referenced = Array.Empty<MediaUiRow>();
        _coverProposals = Array.Empty<IdealMediaCoverProposal>();
        _selectedCover = null;
        ReplacePreview(null);
        _status = "Annulé · permissions INDEX/PREVIEW révoquées ; nouvel INDEX requis.";
        NotifyAll();
    }

    internal void ClearPreview()
    {
        if (!CanClearPreview) return;
        Cancel();
        _status = "Aperçu effacé · pixels libérés et autorisations MEDIAS révoquées. Autoriser un nouvel INDEX pour continuer.";
        NotifyAll();
    }

    private void ReplacePreview(MediaPreviewResult? next)
    {
        _preview?.Dispose();
        _preview = next;
    }

    private void NotifyAll([CallerMemberName] string? caller = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(caller));
        foreach (var name in new[] { nameof(Status), nameof(CanChooseMedia),
            nameof(CanClearPreview), nameof(ActiveStep), nameof(NextAction),
            nameof(IndexAttemptText), nameof(CompleteIndexText),
            nameof(CanAuthorizeIndex), nameof(CanStartIndex), nameof(CanAuthorizePreview),
            nameof(CanLoadPreview), nameof(Evidence), nameof(ReferencedCovers),
            nameof(CoverProposals), nameof(Unlinked), nameof(Preview) })
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        _authorizeIndex.NotifyCanExecuteChanged();
        _startIndex.NotifyCanExecuteChanged();
        _authorizePreview.NotifyCanExecuteChanged();
        _loadPreview.NotifyCanExecuteChanged();
        _cancel.NotifyCanExecuteChanged();
        _clear.NotifyCanExecuteChanged();
    }

    /// <summary>Cancel native work, revoke grants, clear pixels, and close the
    /// owned backend without changing disk content or any persisted permissions.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        Cancel();
        _disposed = true;
        _service.Dispose();
        _projection = null;
        _referenced = Array.Empty<MediaUiRow>();
        _coverProposals = Array.Empty<IdealMediaCoverProposal>();
        _current = null;
        _selectedCover = null;
        ReplacePreview(null);
    }
}
