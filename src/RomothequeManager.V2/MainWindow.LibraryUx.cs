using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RomothequeManager.V2;

// An opt-in synthetic presentation. The embedded contracts describe fixtures;
// they do not represent observed files, identified MYSTERY games or a live scan.
public sealed partial class MainWindow
{
    private readonly DemoScenarioUiState _scenarioUi = DemoScenarioUiState.LoadCanonical();
    private bool _scenarioDetail;
    private StackPanel? _scenarioResults;

    private static IBrush ScenarioAccent => new SolidColorBrush(Color.Parse("#B3202E"));

    private Control ScenarioSelector()
    {
        var root = new StackPanel { Spacing = 9 };
        root.Children.Add(Text("Parcours des bibliothèques de démonstration", 20, true));
        root.Children.Add(Text("Ces trois scénarios décrivent uniquement les fixtures synthétiques. Aucune bibliothèque réelle n'est analysée.", 13));
        foreach (var choice in DemoScenarioUiState.Choices)
        {
            var key = choice;
            var label = DemoScenarioUiState.DisplayName(key) +
                (key == _scenarioUi.SelectedScenario ? "  ·  SÉLECTIONNÉE" : "");
            var button = Button(label, () => SelectScenario(key));
            button.MinHeight = 46;
            if (key == _scenarioUi.SelectedScenario) button.BorderBrush = ScenarioAccent;
            root.Children.Add(button);
            root.Children.Add(Text(DemoScenarioUiState.Explanation(key), 12));
        }
        root.Children.Add(Text("Pour afficher de véritables fichiers médias locaux, choisir explicitement une racine ES-DE avec le sélecteur de la barre latérale.", 12));
        return Panel(root);
    }

    private void SelectScenario(string name)
    {
        if (!_scenarioUi.Select(name)) return;
        _importedUi.ShowDemo();
        _scenarioDetail = false;
        _state.ShowDashboard();
        Render();
    }

    // Test-only: selections are deterministic and do not touch a filesystem.
    internal bool PrepareScenarioCapture(string name, POCView view)
    {
        if (!_scenarioUi.Select(name)) return false;
        _importedUi.ShowDemo();
        _scenarioDetail = false;
        switch (view)
        {
            case POCView.Dashboard: _state.ShowDashboard(); break;
            case POCView.Collection: _state.ShowCollection(); break;
            case POCView.Libraries: _state.ShowLibraries(); break;
            case POCView.GameDetail:
                _state.ShowCollection();
                if (!_scenarioUi.Open("demo01-011")) return false;
                _scenarioDetail = true;
                break;
            default: return false;
        }
        Render();
        return true;
    }

    private void RenderScenario()
    {
        _page.Children.Add(Text("DÉMONSTRATION · LECTURE SEULE · AUCUN SCAN", 12, true));
        if (_scenarioDetail) { ScenarioDetail(); return; }
        switch (_state.CurrentView)
        {
            case POCView.Dashboard: ScenarioDashboard(); break;
            case POCView.Collection: ScenarioCollection(); break;
            case POCView.Libraries:
                _page.Children.Add(ScenarioSelector());
                _page.Children.Add(Text("Aucun rapprochement automatique avec les snapshots historiques A/B.", 12));
                break;
            case POCView.GameDetail: ScenarioCollection(); break;
        }
    }

    private void ScenarioDashboard()
    {
        var name = _scenarioUi.SelectedScenario!;
        _page.Children.Add(Text(DemoScenarioUiState.DisplayName(name), 30, true));
        _page.Children.Add(Text(_scenarioUi.Provenance, 13));
        _page.Children.Add(Text(DemoScenarioUiState.Explanation(name), 15, true));
        var summary = new StackPanel { Spacing = 8 };
        if (_scenarioUi.IdentityKnown)
        {
            summary.Children.Add(Text("30 entrées XML prévues · 13 plateformes documentées", 19, true));
            summary.Children.Add(Text("Ces titres proviennent du contrat canonique. Aucun fichier n'a été observé par cette vue.", 13));
            var previews = new StackPanel { Spacing = 7 };
            foreach (var game in _scenarioUi.VisibleGames.Take(3))
                previews.Children.Add(Text(game.Title + " · " + game.Platform.ToUpperInvariant() + " · média prévu, non chargé", 14));
            summary.Children.Add(previews);
            summary.Children.Add(Button("Parcourir cette collection →", () => Navigate(POCView.Collection)));
        }
        else
        {
            summary.Children.Add(Text("Jeux identifiés : INCONNU · Métadonnées : NON DISPONIBLES", 18, true));
            summary.Children.Add(Text("L'oracle privé connaît l'origine des fichiers fictifs, mais l'application ne peut pas la déduire.", 14));
            summary.Children.Add(Button("Voir les éléments non identifiés →", () => Navigate(POCView.Collection)));
        }
        _page.Children.Add(Panel(summary));
        _page.Children.Add(Button("Changer de bibliothèque", () => Navigate(POCView.Libraries)));
        _page.Children.Add(Button("Importer une bibliothèque ES-DE locale (choix explicite)", () => _ = ChooseLibraryAsync()));
        _page.Children.Add(Text("Aucun scraping, export, suppression ni identification automatique.", 12));
    }

