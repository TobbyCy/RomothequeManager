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
    // Deliberately compile-time closed. Review of #161 alone does not flip this gate:
    // a later owner-approved GUI integration must explicitly enable the complete path.
    private const bool NativeMediaSecurityValidated = false;
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
    private MediaUiRow? _selectedReference;
    private MediaPreviewResult? _preview;
    private bool _selectedRoot, _indexGrant, _previewGrant, _busy, _disposed;
    private long _revision;
    private string _status = "MEDIAS / PREVIEW : Draft inactif ; validation sécurité #161 requise.";

    /// <summary>Take ownership of an unopened native backend and a pure presentation
    /// service. Neither construction nor UI initialization grants filesystem access.</summary>
    internal IdealMediaDraftViewModel(IdealMediaApplicationService service,
        IMediaPresentationService? presentation = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _presentation = presentation ?? new MediaPresentationService();
        _authorizeIndex = new InventoryCommand(_ => AuthorizeIndex(), _ => CanAuthorizeIndex);
        _startIndex = new InventoryCommand(_ => _ = StartIndexAsync(), _ => CanStartIndex);
        _authorizePreview = new InventoryCommand(_ => AuthorizePreview(), _ => CanAuthorizePreview);
        _loadPreview = new InventoryCommand(_ => _ = LoadPreviewAsync(), _ => CanLoadPreview);
        _cancel = new InventoryCommand(_ => Cancel(), _ => !_disposed && _busy);
        _clear = new InventoryCommand(_ => Cancel(), _ => !_disposed && _preview is not null);
    }

    /// <summary>Change notification for the prospective disabled GUI.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Explicit fail-closed UI feature flag; cannot be set by a user, CLI or persisted state.</summary>
    public bool SecurityApproved => NativeMediaSecurityValidated;
    /// <summary>Safe user-facing mode and refusal text, without absolute local paths.</summary>
    public string Status => _status;
    /// <summary>True only when the future security gate explicitly permits a new root selection.</summary>
    public bool CanChooseMedia => NativeMediaSecurityValidated && !_disposed && !_busy;
    /// <summary>INDEX permission requires an independent selected MEDIAS root and user click.</summary>
    public bool CanAuthorizeIndex => CanChooseMedia && _selectedRoot && !_indexGrant;
    /// <summary>Indexing requires a one-shot INDEX grant; a ROMS grant is never consulted.</summary>
    public bool CanStartIndex => CanChooseMedia && _selectedRoot && _indexGrant;
    /// <summary>A completed MEDIAS index can be projected without treating paths as game identities.</summary>
    public IReadOnlyList<MediaUiRow> Evidence =>
        _projection?.Rows ?? Array.Empty<MediaUiRow>();
    /// <summary>Only explicitly sourced and unambiguous references are selectable for preview.</summary>
    public IReadOnlyList<MediaUiRow> ReferencedCovers =>
        _projection?.Rows.Where(row => row.State == MediaUiState.REFERENCED &&
            row.CandidateMediaObservationIds.Count == 1).ToArray()
        ?? Array.Empty<MediaUiRow>();
    /// <summary>Independent media observations not linked to any reference.</summary>
    public IReadOnlyList<UnlinkedMediaRow> Unlinked =>
        _projection?.UnlinkedMedias ?? Array.Empty<UnlinkedMediaRow>();
    /// <summary>Preview permission is separate from both ROMS and INDEX.</summary>
    public bool CanAuthorizePreview => CanChooseMedia && _current is not null &&
        _selectedReference is not null && !_previewGrant;
    /// <summary>Loading requires its own explicit one-shot PREVIEW grant.</summary>
    public bool CanLoadPreview => CanChooseMedia && _previewGrant && _selectedReference is not null;
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
            new MediaRootScope(selectedPath), Guid.NewGuid().ToString("D"));
        _selectedRoot = accepted;
        _current = null;
        _projection = null;
        _selectedReference = null;
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
        _selectedReference = null;
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
            _projection = null; // Never carry links across INDEX generations.
            _status = attempt.ScanStatus == InventoryScanStatus.Complete &&
                attempt.Availability == InventoryAvailability.Online
                ? "INDEX terminé · aucune jaquette attribuée automatiquement."
                : "INDEX partiel/refusé · état antérieur conservé, absent non prouvé.";
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
            if (revision == _revision)
            {
                _pending = null;
                _busy = false;
                if (!_disposed) NotifyAll();
            }
        }
    }

    /// <summary>Pure projection: caller supplies independently verified root mapping,
    /// ROM evidence and an explicitly imported XML snapshot. It performs no I/O.</summary>
    internal void AttachExplicitEsDeEvidence(InventorySnapshot roms,
        EsDeCatalogueSnapshot? imported, VerifiedScopeMapping mapping)
    {
        if (!NativeMediaSecurityValidated || _disposed || _busy || _current is null)
            return;
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(mapping);
        if (_current.ScanStatus != InventoryScanStatus.Complete ||
            _current.Availability != InventoryAvailability.Online ||
            roms.ScanStatus != InventoryScanStatus.Complete ||
            roms.Availability != InventoryAvailability.Online ||
            mapping.RomScanId != roms.ScanId ||
            mapping.MediaScanId != _current.RunId ||
            mapping.MediaRootRef != _current.MediaRootRef)
        {
            _projection = null;
            _status = "Hors scope / OFFLINE / STALE : jaquettes non comparables.";
            NotifyAll();
            return;
        }
        var claims = EsDeDeclaredMediaClaims.Build(roms.Observations, imported);
        _projection = _presentation.Build(roms, _current, mapping, claims);
        _status = "Références ES-DE sourcées ; aucune association approuvée.";
        NotifyAll();
    }

    /// <summary>Only a single observed media ID with explicit ES-DE provenance
    /// may become an independent PREVIEW request. Never selects by title.</summary>
    internal bool SelectReferencedCover(MediaUiRow row)
    {
        if (!NativeMediaSecurityValidated || _disposed || _busy ||
            _projection is null || _current is null ||
            !_projection.Rows.Contains(row) ||
            row.State != MediaUiState.REFERENCED ||
            row.CandidateMediaObservationIds.Count != 1) return false;
        Cancel();
        _selectedReference = row;
        _status = "Jaquette ES-DE déclarée sélectionnée ; autoriser PREVIEW séparément.";
        NotifyAll();
        return true;
    }

    private MediaPreviewRequest? SelectedRequest()
    {
        if (_current is null || _selectedReference is null) return null;
        var id = _selectedReference.CandidateMediaObservationIds.SingleOrDefault();
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
                _selectedReference?.CandidateMediaObservationIds.SingleOrDefault() !=
                    request.MediaObservationId) return;
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
            if (revision == _revision)
            {
                _pending = null;
                _busy = false;
                if (!_disposed) NotifyAll();
            }
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
        ReplacePreview(null);
        _status = "Annulé · permissions INDEX/PREVIEW révoquées.";
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
            nameof(CanAuthorizeIndex), nameof(CanStartIndex), nameof(CanAuthorizePreview),
            nameof(CanLoadPreview), nameof(Evidence), nameof(ReferencedCovers),
            nameof(Unlinked), nameof(Preview) })
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
        _current = null;
        _selectedReference = null;
        ReplacePreview(null);
    }
}
