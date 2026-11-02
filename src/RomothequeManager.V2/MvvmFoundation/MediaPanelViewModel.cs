using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Evidence-only media presentation. No file I/O, bitmap decoding or automatic association.</summary>
/// <remarks>All scan IDs are transient. The default preview service refuses personal images.
/// This ViewModel never creates a UserDecision or persists an association.</remarks>
public sealed class MediaPanelViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly IMediaPresentationService _presentation;
    private IMediaPreviewService _preview;
    private readonly InventoryCommand _load;
    private readonly InventoryCommand _cancel;
    private MediaUiSnapshot? _evidence;
    private MediaIndexSnapshot? _media;
    private string? _romId;
    private IReadOnlyList<MediaChoice> _choices = Array.Empty<MediaChoice>();
    private MediaChoice? _selected;
    private MediaPreviewResult? _result;
    private CancellationTokenSource? _cancellation;
    private long _version;
    private bool _busy;
    private bool _disposed;
    private string _diagnostic = "Aucune source MEDIAS autorisée pour cette session.";

    /// <summary>Evidence-only local media presentation; it never associates covers automatically or grants new preview permissions.</summary>
    public MediaPanelViewModel(IMediaPresentationService presentation, IMediaPreviewService preview)
    {
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
        _preview = preview ?? throw new ArgumentNullException(nameof(preview));
        _load = new InventoryCommand(_ => _ = PreviewSelectedAsync(), _ => CanPreview);
        _cancel = new InventoryCommand(_ => ClearPreview(), _ => _busy || _result is not null);
    }

    /// <summary>Notifies preview, selection and provenance changes visible in the media panel.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Requests preview of the explicitly selected observed media through the injected backend service.</summary>
    public ICommand PreviewCommand => _load;
    /// <summary>Clears any current preview and invalidates or cancels in-flight decoding.</summary>
    public ICommand CancelCommand => _cancel;
    /// <summary>Candidate and unrelated gallery entries, each labelled by evidence status rather than approval.</summary>
    public IReadOnlyList<MediaChoice> Choices => _choices;
    /// <summary>Independent media observations without an established ROM relationship.</summary>
    public IReadOnlyList<UnlinkedMediaRow> Unlinked => _evidence?.UnlinkedMedias ?? Array.Empty<UnlinkedMediaRow>();
    /// <summary>Indicates a preview decode request is still in progress.</summary>
    public bool IsBusy => _busy;
    /// <summary>Requires an accepted selection and index, no current operation and a non-disposed panel; backend can still refuse.</summary>
    public bool CanPreview => !_disposed && !_busy && _media is not null && _selected is not null;
    /// <summary>Owned decoded preview result, if adopted; the VM disposes it on navigation, replacement or close.</summary>
    public MediaPreviewResult? PreviewResult => _result;
    /// <summary>Human-readable safe status distinguishing Ready, Stale, cancellation, refusal and unavailable media.</summary>
    public string PreviewStatus => _busy ? "Décodage en cours · annulation possible." :
        _result is null ? "Aucun aperçu demandé. Sélectionnez une image, puis demandez son aperçu." :
        _result.Status switch
        {
            MediaPreviewStatus.Ready => "Aperçu local décodé · association NON approuvée.",
            MediaPreviewStatus.Unsupported => "Format non pris en charge (WebP inclus).",
            MediaPreviewStatus.Rejected => "Aperçu refusé par les contrôles de sécurité.",
            MediaPreviewStatus.Unavailable => "Média indisponible.",
            MediaPreviewStatus.Cancelled => "Aperçu annulé.",
            MediaPreviewStatus.DecodeFailed => "Image illisible ou invalide.",
            MediaPreviewStatus.Stale => "Aperçu obsolète : vérifier la sélection.",
            _ => "Aucun aperçu disponible."
        };
    /// <summary>Safe preview or evidence diagnostic code, excluding local exception paths.</summary>
    public string Diagnostic => _result?.DiagnosticCode ?? _diagnostic;
    /// <summary>Labels candidate, referenced, ambiguous or not-comparable observations without approving any association.</summary>
    public string EvidenceStatus
    {
        get
        {
            var link = _evidence?.Rows.FirstOrDefault(r =>
                string.Equals(r.RomObservationId, _romId, StringComparison.Ordinal));
            if (link is null) return "Aucun rapprochement établi · fichier observé seulement.";
            return link.State switch
            {
                MediaUiState.CANDIDATE => "Correspondance possible · NON validée.",
                MediaUiState.REFERENCED => "Référence de chemin sourcée · association NON approuvée.",
                MediaUiState.AMBIGUOUS => "Plusieurs médias possibles · aucun choix automatique.",
                MediaUiState.ABSENT_IN_COMPLETE_SCOPE => "Aucun média correspondant observé dans les deux périmètres complets.",
                MediaUiState.NOT_COMPARABLE => "Comparaison indisponible : couverture, fraîcheur ou mapping non vérifié.",
                _ => "Image observée sans lien établi."
            };
        }
    }
    /// <summary>Shows relation rule, provenance, evidence and conflicts from the current projection.</summary>
    public string EvidenceDetails
    {
        get
        {
            var link = _evidence?.Rows.FirstOrDefault(r =>
                string.Equals(r.RomObservationId, _romId, StringComparison.Ordinal));
            if (link is null) return "Aucune preuve de relation.";
            return "Règle : " + link.RuleVersion +
                " · preuves : " + string.Join(", ", link.Evidence) +
                " · conflits : " + string.Join(", ", link.Conflicts) +
                " · provenance : " + string.Join(", ", link.SourceProvenance);
        }
    }

    /// <summary>Accepts only a choice from the current evidence set and disposes the previous preview.</summary>
    public MediaChoice? SelectedChoice
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            if (value is not null && !_choices.Any(x => ReferenceEquals(x, value))) return;
            _selected = value;
            ClearPreview();
            Notify();
            Notify(nameof(CanPreview));
            _load.NotifyCanExecuteChanged();
        }
    }

    /// <summary>Navigation invalidates a pending or already decoded preview without changing the ROM selection.</summary>
    public void ClearPreview()
    {
        ++_version;
        var old = _cancellation;
        _cancellation = null;
        old?.Cancel();
        old?.Dispose();
        var prior = _result;
        _result = null;
        prior?.Dispose();
        _busy = false;
        Notify(nameof(PreviewResult));
        Notify(nameof(PreviewStatus));
        Notify(nameof(Diagnostic));
        Notify(nameof(IsBusy));
        Notify(nameof(CanPreview));
        _load.NotifyCanExecuteChanged();
        _cancel.NotifyCanExecuteChanged();
    }

    /// <summary>Selection alone never loads or approves a cover.</summary>
    public void SelectRom(string? observationId)
    {
        if (_romId == observationId) return;
        _romId = observationId;
        RebuildChoices();
    }

    private void RebuildChoices()
    {
        ClearPreview();
        _selected = null;
        var row = _evidence?.Rows.FirstOrDefault(r =>
            string.Equals(r.RomObservationId, _romId, StringComparison.Ordinal));
        var available = _media?.Observations.ToDictionary(x => x.MediaObservationId,
            StringComparer.Ordinal) ?? new Dictionary<string, MediaObservation>(StringComparer.Ordinal);
        var chosen = new List<MediaChoice>();
        if (row is not null)
        {
            foreach (var id in row.CandidateMediaObservationIds)
                if (available.TryGetValue(id, out var observation))
                    chosen.Add(new MediaChoice(observation,
                        row.State == MediaUiState.REFERENCED
                            ? "Référence sourcée, NON approuvée"
                            : row.State == MediaUiState.AMBIGUOUS
                                ? "Option ambiguë, NON choisie"
                                : "Candidat proposé, NON approuvé"));
        }
        // Gallery entries are not attached to the selected ROM. Explicitly label them as such.
        foreach (var item in _evidence?.UnlinkedMedias ?? Array.Empty<UnlinkedMediaRow>())
            if (available.TryGetValue(item.MediaObservationId, out var observation))
                chosen.Add(new MediaChoice(observation, "Galerie indépendante · AUCUN lien avec ce fichier"));
        _choices = chosen.AsReadOnly();
        _diagnostic = _evidence is null
            ? "Aucune source MEDIAS autorisée pour cette session."
            : "Génération ROM : " + _evidence.RomScanId +
              " · génération MEDIA : " + _evidence.MediaRunId +
              " · racine MEDIA : " + _evidence.MediaRootRef;
        foreach (var name in new[] { nameof(Choices), nameof(Unlinked), nameof(SelectedChoice),
            nameof(EvidenceStatus), nameof(EvidenceDetails), nameof(Diagnostic), nameof(CanPreview) })
            Notify(name);
        _load.NotifyCanExecuteChanged();
    }

