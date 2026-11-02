namespace RomothequeManager.V2;

// UI-only selection, search, paging and mode. The ES-DE importer remains the sole
// authority for XML parsing, media paths and read-only file access.
/// <summary>Presentation mode; no file access is implied by switching modes.</summary>
public enum CatalogueMode
{
    /// <summary>Empty welcome; no catalogue has been loaded or restored automatically.</summary>
    NoCatalog,
    /// <summary>Explicitly selected, entirely synthetic demonstration data.</summary>
    Demonstration,
    /// <summary>Displays an already accepted ES-DE snapshot without granting scan permissions.</summary>
    Imported
}

// A system is proven only by games referenced in imported ES-DE XML.
/// <summary>Platform and reference count inferred from the accepted ES-DE XML only.</summary>
public sealed record ImportedSystem(string Platform, int ReferencedGames);

/// <summary>
/// Stores the accepted imported catalogue's view mode, selection, filters and pages.
/// Media references remain read-only and must be reopened by the accepted session.
/// </summary>
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

    /// <summary>Injected sole read-only import orchestration service; never a new grant.</summary>
    internal IEsDeImportApplicationService ApplicationService => _applicationService;

    /// <summary>Current accepted backend authority; its media membership must match Snapshot.</summary>
    internal EsDeImportSession? Session => _session;

    /// <summary>Currently displayed shell mode; never an import or scan trigger.</summary>
    public CatalogueMode Mode { get; private set; } = CatalogueMode.NoCatalog;
    /// <summary>Whether a validated ES-DE snapshot is retained, even outside Imported mode.</summary>
    public bool HasImportedCatalogue => Snapshot is not null;
    // An asynchronous reimport may finish after Cancel; never display a non-adopted snapshot.
    /// <summary>Last UI-accepted immutable catalogue; never a pending refresh candidate.</summary>
    public EsDeCatalogueSnapshot? Snapshot => _acceptedSnapshot;
    /// <summary>True only while displaying a previously accepted imported snapshot.</summary>
    public bool IsImported => Mode == CatalogueMode.Imported && Snapshot is not null;
    /// <summary>Sanitized last import/refresh error without private root or raw XML.</summary>
    public string? ErrorMessage { get; private set; }
    /// <summary>Local collection filter; no filesystem search is performed.</summary>
    public string SearchText { get; private set; } = "";
    /// <summary>Explicit platform filter restricted to systems declared in Current XML.</summary>
    public string? SelectedPlatform { get; private set; }
    /// <summary>Platforms actually referenced in the accepted catalogue.</summary>
    public IReadOnlyList<ImportedSystem> Systems => Snapshot?.Games
        .GroupBy(game => game.Platform, StringComparer.OrdinalIgnoreCase)
        .Select(group => new ImportedSystem(group.Key, group.Count()))
        .OrderBy(system => system.Platform, StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? Array.Empty<ImportedSystem>();
    /// <summary>Identifier of a game selected from the accepted snapshot only.</summary>
    public string? SelectedGameId { get; private set; }
    /// <summary>Selected game if it still belongs to the last accepted snapshot.</summary>
    public EsDeGame? Selected => Snapshot?.Games.FirstOrDefault(game =>
        string.Equals(game.GameId, SelectedGameId, StringComparison.Ordinal));
    /// <summary>Maximum number of ES-DE game cards on one collection page.</summary>
    public const int PageSize = 50;
    /// <summary>Zero-based current page, clamped when an accepted refresh changes results.</summary>
    public int PageIndex { get; private set; }
    /// <summary>Dashboard, collection or permitted game detail in imported mode.</summary>
    public POCView CurrentView { get; private set; } = POCView.Dashboard;
    /// <summary>Number of currently filtered references, not discovered ROM files.</summary>
    public int TotalCount => FilteredGames.Count;
    /// <summary>Number of pages with a minimum of one for empty display.</summary>
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    /// <summary>At most 50 references from Current matching the filters and page.</summary>
    public IReadOnlyList<EsDeGame> PageGames => FilteredGames.Skip(PageIndex * PageSize).Take(PageSize).ToArray();

    /// <summary>Read-only projection of XML references under platform/text filters.</summary>
    public IReadOnlyList<EsDeGame> FilteredGames => Snapshot?.Games.Where(game =>
        (SelectedPlatform is null || string.Equals(game.Platform, SelectedPlatform,
            StringComparison.OrdinalIgnoreCase)) &&
        (string.IsNullOrWhiteSpace(SearchText) ||
        game.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
        game.Platform.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
        game.RomRelativePath.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))).ToArray()
        ?? Array.Empty<EsDeGame>();

    // A prepared import is adopted only after EsDeImportSession.Import completed.
    /// <summary>Adopts a completed first import and resets the imported view to Dashboard.</summary>
    /// <param name="prepared">Explicit session with a validated Current catalogue.</param>
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
    /// <summary>Compatibility adapter: explicitly stages and accepts an ES-DE root.</summary>
    /// <remarks>Standard Avalonia UI instead uses the observable VM after native consent.</remarks>
    /// <param name="selectedRoot">Root explicitly selected by the caller, never autodiscovered.</param>
    /// <returns>False on a supported import refusal, leaving the previous Current intact.</returns>
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

    /// <summary>Classifies expected read/format refusals for neutral UI diagnostics.</summary>
    /// <param name="error">Exception to classify without exposing its raw contents.</param>
    /// <returns>True when the error can be presented without crashing the shell.</returns>
    public static bool IsExpectedImportError(Exception error) =>
        error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or
        System.Xml.XmlException or System.Security.SecurityException or NotSupportedException;

    /// <summary>Shows a sanitized refusal while preserving the accepted snapshot.</summary>
    /// <param name="error">Original cause, mapped to a bounded non-private explanation.</param>
    public void ImportFailed(Exception error) =>
        ErrorMessage = "Import non effectué : " + EsDeDiagnosticPresentation.ImportFailure(error) + " " +
            (Snapshot is null ? "Aucun catalogue chargé." : "Dernier catalogue valide conservé.") +
            " Aucun fichier source modifié.";

    /// <summary>Reports refusal of a non-local or inaccessible picker selection.</summary>
    public void ReportUnavailableFolder() =>
        ErrorMessage = "Sélectionnez un dossier local accessible. Aucun import effectué.";

    /// <summary>Switches to explicit synthetic demonstration without clearing accepted ES-DE Current.</summary>
    public void ShowDemo()
    {
        Mode = CatalogueMode.Demonstration;
        ErrorMessage = null;
        SelectedGameId = null;
        CurrentView = POCView.Dashboard;
    }

    // Empty welcome never loads/saves the previous filesystem path or exposes demo data.
    /// <summary>Returns to empty welcome without accessing or exposing the last chosen path.</summary>
    public void ShowWelcome()
    {
        Mode = CatalogueMode.NoCatalog;
        ErrorMessage = null;
        SelectedGameId = null;
        CurrentView = POCView.Dashboard;
    }

    /// <summary>Displays the last accepted ES-DE catalogue, with no new file read.</summary>
    /// <returns>False if the session has no accepted catalogue.</returns>
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
    /// <summary>Navigates only to a game detail that is present in accepted Current.</summary>
    /// <param name="view">Requested imported shell section.</param>
    public void Navigate(POCView view)
    {
        CurrentView = view == POCView.GameDetail && Selected is null
            ? POCView.Collection : view;
    }

    // UI projection only: absent optional metadata is never an invented value.
    /// <summary>Displays absent optional metadata without inventing values.</summary>
    /// <param name="value">Untrusted optional XML metadata value.</param>
    /// <returns>Original text or a neutral not-provided placeholder.</returns>
    public static string MetadataOrUnspecified(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Non renseigné" : value;

    /// <summary>Returns declared image then thumbnail references; no automatic attribution.</summary>
    /// <param name="game">A game already selected from an accepted catalogue.</param>
    /// <returns>Ordered media references, each still subject to TryOpenMedia authorization.</returns>
    public static IReadOnlyList<EsDeMedia> CoverCandidates(EsDeGame game) =>
        game.Media.Where(media => media.Kind is "image" or "thumbnail")
            .OrderBy(media => media.Kind == "image" ? 0 : 1).ToArray();

    // Selects only a platform present in the XML snapshot; never scans folders for systems.
    /// <summary>Applies an XML-proven platform filter without scanning disk folders.</summary>
    /// <param name="platform">Declared platform or null to show all.</param>
    /// <returns>False if the platform is not a member of Current.</returns>
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

    /// <summary>Applies a local text filter and resets pagination only when changed.</summary>
    /// <param name="search">Text matched against title, platform and declared ROM-relative path.</param>
    public void Search(string? search)
    {
        var text = search ?? "";
        if (string.Equals(SearchText, text, StringComparison.Ordinal)) return;
        SearchText = text;
        PageIndex = 0;
    }

    /// <summary>Advances one bounded 50-reference page when available.</summary>
    /// <returns>True if the displayed page changed.</returns>
    public bool NextPage()
    {
        if (PageIndex + 1 >= PageCount) return false;
        PageIndex++;
        return true;
    }

    /// <summary>Moves one page toward the start without leaving page zero.</summary>
    /// <returns>True if the displayed page changed.</returns>
    public bool PreviousPage()
    {
        if (PageIndex == 0) return false;
        PageIndex--;
        return true;
    }

    /// <summary>Opens a detail view only for a game referenced by accepted Current.</summary>
    /// <param name="gameId">Exact game identity from the imported catalogue.</param>
    /// <returns>False for an unknown or stale identifier.</returns>
    public bool Open(string? gameId)
    {
        if (Snapshot?.Games.Any(game => string.Equals(game.GameId, gameId, StringComparison.Ordinal)) != true)
            return false;
        SelectedGameId = gameId;
        CurrentView = POCView.GameDetail;
        return true;
    }

    /// <summary>Delegates a declared media read to the accepted session's guarded opener.</summary>
    /// <param name="media">Reference that must belong to the session's accepted Current.</param>
    /// <param name="stream">Read-only stream owned by caller, null on any refusal.</param>
    /// <returns>True only when the session permits and opens this specific media reference.</returns>
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
/// <summary>Culture-safe rendering of declared ES-DE release-date strings; no filesystem access.</summary>
public static class EsDeReleaseDatePresentation
{
    /// <summary>Formats a recognized ES-DE date without changing the source's value.</summary>
    /// <param name="original">Optional original XML date; unknown patterns are returned unchanged.</param>
    /// <returns>French readable date or a neutral placeholder for missing metadata.</returns>
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

/// <summary>Aggregate of ES-DE diagnostics grouped by stable code and attention state.</summary>
public sealed record EsDeDiagnosticGroup(string Code, string Title, int Count, bool NeedsAttention);

/// <summary>Bounded French diagnostic projection; never exposes raw XML or root paths.</summary>
public static class EsDeDiagnosticPresentation
{
    /// <summary>Recognizes missing XML/ROM/MEDIA/disc references needing attention.</summary>
    /// <param name="diagnostic">One importer diagnostic code.</param>
    /// <returns>True for an actionable missing-reference warning.</returns>
    public static bool NeedsAttention(EsDeDiagnostic diagnostic) =>
        diagnostic.Code is "ROM_MISSING" or "MEDIA_MISSING" or "DISC_MISSING" or "XML_MISSING";

    /// <summary>Counts actionable and informational diagnostics without deleting anything.</summary>
    /// <param name="diagnostics">Only diagnostics produced by the existing importer.</param>
    /// <returns>Sanitized aggregate summary.</returns>
    public static string Summary(IReadOnlyList<EsDeDiagnostic> diagnostics)
    {
        var attention = diagnostics.Count(NeedsAttention);
        return attention + " point(s) à vérifier · " +
            (diagnostics.Count - attention) + " information(s) · aucune suppression automatique.";
    }

    // All codes are counted, including future/unknown codes. This is a bounded
    // presentation aggregation over the existing immutable snapshot, no file reads.
    /// <summary>Groups every diagnostic code, including unknown future codes, with stable counts.</summary>
    /// <param name="diagnostics">Immutable import diagnostics to aggregate.</param>
    /// <returns>Presentation groups ordered by attention, frequency and code.</returns>
    public static IReadOnlyList<EsDeDiagnosticGroup> ByType(IReadOnlyList<EsDeDiagnostic> diagnostics) =>
        diagnostics.GroupBy(d => d.Code, StringComparer.Ordinal)
            .Select(group => new EsDeDiagnosticGroup(group.Key, Title(group.First()),
                group.Count(), group.Any(NeedsAttention)))
            .OrderByDescending(group => group.NeedsAttention)
            .ThenByDescending(group => group.Count)
            .ThenBy(group => group.Code, StringComparer.Ordinal)
            .ToArray();

    /// <summary>Maps a diagnostic code to a bounded, user-readable label.</summary>
    /// <param name="diagnostic">Code without untrusted exception text.</param>
    /// <returns>Neutral title including unknown codes.</returns>
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

    /// <summary>Displays declared platform and entry without inspecting the filesystem.</summary>
    /// <param name="diagnostic">Diagnostic associated with the imported entry.</param>
    /// <returns>Readable platform/entry context.</returns>
    public static string Context(EsDeDiagnostic diagnostic)
    {
        var platform = string.IsNullOrWhiteSpace(diagnostic.Platform)
            ? "Plateforme non précisée" : "Plateforme : " + diagnostic.Platform;
        return string.IsNullOrWhiteSpace(diagnostic.Entry)
            ? platform : platform + " · Entrée : " + diagnostic.Entry;
    }

    /// <summary>Explains supported ES-DE diagnostic codes and their safe remediation.</summary>
    /// <param name="diagnostic">Existing importer diagnostic; no extra scan is started.</param>
    /// <returns>Bounded explanation or neutral fallback for an unknown code.</returns>
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

    /// <summary>Redacts expected import exceptions into neutral French failure messages.</summary>
    /// <param name="error">Exception whose raw message/path must not be displayed.</param>
    /// <returns>User-safe explanation that does not reveal private filesystem roots.</returns>
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
