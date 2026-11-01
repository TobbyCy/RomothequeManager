namespace RomothequeManager.V2;

// UI-only selection, search, paging and mode. The ES-DE importer remains the sole
// authority for XML parsing, media paths and read-only file access.
public enum CatalogueMode { NoCatalog, Demonstration, Imported }

// A system is proven only by games referenced in imported ES-DE XML.
public sealed record ImportedSystem(string Platform, int ReferencedGames);

public sealed class ImportedCatalogueUiState
{
    private EsDeImportSession? _session;
    private EsDeCatalogueSnapshot? _acceptedSnapshot;
    private readonly IEsDeImportApplicationService _applicationService;

    /// <summary>Presentation state; import I/O is delegated to an application service.</summary>
    public ImportedCatalogueUiState() : this(new EsDeImportApplicationService()) { }

    /// <summary>Use an injected read-only importer for testable, explicit operations.</summary>
    public ImportedCatalogueUiState(IEsDeImportApplicationService applicationService) =>
        _applicationService = applicationService ?? throw new ArgumentNullException(nameof(applicationService));

    internal IEsDeImportApplicationService ApplicationService => _applicationService;
    internal EsDeImportSession? Session => _session;

    public CatalogueMode Mode { get; private set; } = CatalogueMode.NoCatalog;
    public bool HasImportedCatalogue => Snapshot is not null;
    // An asynchronous reimport may finish after Cancel; never display a non-adopted snapshot.
    public EsDeCatalogueSnapshot? Snapshot => _acceptedSnapshot;
    public bool IsImported => Mode == CatalogueMode.Imported && Snapshot is not null;
    public string? ErrorMessage { get; private set; }
    public string SearchText { get; private set; } = "";
    public string? SelectedPlatform { get; private set; }
    public IReadOnlyList<ImportedSystem> Systems => Snapshot?.Games
        .GroupBy(game => game.Platform, StringComparer.OrdinalIgnoreCase)
        .Select(group => new ImportedSystem(group.Key, group.Count()))
        .OrderBy(system => system.Platform, StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<ImportedSystem>();
    public string? SelectedGameId { get; private set; }
    public EsDeGame? Selected => Snapshot?.Games.FirstOrDefault(game =>
        string.Equals(game.GameId, SelectedGameId, StringComparison.Ordinal));
    public const int PageSize = 50;
    public int PageIndex { get; private set; }
    public POCView CurrentView { get; private set; } = POCView.Dashboard;
    public int TotalCount => FilteredGames.Count;
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public IReadOnlyList<EsDeGame> PageGames => FilteredGames.Skip(PageIndex * PageSize).Take(PageSize).ToArray();

    public IReadOnlyList<EsDeGame> FilteredGames => Snapshot?.Games.Where(game =>
        (SelectedPlatform is null || string.Equals(game.Platform, SelectedPlatform,
            StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(SearchText) ||
        game.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
        game.Platform.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
        game.RomRelativePath.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray()
        ?? Array.Empty<EsDeGame>();

    // A prepared import is adopted only after EsDeImportSession.Import completed.
    public void Adopt(EsDeImportSession prepared)
    {
        if (prepared?.Current is null)
            throw new ArgumentException("A validated import is required.", nameof(prepared));
        _session = prepared;
        _acceptedSnapshot = prepared.Current;
        Mode = CatalogueMode.Imported;
        SearchText = "";
        SelectedPlatform = null;
        PageIndex = 0;
        SelectedGameId = null;
        CurrentView = POCView.Dashboard;
        ErrorMessage = null;
    }

    // Compatibility entrypoints for existing tests; the standard View calls its
    // observable VM, never these blocking methods. No parsing/I/O lives here.
    public bool TryImport(string selectedRoot)
    {
        try
        {
            var prepared = _applicationService.PrepareAsync(selectedRoot, CancellationToken.None)
                .GetAwaiter().GetResult();
            Adopt(prepared);
            return true;
        }
        catch (Exception error) when (IsExpectedImportError(error))
        {
            ImportFailed(error);
            return false;
        }
    }

    /// <summary>
    /// Legacy synchronous refresh adapter. It prepares a candidate without mutation,
    /// then uses exactly the same guarded commit as the observable VM.
    /// </summary>
    /// <returns>True only when the selected session and UI adopt one identical snapshot.</returns>
    public bool TryReimport()
    {
        if (_session is null)
        {
            ErrorMessage = "Aucune bibliothèque importée à actualiser.";
            return false;
        }
        try
        {
            var prepared = _applicationService.RefreshAsync(_session, CancellationToken.None)
                .GetAwaiter().GetResult();
            if (!TryApplyPreparedRefresh(_session, prepared))
                throw new InvalidDataException("ESDE_REFRESH_STALE_PROPOSAL");
            return true;
        }
        catch (Exception error) when (IsExpectedImportError(error))
        {
            ImportFailed(error);
            return false;
        }
    }

    /// <summary>
    /// Commits an opaque prepared refresh to the session and accepted UI state
    /// in one synchronous owner-context operation, without events or I/O between them.
    /// All display projections are calculated before the backend commit.
    /// </summary>
    /// <param name="session">Expected accepted session, checked by reference.</param>
    /// <param name="prepared">Candidate created on that session without modifying Current.</param>
    /// <returns>False without modifying the UI or session if any guard rejects the proposal.</returns>
    internal bool TryApplyPreparedRefresh(EsDeImportSession session,
        EsDeImportSession.PreparedEsDeRefresh prepared)
    {
        if (!ReferenceEquals(session, _session) || _acceptedSnapshot is null ||
            !ReferenceEquals(session.Current, _acceptedSnapshot) || prepared is null)
            return false;

        var updated = prepared.Snapshot;
        if (!string.Equals(updated.LibraryId, _acceptedSnapshot.LibraryId, StringComparison.Ordinal))
            return false;

        // Fully project all mutable UI selection/page state BEFORE a possible commit.
        // A stale/foreign proposal must leave both the VM's Current and media authority alone.
        var nextGameId = updated.Games.Any(g => g.GameId == SelectedGameId)
            ? SelectedGameId : null;
        var nextPlatform = SelectedPlatform is not null && updated.Games.Any(g =>
            string.Equals(g.Platform, SelectedPlatform, StringComparison.OrdinalIgnoreCase))
            ? SelectedPlatform : null;
        var nextView = nextGameId is null && CurrentView == POCView.GameDetail
            ? POCView.Collection : CurrentView;
        var filteredCount = updated.Games.Count(g =>
            (nextPlatform is null || string.Equals(g.Platform, nextPlatform,
                StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(SearchText) ||
                g.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
                g.Platform.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
                g.RomRelativePath.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)));
        var nextPageCount = Math.Max(1, (filteredCount + PageSize - 1) / PageSize);
        var nextPage = Math.Min(PageIndex, nextPageCount - 1);

        // No awaits, callbacks, notifications or parsing between the backend's
        // accepted-media authority switch and the UI snapshot assignment.
        if (!session.TryCommitPreparedRefresh(prepared))
            return false;
        _acceptedSnapshot = updated;
        SelectedGameId = nextGameId;
        SelectedPlatform = nextPlatform;
        CurrentView = nextView;
        PageIndex = nextPage;
        ErrorMessage = null;
        return true;
    }

    public static bool IsExpectedImportError(Exception error) =>
        error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
        System.Xml.XmlException or System.Security.SecurityException or NotSupportedException;

    public void ImportFailed(Exception error) =>
        ErrorMessage = "Import non effectué : " + EsDeDiagnosticPresentation.ImportFailure(error) + " " +
            (Snapshot is null ? "Aucun catalogue chargé." : "Dernier catalogue valide conservé.") +
            " Aucun fichier source modifié.";

    public void ReportUnavailableFolder() =>
        ErrorMessage = "Sélectionnez un dossier local accessible. Aucun import effectué.";

    public void ShowDemo()
    {
        Mode = CatalogueMode.Demonstration;
        ErrorMessage = null;
        SelectedGameId = null;
        CurrentView = POCView.Dashboard;
    }

    // Empty welcome never loads/saves the previous filesystem path or exposes demo data.
    public void ShowWelcome()
    {
        Mode = CatalogueMode.NoCatalog;
        ErrorMessage = null;
        SelectedGameId = null;
        CurrentView = POCView.Dashboard;
    }

    public bool ShowImported()
    {
        if (Snapshot is null)
        {
            ErrorMessage = "Aucune bibliothèque importée disponible.";
            return false;
        }
        Mode = CatalogueMode.Imported;
        CurrentView = POCView.Dashboard;
        return true;
    }

    // Imported navigation never points to an unselected game's details.
    public void Navigate(POCView view)
    {
        CurrentView = view == POCView.GameDetail && Selected is null
            ? POCView.Collection : view;
    }

    // UI projection only: absent optional metadata is never an invented value.
    public static string MetadataOrUnspecified(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Non renseigné" : value;

    public static IReadOnlyList<EsDeMedia> CoverCandidates(EsDeGame game) =>
        game.Media.Where(media => media.Kind is "image" or "thumbnail")
            .OrderBy(media => media.Kind == "image" ? 0 : 1).ToArray();

    // Selects only a platform present in the XML snapshot; never scans folders for systems.
    public bool SelectPlatform(string? platform)
    {
        if (platform is not null && !Systems.Any(system =>
            string.Equals(system.Platform, platform, StringComparison.OrdinalIgnoreCase)))
            return false;
        // Avalonia may emit SelectionChanged when reattaching an unchanged selector.
        // Idempotent UI values must not reset pagination, selection, or scroll.
        if (string.Equals(SelectedPlatform, platform, StringComparison.OrdinalIgnoreCase))
            return true;
        SelectedPlatform = platform;
        PageIndex = 0;
        SelectedGameId = null;
        CurrentView = POCView.Collection;
        return true;
    }

    public void Search(string? search)
    {
        var text = search ?? "";
        if (string.Equals(SearchText, text, StringComparison.Ordinal)) return;
        SearchText = text;
        PageIndex = 0;
    }

    public bool NextPage()
    {
        if (PageIndex + 1 >= PageCount) return false;
        PageIndex++;
        return true;
    }

    public bool PreviousPage()
    {
        if (PageIndex == 0) return false;
        PageIndex--;
        return true;
    }

    public bool Open(string? gameId)
    {
        if (Snapshot?.Games.Any(game => string.Equals(game.GameId, gameId, StringComparison.Ordinal)) != true)
            return false;
        SelectedGameId = gameId;
        CurrentView = POCView.GameDetail;
        return true;
    }

    public bool TryOpenMedia(EsDeMedia media, out Stream? stream)
    {
        stream = null;
        return _session is not null && _session.TryOpenMedia(media, out stream);
    }
}


/// <summary>
/// French read-only presentation of the ES-DE contract's diagnostic codes.
/// No additional filesystem inspection; the original diagnostics remain intact.
/// </summary>
// Presentation only. The importer's original ReleaseDate is never normalized,
// rewritten or assigned a timezone. A year-only source remains year-only.
public static class EsDeReleaseDatePresentation
{
    public static string Display(string? original)
    {
        if (string.IsNullOrWhiteSpace(original)) return "Non renseigné";
        var formats = new (string Source, string Display)[]
        {
            ("yyyyMMdd'T'HHmmss", "d MMMM yyyy 'à' HH:mm:ss"),
            ("yyyyMMdd", "d MMMM yyyy"),
            ("yyyy-MM-dd", "d MMMM yyyy"),
            ("yyyy", "yyyy")
        };
        foreach (var (source, output) in formats)
        {
            if (DateTime.TryParseExact(original, source,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            {
                var formatted = date.ToString(output,
                    System.Globalization.CultureInfo.GetCultureInfo("fr-FR"));
                if (source != "yyyy" && date.Day == 1)
                    formatted = "1er" + formatted[1..];
                return formatted;
            }
        }
        // Display unrecognized strings verbatim; never pretend an invented date.
        return original;
    }
}

public sealed record EsDeDiagnosticGroup(string Code, string Title, int Count, bool NeedsAttention);

public static class EsDeDiagnosticPresentation
{
    public static bool NeedsAttention(EsDeDiagnostic diagnostic) =>
        diagnostic.Code is "ROM_MISSING" or "MEDIA_MISSING" or "DISC_MISSING" or "XML_MISSING";

    public static string Summary(IReadOnlyList<EsDeDiagnostic> diagnostics)
    {
        var attention = diagnostics.Count(NeedsAttention);
        return attention + " point(s) à vérifier · " +
            (diagnostics.Count - attention) + " information(s) · aucune suppression automatique.";
    }

    // All codes are counted, including future/unknown codes. This is a bounded
    // presentation aggregation over the existing immutable snapshot, no file reads.
    public static IReadOnlyList<EsDeDiagnosticGroup> ByType(IReadOnlyList<EsDeDiagnostic> diagnostics) =>
        diagnostics.GroupBy(d => d.Code, StringComparer.Ordinal)
            .Select(group => new EsDeDiagnosticGroup(group.Key, Title(group.First()),
                group.Count(), group.Any(NeedsAttention)))
            .OrderByDescending(group => group.NeedsAttention)
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.Code, StringComparer.Ordinal)
            .ToArray();

    public static string Title(EsDeDiagnostic diagnostic) => diagnostic.Code switch
    {
        "XML_MISSING" => "Liste de jeux ES-DE absente",
        "FOLDER_ENTRY" => "Dossier ignoré",
        "IGNORED_ENTRY" => "Élément XML non reconnu",
        "TITLE_FALLBACK" => "Titre repris du nom de fichier",
        "ROM_MISSING" => "ROM indisponible",
        "MEDIA_MISSING" => "Média indisponible",
        "DISC_MISSING" => "Disque de playlist indisponible",
        "METADATA_FIELD_INVALID" => "Métadonnée facultative ES-DE ignorée",
        "RELEASE_DATE_UNSUPPORTED" => "Date de sortie ES-DE non reconnue",
        _ => "Diagnostic ES-DE (" + diagnostic.Code + ")"
    };

    public static string Context(EsDeDiagnostic diagnostic)
    {
        var platform = string.IsNullOrWhiteSpace(diagnostic.Platform)
            ? "Plateforme non précisée" : "Plateforme : " + diagnostic.Platform;
        return string.IsNullOrWhiteSpace(diagnostic.Entry)
            ? platform : platform + " · Entrée : " + diagnostic.Entry;
    }

    public static string Explanation(EsDeDiagnostic diagnostic) => diagnostic.Code switch
    {
        "XML_MISSING" => "Aucun fichier gamelist.xml pris en charge pour cette plateforme. Les autres plateformes importées restent disponibles.",
        "FOLDER_ENTRY" => "Une entrée de dossier n'est pas comptabilisée comme un jeu.",
        "IGNORED_ENTRY" => "Cet élément n'est pas un jeu ES-DE reconnu : il a été ignoré.",
        "TITLE_FALLBACK" => "Le XML ne donne pas de titre ; le nom du fichier sert de libellé.",
        "ROM_MISSING" => "Fichier absent ou support indisponible lors de l'import. Aucune suppression de jeu n'est déduite.",
        "MEDIA_MISSING" => "Média référencé introuvable. Aucune recherche réseau ni modification n'est effectuée.",
        "DISC_MISSING" => "Un disque référencé par la playlist n'a pas été trouvé. Aucun fichier n'est modifié.",
        "METADATA_FIELD_INVALID" => "Une métadonnée facultative répétée ou excessive a été ignorée sans retirer le jeu.",
        "RELEASE_DATE_UNSUPPORTED" => "La date ES-DE n'est pas dans un format reconnu ; elle reste non renseignée.",
        _ => string.IsNullOrWhiteSpace(diagnostic.Message)
            ? "Diagnostic conservé pour vérification." : diagnostic.Message
    };

    public static string ImportFailure(Exception error) => error switch
    {
        DirectoryNotFoundException =>
            "Racine ES-DE non accessible : sélectionnez le dossier parent contenant directement ROMS/. " +
            "Ne sélectionnez ni ROMS/, ni ES-DE/, ni un dossier de plateforme. Aucun scan automatique.",
        System.Xml.XmlException =>
            "Le XML ES-DE n'a pas pu être analysé (syntaxe, encodage ou contenu interdit).",
        // Stable, controlled backend refusal codes (#96). Never echo raw XML,
        // exception paths or playlist contents to the user.
        InvalidDataException e when e.Message == "ESDE_XML_BYTES_LIMIT" =>
            "La gamelist ES-DE dépasse la limite de 8 Mio. Réduisez le XML à la source ; " +
            "l'import est refusé sans charger de catalogue partiel.",
        InvalidDataException e when e.Message == "ESDE_PLAYLIST_BYTES_LIMIT" =>
            "Une playlist .m3u dépasse la limite de 64 Kio. " +
            "L'import est refusé sans modifier les fichiers.",
        InvalidDataException e when e.Message == "ESDE_PLAYLIST_LINES_LIMIT" =>
            "Une playlist .m3u dépasse 256 lignes. " +
            "L'import est refusé sans modifier les fichiers.",
        InvalidDataException e when e.Message == "ESDE_PLAYLIST_ENCODING_UNSUPPORTED" =>
            "L'encodage d'une playlist .m3u n'est pas pris en charge. " +
            "Vérifiez son encodage à la source ; aucune conversion automatique.",
        InvalidDataException e when e.Message == "ESDE_PLATFORM_CASE_COLLISION" =>
            "Deux dossiers systèmes ont des noms ne différant que par la casse " +
            "(majuscules/minuscules). Import ambigu sur Windows, refusé sans renommage.",
        InvalidDataException e when e.Message.StartsWith("Duplicated ROM identity", StringComparison.Ordinal) =>
            "Deux références ROM utilisent une identité en conflit. " +
            "Import refusé ; aucun fichier renommé ou supprimé.",
        InvalidDataException e when e.Message.StartsWith("Ambiguous ES-DE source", StringComparison.Ordinal) =>
            "Plusieurs gamelists concurrentes ont été trouvées pour une plateforme (ROMS, MEDIAS ou ES-DE). Conservez une seule source.",
        InvalidDataException e when e.Message.StartsWith("No supported ES-DE gamelist", StringComparison.Ordinal) =>
            "Aucune gamelist ES-DE prise en charge n'a été trouvée dans le dossier sélectionné.",
        EsDeXmlReadException e =>
            "Le XML ES-DE n'a pas pu être analysé (syntaxe, encodage, DTD interdite ou limite). " +
            "Système : " + e.Platform + ", convention : " + e.SourceConvention +
            (e.LineNumber > 0 ? ", ligne " + e.LineNumber +
                (e.LinePosition > 0 ? ", colonne " + e.LinePosition : "") : "") +
            ". Vérifiez la source de cette plateforme ; aucune correction automatique.",
        InvalidDataException => "XML chargé, mais sa structure, certaines métadonnées ou des chemins ne sont pas pris en charge.",
        UnauthorizedAccessException => "Accès refusé au dossier ou aux métadonnées. Vérifiez les autorisations.",
        IOException => "La lecture de fichiers ES-DE a échoué. Vérifiez la disponibilité du support.",
        ArgumentException => "Le dossier ou un chemin de fichier est invalide.",
        System.Security.SecurityException => "L'accès aux fichiers a été refusé par le système.",
        NotSupportedException => "Le format ou le chemin demandé n'est pas pris en charge.",
        _ => "Une erreur inattendue a empêché l'import."
    };
}
