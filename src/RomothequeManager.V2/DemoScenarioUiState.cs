using System.Reflection;
using System.Text.Json;

namespace RomothequeManager.V2;

// Presentation of the canonical synthetic DEMO-01 contract, NOT an inventory reader.
// Never opens ROM paths or treats the test oracle as evidence of discovered games.
public sealed record ScenarioMediaPreview(string Kind, bool ExpectedInFixture, string RelativePath);
public sealed record ScenarioGamePreview(
    string Id, string Title, string Platform, string Description, string Edition,
    bool RomExpectedInFixture, IReadOnlyList<ScenarioMediaPreview> Media);

public sealed class DemoScenarioUiState
{
    public static readonly IReadOnlyList<string> Choices = ["IDEAL", "CHAOS", "MYSTERY"];
    private readonly Dictionary<string, IReadOnlyList<ScenarioGamePreview>> _catalogues;
    public string? SelectedScenario { get; private set; }
    public string? SelectedGameId { get; private set; }
    public string SearchText { get; private set; } = "";
    public bool Active => SelectedScenario is not null;
    public bool IdentityKnown => SelectedScenario is "IDEAL" or "CHAOS";
    public string Provenance => "Contrat de démonstration fictif · non observé sur disque";
    public int DeclaredCanonicalCount { get; }
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

    public bool Select(string? scenario)
    {
        if (scenario is null || !_catalogues.ContainsKey(scenario)) return false;
        SelectedScenario = scenario;
        SelectedGameId = null;
        SearchText = "";
        return true;
    }

    public void Clear()
    {
        SelectedScenario = null;
        SelectedGameId = null;
        SearchText = "";
    }

    public void Search(string? query) => SearchText = query ?? "";

    public IReadOnlyList<ScenarioGamePreview> VisibleGames =>
        !IdentityKnown ? Array.Empty<ScenarioGamePreview>() :
        _catalogues[SelectedScenario!].Where(game =>
            string.IsNullOrWhiteSpace(SearchText) ||
            game.Title.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase) ||
            game.Platform.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToArray();

    public ScenarioGamePreview? SelectedGame => !IdentityKnown ? null :
        _catalogues[SelectedScenario!].FirstOrDefault(item =>
            string.Equals(item.Id, SelectedGameId, StringComparison.Ordinal));

    public bool Open(string? gameId)
    {
        if (!IdentityKnown || string.IsNullOrWhiteSpace(gameId) ||
            !_catalogues[SelectedScenario!].Any(item => item.Id == gameId)) return false;
        SelectedGameId = gameId;
        return true;
    }

    public static string DisplayName(string scenario) => scenario switch
    {
        "IDEAL" => "IDEAL · bibliothèque préparée",
        "CHAOS" => "CHAOS · anomalies contrôlées",
        "MYSTERY" => "MYSTERY · sans métadonnées",
        _ => "Scénario inconnu"
    };

    public static string Explanation(string scenario) => scenario switch
    {
        "IDEAL" => "Jeux et médias décrits dans le contrat de référence. Ce n'est pas une observation réelle.",
        "CHAOS" => "Références ES-DE, deux ROM principales et certains médias prévus manquants. Doublons seulement candidats.",
        "MYSTERY" => "Aucun XML ES-DE et aucun média. Identités et nombre de jeux confirmés : INCONNUS.",
        _ => "Aucune donnée"
    };
}