    private void ScenarioCollection()
    {
        _page.Children.Add(Button("← Tableau de bord", () => Navigate(POCView.Dashboard)));
        _page.Children.Add(Text("Collection — " + _scenarioUi.SelectedScenario, 28, true));
        _page.Children.Add(Text(_scenarioUi.Provenance, 13));
        if (!_scenarioUi.IdentityKnown)
        {
            _page.Children.Add(Panel(Group(
                Text("Jeux identifiés : INCONNU", 20, true),
                Text("MYSTERY ne contient ni gamelist ES-DE, ni titres, ni médias déclarés. Aucun nombre de jeux confirmé ne peut être affiché.", 15),
                Text("Les fichiers physiques prévus par l'oracle de test ne prouvent ni une identité de jeu ni une suppression.", 13),
                Text("Aucune fiche de jeu n'est ouverte sans métadonnées.", 13)
            )));
            return;
        }
        _page.Children.Add(Text("Jeux déclarés dans le scénario, non observés. Les fichiers indiqués manquants restent des références.", 13));
        var search = new TextBox
        {
            PlaceholderText = "Rechercher un titre ou une plateforme dans le contrat",
            Text = _scenarioUi.SearchText
        };
        search.TextChanged += (_, _) => { _scenarioUi.Search(search.Text); FillScenarioResults(); };
        _page.Children.Add(search);
        _scenarioResults = new StackPanel { Spacing = 9 };
        _page.Children.Add(_scenarioResults);
        FillScenarioResults();
    }

    private void FillScenarioResults()
    {
        if (_scenarioResults is null) return;
        _scenarioResults.Children.Clear();
        var games = _scenarioUi.VisibleGames;
        _scenarioResults.Children.Add(Text(games.Count + " entrée(s) XML prévues · identité fictive, non vérifiée sur disque.", 13));
        foreach (var game in games)
        {
            var gameId = game.Id;
            var media = game.Media.Count(m => m.ExpectedInFixture);
            var summary = new StackPanel { Spacing = 5 };
            summary.Children.Add(Text(game.Title, 18, true));
            summary.Children.Add(Text(game.Platform.ToUpperInvariant() + " · " + game.Edition, 13));
            summary.Children.Add(Text(game.RomExpectedInFixture
                ? "Fichier ROM prévu dans le scénario — non observé"
                : "ROM référencée mais manquante dans le scénario — suppression non déduite", 13));
            summary.Children.Add(Text(media + " / " + game.Media.Count + " médias prévus · aucun décodeur exécuté", 12));
            summary.Children.Add(Button("Ouvrir la fiche", () =>
            {
                if (!_scenarioUi.Open(gameId)) return;
                _scenarioDetail = true;
                Render();
            }));
            _scenarioResults.Children.Add(Panel(summary));
        }
    }

    private void ScenarioDetail()
    {
        var game = _scenarioUi.SelectedGame;
        if (game is null)
        {
            _scenarioDetail = false;
            _state.ShowCollection();
            ScenarioCollection();
            return;
        }
        _page.Children.Add(Button("← Retour à la collection", () =>
        {
            _scenarioDetail = false;
            _state.ShowCollection();
            Render();
        }));
        _page.Children.Add(Text(game.Title, 29, true));
        _page.Children.Add(Text(_scenarioUi.SelectedScenario + " · " + _scenarioUi.Provenance, 13));
        var details = new StackPanel { Spacing = 8 };
        details.Children.Add(Text("Plateforme : " + game.Platform.ToUpperInvariant() + " · Édition : " + game.Edition, 16, true));
        details.Children.Add(Text(game.Description, 14));
        details.Children.Add(Text("Identifiant du contrat fictif : " + game.Id, 12));
        details.Children.Add(Text(game.RomExpectedInFixture
            ? "ROM attendue dans la fixture — disponibilité réelle non vérifiée"
            : "ROM absente selon le scénario ; référence conservée, pas de suppression prouvée", 14, true));
        _page.Children.Add(Panel(details));
        _page.Children.Add(Text("Médias du scénario — états prévus, pas des images décodées", 19, true));
        var gallery = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 155, ItemHeight = 110 };
        foreach (var media in game.Media)
        {
            var tile = new StackPanel { Spacing = 5, Margin = new Thickness(4) };
            tile.Children.Add(Text(media.Kind.ToUpperInvariant(), 12, true));
            tile.Children.Add(Text(media.ExpectedInFixture
                ? "Prévu · non chargé"
                : "Manquant attendu", 12));
            tile.Children.Add(Text("Aucun visuel substitué", 11));
            gallery.Children.Add(Panel(tile));
        }
        _page.Children.Add(gallery);
        _page.Children.Add(Button("Voir les médias réellement accessibles : importer ES-DE…",
            () => _ = ChooseLibraryAsync()));
        _page.Children.Add(Text("Captures, dos et arrière-plans ne sont pas déduits de l'import ES-DE actuel.", 12));
    }
}
