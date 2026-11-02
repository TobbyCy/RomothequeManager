using System.Reflection;
using System.Text.Json;

namespace RomothequeManager.V2;

// Presentation of the canonical synthetic DEMO-01 contract, NOT an inventory reader.
// Never opens ROM paths or treats the test oracle as evidence of discovered games.
/// <summary>Declared fictional media for a demo game; expected presence is a fixture oracle, not a real filesystem check.</summary>
public sealed record ScenarioMediaPreview(string Kind, bool ExpectedInFixture, string RelativePath);
/// <summary>Fictional game detail from the approved demo contract, not an identified ROM or scraped metadata record.</summary>
public sealed record ScenarioGamePreview(
    string Id, string Title, string Platform, string Description, string Edition,
    bool RomExpectedInFixture, IReadOnlyList<ScenarioMediaPreview> Media);

/// <summary>Presentation-only state for three canonical synthetic scenarios with no ROM path access.</summary>
public sealed class DemoScenarioUiState
{
    /// <summary>Only supported fixture scenarios: prepared IDEAL, anomalous CHAOS and metadata-free MYSTERY.</summary>
    public static readonly IReadOnlyList<string> Choices = ["IDEAL", "CHAOS", "MYSTERY"];
    private readonly Dictionary<string, IReadOnlyList<ScenarioGamePreview>> _catalogues;
    /// <summary>Active fixture scenario or null before selection; never persisted across sessions.</summary>
    public string? SelectedScenario { get; private set; }
    /// <summary>Selected fictional entry, cleared when the scenario changes and unknown in MYSTERY.</summary>
    public string? SelectedGameId { get; private set; }
    /// <summary>Transient title/platform text filter applied only to declared fictional entries.</summary>
    public string SearchText { get; private set; } = "";
    /// <summary>True when a supported demo scenario is chosen; not proof of an active scan.</summary>
    public bool Active => SelectedScenario is not null;
    /// <summary>True only for scenarios with declared ES-DE-like identities; MYSTERY remains unknown.</summary>
    public bool IdentityKnown => SelectedScenario is "IDEAL" or "CHAOS";
    /// <summary>Explicit synthetic provenance notice preventing demo rows from appearing as observed files.</summary>
    public string Provenance => "Contrat de démonstration fictif · non observé sur disque";
    /// <summary>Number of fictional catalogue entries defined in the fixture oracle; not a discovered game count.</summary>
    public int DeclaredCanonicalCount { get; }
    /// <summary>Number of platform labels declared by DEMO-01, not platforms observed on a disk.</summary>
    public int DeclaredPlatformCount { get; }

    private DemoScenarioUiState(
        Dictionary<string, IReadOnlyList<ScenarioGamePreview>> catalogues,
        int count, int platforms)
    {
        _catalogues = catalogues;
        DeclaredCanonicalCount = count;
        DeclaredPlatformCount = platforms;
    }