#if NEXT05_CI
    /// <summary>CI-only: exact synthetic snapshots and preview permission; absent from product executable.</summary>
    internal void AttachSynthetic(InventorySnapshot roms, MediaIndexSnapshot media,
        VerifiedScopeMapping mapping, IMediaPreviewService preview,
        IReadOnlyList<ExplicitMediaReference>? claims = null)
    {
        ClearPreview();
        _preview = preview;
        _evidence = _presentation.Build(roms, media, mapping, claims);
        _media = media;
        _romId = null;
        RebuildChoices();
    }
#endif

    /// <summary>Preview request uses only run/root/observation IDs. The backend owns access and decoding.</summary>
    public async Task PreviewSelectedAsync()
    {
        if (!CanPreview || _media is null || _selected is null) return;
        var id = _selected.Observation.MediaObservationId;
        var run = _media.RunId;
        var root = _media.MediaRootRef;
        ClearPreview();
        var version = ++_version;
        _busy = true;
        var cts = new CancellationTokenSource();
        _cancellation = cts;
        Notify(nameof(IsBusy));
        Notify(nameof(PreviewStatus));
        Notify(nameof(CanPreview));
        _load.NotifyCanExecuteChanged();
        _cancel.NotifyCanExecuteChanged();
        MediaPreviewResult? received = null;
        try
        {
            received = await _preview.LoadAsync(new MediaPreviewRequest(run, root, id, 512), cts.Token);
            if (_disposed || version != _version || !string.Equals(_selected?.Observation.MediaObservationId,
                id, StringComparison.Ordinal) || _media?.RunId != run)
                return;
            _result = received;
            received = null;
        }
        catch (OperationCanceledException)
        {
            // A navigation/selection change already invalidated this request.
        }
        catch (Exception)
        {
            if (!_disposed && version == _version)
                _diagnostic = "MEDIA_PREVIEW_ERROR";
        }
        finally
        {
            received?.Dispose();
            if (!_disposed && version == _version)
            {
                _busy = false;
                _cancellation = null;
                cts.Dispose();
                foreach (var name in new[] { nameof(PreviewResult), nameof(PreviewStatus),
                    nameof(Diagnostic), nameof(IsBusy), nameof(CanPreview) })
                    Notify(name);
                _load.NotifyCanExecuteChanged();
                _cancel.NotifyCanExecuteChanged();
            }
        }
    }

    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    /// <summary>Cancels outstanding previews, disposes the adopted result and clears retained media evidence.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClearPreview();
        _choices = Array.Empty<MediaChoice>();
        _media = null;
        _evidence = null;
    }
}

/// <summary>A single observed media file, not a selected or approved cover.</summary>
public sealed record MediaChoice(MediaObservation Observation, string Relationship)
{
    /// <summary>Displays relative media location, detected format and kind without opening the file.</summary>
    public string Caption => Observation.RelativePath + " · " +
        Observation.DetectedFormat + " · " + Observation.Kind;
    /// <summary>Explains why the media is a candidate or unlinked gallery entry; never proves an association.</summary>
    public string Provenance => Relationship + " · " + Observation.SourceKind;
}
