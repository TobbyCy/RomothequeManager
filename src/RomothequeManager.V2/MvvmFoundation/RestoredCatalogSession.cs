using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Persistence;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Read-only historical catalogue projection and explicit manual cover decisions.
/// Never scans, opens an image, imports XML, or binds a physical drive.</summary>
/// <remarks>Real catalogue writes remain denied. Only an internal, explicitly fixture-scoped
/// composition may grant synthetic decision tests. Run-scoped observation IDs are never
/// persisted as decision identities.</remarks>
public sealed class RestoredCatalogSession : INotifyPropertyChanged, IDisposable
{
    private readonly ICatalogStore _store;
    private readonly bool _fixtureWriteAllowed;
    private readonly string _romRootId;
    private readonly string _mediaRootId;
    private readonly InventoryCommand _saveCommand;
    private readonly InventoryCommand _showCompleteCommand;
    private readonly InventoryCommand _showAttemptCommand;
    private readonly InventoryCommand _revokeCommand;
    private CatalogLastKnown _roms;
    private CatalogLastKnown _medias;
    private FileObservation? _selectedRom;
    private SavedCoverCandidate? _selectedCover;
    private IReadOnlyList<SavedCoverCandidate> _candidates = Array.Empty<SavedCoverCandidate>();
    private bool _showAttempt;
    private bool _confirmation;
    private bool _disposed;
    private string _saveStatus = "Aucune modification demandée.";

    /// <summary>Creates a historical catalogue presentation with manual decision writes disabled.</summary>
    /// <remarks>This public overload selects fixtureWriteAllowed=false; it never grants live ROMS/MEDIAS access, previews, or personal catalogue writes.</remarks>
    /// <param name="store">Injected catalogue store, whose lifetime is owned by this session.</param>
    /// <param name="romRootId">Identifier of the saved ROMS root, checked against its stored role and library.</param>
    /// <param name="mediaRootId">Identifier of the saved MEDIAS root, required to share the ROMS library identity.</param>
    public RestoredCatalogSession(ICatalogStore store, string romRootId, string mediaRootId)
        : this(store, romRootId, mediaRootId, fixtureWriteAllowed: false) { }