    private static string Resource(string name)
    {
        using var stream = typeof(DemoScenarioUiState).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidDataException("Contrat de démonstration indisponible : " + name);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Loads and validates the embedded 30-game, 13-platform fixture contract without touching user storage.</summary>
    /// <returns>A state object whose scenarios preserve the declared IDEAL/CHAOS/MYSTERY differences.</returns>
    /// <exception cref="InvalidDataException">A synthetic oracle is missing, malformed or internally inconsistent.</exception>
    public static DemoScenarioUiState LoadCanonical()
    {
        using var catalogue = JsonDocument.Parse(Resource("DemoLibrary.Catalogue.json"));
        using var scenarios = JsonDocument.Parse(Resource("DemoLibrary.Scenarios.json"));
        var cat = catalogue.RootElement;
        var contracts = scenarios.RootElement;
        if (cat.GetProperty("schemaVersion").GetString() != "demo-library-01/1" ||
            contracts.GetProperty("schemaVersion").GetString() != "demo-library-01/scenarios/1" ||
            !cat.GetProperty("syntheticOnly").GetBoolean())
            throw new InvalidDataException("Contrat synthétique inattendu.");

        var entries = cat.GetProperty("games").EnumerateArray().ToArray();
        var ids = entries.Select(item => item.GetProperty("id").GetString()).ToArray();
        if (entries.Length != 30 || ids.Any(string.IsNullOrWhiteSpace) ||
            ids.Distinct(StringComparer.Ordinal).Count() != entries.Length)
            throw new InvalidDataException("Identités canoniques DEMO-01 incohérentes.");

        var platforms = cat.GetProperty("platformSlugs").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        if (platforms.Length != 13 || platforms.Distinct(StringComparer.Ordinal).Count() != 13)
            throw new InvalidDataException("Plateformes DEMO-01 incohérentes.");

        var result = new Dictionary<string, IReadOnlyList<ScenarioGamePreview>>(StringComparer.Ordinal);
        foreach (var scenario in contracts.GetProperty("variants").EnumerateArray())
        {
            var name = scenario.GetProperty("name").GetString() ?? "";
            if (!Choices.Contains(name, StringComparer.Ordinal) || result.ContainsKey(name))
                throw new InvalidDataException("Scénario DEMO-01 inconnu ou répété.");
            var specifications = scenario.GetProperty("entries").EnumerateArray()
                .ToDictionary(item => item.GetProperty("id").GetString()!, StringComparer.Ordinal);
            if (specifications.Count != entries.Length ||
                ids.Any(id => !specifications.ContainsKey(id!)))
                throw new InvalidDataException("Scénario non aligné avec le catalogue canonique.");
            if (name == "MYSTERY")
            {
                // Its private fixture oracle knows the synthetic origins, but the UI MUST NOT.
                // Without XML/metadata, neither a game title nor a confirmed-game count exists.
                result[name] = Array.Empty<ScenarioGamePreview>();
                continue;
            }
            var previews = new List<ScenarioGamePreview>();
            foreach (var item in entries)
            {
                var id = item.GetProperty("id").GetString()!;
                var spec = specifications[id];
                if (!spec.GetProperty("xmlEntryPresent").GetBoolean()) continue;
                var media = spec.GetProperty("media").EnumerateObject()
                    .Select(field => new ScenarioMediaPreview(field.Name,
                        field.Value.GetProperty("exists").GetBoolean(),
                        field.Value.GetProperty("relativePath").GetString() ?? ""))
                    .ToArray();
                previews.Add(new ScenarioGamePreview(id,
                    item.GetProperty("title").GetString() ?? "",
                    item.GetProperty("platform").GetString() ?? "",
                    item.GetProperty("description").GetString() ?? "",
                    item.GetProperty("edition").GetString() ?? "",
                    spec.GetProperty("romPresent").GetBoolean(), media));
            }
            result[name] = previews.AsReadOnly();
        }
        if (result.Count != Choices.Count || result["IDEAL"].Count != 30 ||
            result["CHAOS"].Count != 30 || result["MYSTERY"].Count != 0)
            throw new InvalidDataException("Scénarios DEMO-01 incomplets.");
        return new DemoScenarioUiState(result, entries.Length, platforms.Length);
    }

    /// <summary>Switches only to an existing fictional scenario, clearing selection and search on success.</summary>
    /// <returns>False for an unknown scenario without modifying the active selection.</returns>
    public bool Select(string? scenario)
    {
        if (scenario is null || !_catalogues.ContainsKey(scenario)) return false;
        SelectedScenario = scenario;
        SelectedGameId = null;
        SearchText = "";
        return true;
    }

    /// <summary>Resets scenario, text query and selected game without modifying any persisted state.</summary>
    public void Clear()
    {
        SelectedScenario = null;
        SelectedGameId = null;
        SearchText = "";
    }

    /// <summary>Updates only the in-memory query; does not enumerate ROMs or resolve unidentified games.</summary>
    public void Search(string? query) => SearchText = query ?? "";

    /// <summary>Filtered fictional entries; empty in MYSTERY because identity cannot be inferred without metadata.</summary>
    public IReadOnlyList<ScenarioGamePreview> VisibleGames =>
        !IdentityKnown ? Array.Empty<ScenarioGamePreview>() :
        _catalogues[SelectedScenario!].Where(game =>
            string.IsNullOrWhiteSpace(SearchText) ||
            game.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
            game.Platform.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

    /// <summary>Selected declared entry when identity is known, null for MYSTERY or an unknown ID.</summary>
    public ScenarioGamePreview? SelectedGame => !IdentityKnown ? null :
        _catalogues[SelectedScenario!].FirstOrDefault(item =>
            string.Equals(item.Id, SelectedGameId, StringComparison.Ordinal));

    /// <summary>Selects a known scenario game by exact ID, refusing any implicit game inference in MYSTERY.</summary>
    public bool Open(string? gameId)
    {
        if (!IdentityKnown || string.IsNullOrWhiteSpace(gameId) ||
            !_catalogues[SelectedScenario!].Any(item => item.Id == gameId)) return false;
        SelectedGameId = gameId;
        return true;
    }

    /// <summary>Maps a recognized synthetic scenario to its user-facing label; unknown inputs are explicitly labeled.</summary>
    public static string DisplayName(string scenario) => scenario switch
    {
        "IDEAL" => "IDEAL · bibliothèque préparée",
        "CHAOS" => "CHAOS · anomalies contrôlées",
        "MYSTERY" => "MYSTERY · sans métadonnées",
        _ => "Scénario inconnu"
    };

    /// <summary>Explains each scenario's limits, especially missing XML and non-observed identities in MYSTERY.</summary>
    public static string Explanation(string scenario) => scenario switch
    {
        "IDEAL" => "Jeux et médias décrits dans le contrat de référence. Ce n'est pas une observation réelle.",
        "CHAOS" => "Références ES-DE, deux ROM principales et certains médias prévus manquants. Doublons seulement candidats.",
        "MYSTERY" => "Aucun XML ES-DE et aucun média. Identités et nombre de jeux confirmés : INCONNUS.",
        _ => "Aucune donnée"
    };
}
