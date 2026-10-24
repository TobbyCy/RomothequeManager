namespace RomothequeManager.V2;

// UI-only selection, search, paging and mode. The ES-DE importer remains the sole
// authority for XML parsing, media paths and read-only file access.
public enum CatalogueMode { Demonstration, Imported }

public sealed class ImportedCatalogueUiState
{
    private EsDeImportSession? _session;
    public CatalogueMode Mode { get; private set; } = CatalogueMode.Demonstration;
    public EsDeCatalogueSnapshot? Snapshot => _session?.Current;
    public bool IsImported => Mode == CatalogueMode.Imported && Snapshot is not null;
    public string? ErrorMessage { get; private set; }
    public string SearchText { get; private set; } = "";
    public string? SelectedGameId { get; private set; }
    public EsDeGame? Selected => Snapshot?.Games.FirstOrDefault(game =>
        string.Equals(game.GameId, SelectedGameId, StringComparison.Ordinal));
    public const int PageSize = 50;
    public int PageIndex { get; private set; }
    public int TotalCount => FilteredGames.Count;
    public int PageCount => Math.Max(1, (TotalCount + PageSize - 1) / PageSize);
    public IReadOnlyList<EsDeGame> PageGames => FilteredGames.Skip(PageIndex * PageSize).Take(PageSize).ToArray();

    public IReadOnlyList<EsDeGame> FilteredGames => Snapshot?.Games.Where(game =>
        string.IsNullOrWhiteSpace(SearchText) ||
        game.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
        game.Platform.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
        game.RomRelativePath.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray()
        ?? Array.Empty<EsDeGame>();

    // A prepared import is adopted only after EsDeImportSession.Import completed.
    public void Adopt(EsDeImportSession prepared)
    {
        if (prepared?.Current is null)
            throw new ArgumentException("A validated import is required.", nameof(prepared));
        _session = prepared;
        Mode = CatalogueMode.Imported;
        SearchText = "";
        PageIndex = 0;
        SelectedGameId = null;
        ErrorMessage = null;
    }

    public bool TryImport(string selectedRoot)
    {
        try
        {
            var staged = new EsDeImportSession(selectedRoot);
            staged.Import();
            Adopt(staged);
            return true;
        }
        catch (Exception error) when (IsExpectedImportError(error))
        {
            ImportFailed(error);
            return false;
        }
    }

    public bool TryReimport()
    {
        if (_session is null)
        {
            ErrorMessage = "Aucune bibliothèque importée à actualiser.";
            return false;
        }
        try
        {
            var updated = _session.Import();
            SelectedGameId = updated.Games.Any(g => g.GameId == SelectedGameId) ? SelectedGameId : null;
            PageIndex = Math.Min(PageIndex, PageCount - 1);
            ErrorMessage = null;
            return true;
        }
        catch (Exception error) when (IsExpectedImportError(error))
        {
            ImportFailed(error);
            return false;
        }
    }

    public static bool IsExpectedImportError(Exception error) =>
        error is IOException or UnauthorizedAccessException or ArgumentException or
        System.Xml.XmlException or System.Security.SecurityException or NotSupportedException;

    public void ImportFailed(Exception error) =>
        ErrorMessage = "Import refusé (" + error.GetType().Name +
            "). Dernier catalogue valide conservé ; aucune source modifiée.";

    public void ReportUnavailableFolder() =>
        ErrorMessage = "Sélectionnez un dossier local accessible. Aucun import effectué.";

    public void ShowDemo() { Mode = CatalogueMode.Demonstration; SelectedGameId = null; }

    public bool ShowImported()
    {
        if (Snapshot is null)
        {
            ErrorMessage = "Aucune bibliothèque importée disponible.";
            return false;
        }
        Mode = CatalogueMode.Imported;
        return true;
    }

    public void Search(string? search)
    {
        SearchText = search ?? "";
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
        return true;
    }

    public bool TryOpenMedia(EsDeMedia media, out Stream? stream)
    {
        stream = null;
        return _session is not null && _session.TryOpenMedia(media, out stream);
    }
}