    // Permission for disposable, synthetic test DBs only. Not exposed to normal application.
    /// <summary>Creates a historical catalogue session for controlled synthetic fixtures, with explicitly bounded optional manual-decision writes.</summary>
    /// <remarks>When fixtureWriteAllowed is true, Save and Revoke can write only to the fixture store after explicit confirmation, valid selection and complete saved ROMS/MEDIAS evidence; CanSave and CanRevoke enforce those conditions. Both root roles and their shared LibraryId are validated. This switch does not grant personal catalogue writes, live ROMS/MEDIAS reads, image decoding or preview permission.</remarks>
    /// <param name="store">Injected fixture catalogue store, owned and disposed by the session.</param>
    /// <param name="romRootId">Stored ROMS root identifier; its role and library are validated during construction.</param>
    /// <param name="mediaRootId">Stored MEDIAS root identifier; must belong to the same validated library.</param>
    /// <param name="fixtureWriteAllowed">True only for a controlled disposable synthetic fixture to permit explicitly confirmed manual decisions; false makes Save/Revoke unavailable.</param>
    internal RestoredCatalogSession(ICatalogStore store, string romRootId, string mediaRootId,
        bool fixtureWriteAllowed)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _romRootId = romRootId;
        _mediaRootId = mediaRootId;
        _fixtureWriteAllowed = fixtureWriteAllowed;
        _roms = _store.LoadLastKnown(_romRootId);
        _medias = _store.LoadLastKnown(_mediaRootId);
        if (_roms.StorageRoot.Role != "ROMS" ||
            _medias.StorageRoot.Role != "MEDIAS" ||
            !string.Equals(_roms.StorageRoot.LibraryId, _medias.StorageRoot.LibraryId,
                StringComparison.Ordinal))
            throw new InvalidDataException("UI05_ROOT_ROLE_OR_LIBRARY_MISMATCH");
        _showCompleteCommand = new InventoryCommand(_ => ShowComplete(), _ => true);
        _showAttemptCommand = new InventoryCommand(_ => ShowAttempt(), _ => HasLastAttempt);
        _saveCommand = new InventoryCommand(_ => SaveExplicitChoice(), _ => CanSave);
        _revokeCommand = new InventoryCommand(_ => RevokeExplicitChoice(), _ => CanRevoke);
    }

    /// <summary>Notifies presentation changes after a historical selection or explicitly permitted fixture decision.</summary>
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Stable library identity shared by the saved ROMS and MEDIAS roots, validated at construction.</summary>
    public string LibraryId => _roms.StorageRoot.LibraryId;
    /// <summary>Indicates a recorded attempt exists without asserting that its coverage is complete.</summary>
    public bool HasLastAttempt => _roms.LastAttempt is not null;
    /// <summary>Indicates that the user selected the last historical attempt instead of CurrentComplete.</summary>
    public bool ShowingAttempt => _showAttempt && HasLastAttempt;
    /// <summary>Restored captured inventory only, selected between CurrentComplete and LastAttempt; no live drive check.</summary>
    public InventorySnapshot? DisplayedInventory => (ShowingAttempt
        ? _roms.LastAttempt : _roms.CurrentComplete ?? _roms.LastAttempt)?.Inventory;
    /// <summary>Displays historical capture time and OFFLINE/STALE status without asserting physical availability.</summary>
    public string StatusLabel => DisplayedInventory is null
        ? "Collection sauvegardée indisponible · OFFLINE / STALE · présence non vérifiée."
        : (ShowingAttempt ? "Dernière tentative" : "Dernier inventaire complet") +
          " enregistré le " + (ShowingAttempt
              ? _roms.LastAttempt!.CapturedAtUtc
              : (_roms.CurrentComplete ?? _roms.LastAttempt)!.CapturedAtUtc).ToString("u") +
          " · OFFLINE / STALE · présence physique non vérifiée.";
    /// <summary>Displays the status and timestamp of the last persisted attempt.</summary>
    public string AttemptLabel => _roms.LastAttempt is null
        ? "Aucune tentative enregistrée."
        : "Dernière tentative : " + _roms.LastAttempt.ScanStatus + " · " +
          _roms.LastAttempt.CapturedAtUtc.ToString("u") + " · historique seulement.";
    /// <summary>Unique validated-format historical cover candidates for the selected ROM observation; no file is opened.</summary>
    public IReadOnlyList<SavedCoverCandidate> Candidates => _candidates;
    /// <summary>Selected historical candidate must belong to Candidates; changing it revokes pending confirmation.</summary>
    public SavedCoverCandidate? SelectedCandidate
    {
        get => _selectedCover;
        set
        {
            if (ReferenceEquals(_selectedCover, value)) return;
            if (value is not null && !_candidates.Contains(value)) return;
            _selectedCover = value;
            _confirmation = false;
            Notify(nameof(SelectedCandidate));
            Notify(nameof(Confirmation));
            CommandsChanged();
        }
    }
    /// <summary>Explicit one-shot confirmation required for fixture-scoped save or revoke commands.</summary>
    public bool Confirmation
    {
        get => _confirmation;
        set
        {
            if (_confirmation == value) return;
            _confirmation = value;
            Notify();
            CommandsChanged();
        }
    }
    /// <summary>Shows the persisted CurrentComplete inventory when available.</summary>
    public ICommand ShowCompleteCommand => _showCompleteCommand;
    /// <summary>Shows a persisted attempt, including partial or failed evidence, when available.</summary>
    public ICommand ShowAttemptCommand => _showAttemptCommand;
    /// <summary>Attempts an explicitly confirmed manual cover decision only within synthetic fixture write scope.</summary>
    public ICommand SaveCommand => _saveCommand;
    /// <summary>Revokes an explicitly confirmed fixture-scoped choice while retaining decision history.</summary>
    public ICommand RevokeCommand => _revokeCommand;
    /// <summary>Neutral result of the last fixture-only manual decision request; failures are not reported as success.</summary>
    public string SaveStatus => _saveStatus;
    /// <summary>Requires fixture-write authorization, confirmation, a valid selected ROM and cover, and complete saved ROMS and MEDIAS evidence.</summary>
    public bool CanSave => !_disposed && _fixtureWriteAllowed && _confirmation &&
        _selectedRom is not null && _selectedCover is not null &&
        DisplayedInventory is { ScanStatus: InventoryScanStatus.Complete } &&
        _roms.CurrentComplete?.Inventory is not null &&
        _medias.CurrentComplete?.Media is not null;
    /// <summary>Requires fixture-write authorization, confirmation and an active historical decision.</summary>
    public bool CanRevoke => !_disposed && _fixtureWriteAllowed && _confirmation &&
        ActiveDecision() is not null;
    /// <summary>Labels the saved manual choice as historical and media-unverified; never claims a live preview.</summary>
    public string CurrentChoiceLabel
    {
        get
        {
            var current = ActiveDecision();
            if (current is null) return "Aucune jaquette associée manuellement.";
            var cover = _candidates.FirstOrDefault(x =>
                string.Equals(x.DurableKey, current.Value, StringComparison.Ordinal));
            return cover is null
                ? "Association sauvegardée · média non vérifié / source indisponible."
                : "Jaquette choisie (historique) : " + cover.Label +
                  " · OFFLINE, aperçu non autorisé.";
        }
    }
    /// <summary>Reports internal fixture write capability, which is false for normal product composition.</summary>
    public bool FixtureWriteAllowed => _fixtureWriteAllowed;

    /// <summary>Switches to the last complete historical inventory without accessing ROMS or MEDIAS.</summary>
    public void ShowComplete() => SwitchAttempt(false);
    /// <summary>Switches to the last saved attempt without promoting it to a complete inventory.</summary>
    public void ShowAttempt() => SwitchAttempt(true);

    private void SwitchAttempt(bool value)
    {
        if (_disposed || (value && !HasLastAttempt)) return;
        _showAttempt = value;
        SelectRom(null);
        Notify(nameof(ShowingAttempt));
        Notify(nameof(DisplayedInventory));
        Notify(nameof(StatusLabel));
        Notify(nameof(AttemptLabel));
    }

    /// <summary>Builds bounded historical cover candidates using validated index entries for the selected ROM; no image is opened.</summary>
    public void SelectRom(FileObservation? rom)
    {
        if (_disposed) return;
        _selectedRom = rom;
        _selectedCover = null;
        _confirmation = false;
        // These are SAVED historical observations, never live media, and never
        // used to open a physical file or preview an image on restoration.
        _candidates = rom is null ? Array.Empty<SavedCoverCandidate>() :
            (_medias.CurrentComplete?.Media?.Observations ?? Array.Empty<MediaObservation>())
            .Where(media => media.Kind == MediaKind.Cover && media.HeaderValidated &&
                media.DetectedFormat is MediaImageFormat.Png or MediaImageFormat.Jpeg &&
                string.Equals(media.PlatformKey, rom.PlatformKey, StringComparison.Ordinal))
            .GroupBy(media => media.RelativePath, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .Select(group => new SavedCoverCandidate(group.Key,
                group.Single().DetectedFormat.ToString(),
                DurableKey("media-v1", _mediaRootId, "", group.Key)))
            .OrderBy(x => x.Label, StringComparer.Ordinal)
            .ToArray();
        Notify(nameof(Candidates));
        Notify(nameof(SelectedCandidate));
        Notify(nameof(Confirmation));
        Notify(nameof(CurrentChoiceLabel));
        CommandsChanged();
    }

    private CatalogUserDecision? ActiveDecision()
    {
        if (_selectedRom is null) return null;
        var key = SubjectKey(_selectedRom);
        var all = _roms.Decisions.Where(d =>
            d.Action == "MANUAL_COVER_CHOICE" &&
            string.Equals(d.SubjectKey, key, StringComparison.Ordinal)).ToArray();
        var superseded = all.Where(d => d.SupersedesId is not null)
            .Select(d => d.SupersedesId!).ToHashSet(StringComparer.Ordinal);
        var active = all.Where(d => d.RevokedAtUtc is null &&
            !superseded.Contains(d.DecisionId)).ToArray();
        return active.Length == 1 ? active[0] : null;
    }

    private string SubjectKey(FileObservation rom) =>
        DurableKey("rom-v1", _romRootId, rom.PlatformKey, rom.RelativePath);

    // Hash the EXACT root-scoped platform/path bytes; no casefolding, auto-rebind,
    // title inference, SSD letter or transient per-scan identifier.
    private static string DurableKey(string kind, string rootId, string platform, string relative)
    {
        var material = Encoding.UTF8.GetBytes(kind + "\0" + rootId + "\0" +
            platform + "\0" + relative);
        return kind + ":" + rootId + ":" +
            Convert.ToHexString(SHA256.HashData(material)).ToLowerInvariant();
    }

    private void Reload()
    {
        _roms = _store.LoadLastKnown(_romRootId);
        _medias = _store.LoadLastKnown(_mediaRootId);
        Notify(nameof(CurrentChoiceLabel));
        CommandsChanged();
    }

    private void SaveExplicitChoice()
    {
        if (!CanSave || _selectedRom is null || _selectedCover is null) return;
        try
        {
            var current = ActiveDecision();
            if (current?.Value == _selectedCover.DurableKey)
                _saveStatus = "Association déjà enregistrée ; aucun doublon créé.";
            else
            {
                var decision = new CatalogUserDecision(Guid.NewGuid().ToString("D"),
                    LibraryId, SubjectKey(_selectedRom), "MANUAL_COVER_CHOICE",
                    _selectedCover.DurableKey, DateTimeOffset.UtcNow, current?.DecisionId);
                _store.SaveManualDecision(decision, explicitUserAction: true);
                Reload(); // Never report success before a committed, readable decision.
                _saveStatus = "Association enregistrée dans SQLite (fixture uniquement).";
            }
        }
        catch (Exception)
        {
            _saveStatus = "PERSIST_UI_SAVE_FAILED : association non confirmée.";
        }
        finally
        {
            _confirmation = false;
            Notify(nameof(Confirmation));
            Notify(nameof(SaveStatus));
            CommandsChanged();
        }
    }

    private void RevokeExplicitChoice()
    {
        var current = ActiveDecision();
        if (!CanRevoke || current is null) return;
        try
        {
            _store.RevokeManualDecision(current.DecisionId, explicitUserAction: true);
            Reload();
            _saveStatus = "Association révoquée ; historique conservé.";
        }
        catch (Exception)
        {
            _saveStatus = "PERSIST_UI_REVOKE_FAILED : modification non confirmée.";
        }
        finally
        {
            _confirmation = false;
            Notify(nameof(Confirmation));
            Notify(nameof(SaveStatus));
            CommandsChanged();
        }
    }

    private void CommandsChanged()
    {
        Notify(nameof(CanSave));
        Notify(nameof(CanRevoke));
        _saveCommand.NotifyCanExecuteChanged();
        _revokeCommand.NotifyCanExecuteChanged();
    }
    private void Notify([CallerMemberName] string? property = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));

    /// <summary>Closes the session-owned catalogue store and disables its commands; it does not touch live roots.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _store.Dispose();
        CommandsChanged();
    }
}

/// <summary>Saved media-index description, not an image/thumbnail or live filesystem handle.</summary>
public sealed record SavedCoverCandidate(string Label, string Format, string DurableKey)
{
    /// <summary>Readable label for one saved cover candidate; the association remains historical and unverified.</summary>
    public string Caption => Label + " · " + Format + " · observation historique";
}
