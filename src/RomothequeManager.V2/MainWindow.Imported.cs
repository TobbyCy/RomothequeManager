using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace RomothequeManager.V2;

// The import adapter lives in DEV 1's contract. This partial file owns only
// presentation, file-picker opt-in and read-only image decoding.
public sealed partial class MainWindow
{
    private readonly ImportedCatalogueUiState _importedUi = new();
    private readonly List<Bitmap> _coverImages = new();
    private StackPanel? _importedList;
    private bool _importBusy;
    private POCView ActiveView => _importedUi.IsImported ? _importedUi.CurrentView :
        _scenarioUi.Active && _scenarioDetail ? POCView.GameDetail : _state.CurrentView;

    private void Navigate(POCView view)
    {
        _scenarioDetail = false;
        if (_importedUi.IsImported)
            _importedUi.Navigate(view);
        else
        {
            switch (view)
            {
                case POCView.Dashboard: _state.ShowDashboard(); break;
                case POCView.Collection: _state.ShowCollection(); break;
                case POCView.Libraries: _state.ShowLibraries(); break;
            }
        }
        Render();
    }

    private void ShowDemo()
    {
        _scenarioUi.Clear();
        _scenarioDetail = false;
        _importedUi.ShowDemo();
        _state.ShowDashboard();
        Render();
    }

    private void ShowImported()
    {
        _importedUi.ShowImported();
        Render();
    }

    private async Task ChooseLibraryAsync()
    {
        if (_importBusy) return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Sélectionner la racine ROMS / MEDIAS (lecture seule)",
                AllowMultiple = false
            });
            var selected = folders.FirstOrDefault();
            if (selected is null) return;
            if (!selected.Path.IsFile)
            {
                _importedUi.ReportUnavailableFolder();
                Render();
                return;
            }
            await RunImportAsync(() => _importedUi.TryImport(selected.Path.LocalPath));
        }
        catch (Exception error) when (ImportedCatalogueUiState.IsExpectedImportError(error) ||
            error is InvalidOperationException)
        {
            _importedUi.ImportFailed(error);
            Render();
        }
    }

    private async Task RefreshImportAsync()
    {
        if (_importBusy) return;
        await RunImportAsync(_importedUi.TryReimport);
    }

    private async Task RunImportAsync(Func<bool> operation)
    {
        _importBusy = true;
        Render();
        try
        {
            var success = await Task.Run(operation);
            if (success) _importedUi.Navigate(POCView.Dashboard);
        }
        finally
        {
            _importBusy = false;
            Render();
        }
    }

    private void ReleaseCoverImages()
    {
        // Called after removing the previous visual tree, not during drawing.
        foreach (var cover in _coverImages) cover.Dispose();
        _coverImages.Clear();
    }


    // Test-only exercise of the REAL imported Avalonia visual tree on disposable CI
    // fixtures. Never invoked during normal launch; no network, real ROM or scraping.
    // This verifies the labels actually presented, unlike a model-only unit test.
    public bool SmokeExerciseOfflineImportedFixture(string fixtureRoot)
    {
        if (!_importedUi.TryImport(fixtureRoot) || !_importedUi.IsImported)
            return false;
        var original = _importedUi.Snapshot;
        if (original?.Games.Count != 4 ||
            !original.Diagnostics.Any(d => d.Code == "ROM_MISSING") ||
            !original.Diagnostics.Any(d => d.Code == "MEDIA_MISSING"))
            return false;
        // Verify media decoding through the REAL Avalonia bitmap renderer.
        // A: missing cover -> valid local thumbnail.
        // B: valid local cover. C: intentionally corrupt file -> honest placeholder.
        var gameA = original.Games.Single(g =>
            g.RomRelativePath.EndsWith("A.fixture", StringComparison.OrdinalIgnoreCase));
        var gameB = original.Games.Single(g =>
            g.RomRelativePath.EndsWith("B.fixture", StringComparison.OrdinalIgnoreCase));
        var gameC = original.Games.Single(g =>
            g.RomRelativePath.EndsWith("C.fixture", StringComparison.OrdinalIgnoreCase));
        if (CoverFor(gameA, 48, 64) is not Image ||
            CoverFor(gameB, 48, 64) is not Image ||
            CoverFor(gameC, 48, 64) is not TextBlock intentionallyCorrupt ||
            !string.Equals(intentionallyCorrupt.Text, "Jaquette illisible / miniature indisponible",
                StringComparison.Ordinal))
            return false;
        Render();
        var dashboardText = SmokeVisibleTexts();
        if (!dashboardText.Contains("Diagnostics de l'import", StringComparison.Ordinal) ||
            !dashboardText.Contains("ROM indisponible", StringComparison.Ordinal) ||
            !dashboardText.Contains("Média indisponible", StringComparison.Ordinal) ||
            !dashboardText.Contains("À vérifier", StringComparison.Ordinal))
            return false;

        _importedUi.Navigate(POCView.Collection);
        Render();
        if (!SmokeVisibleTexts().Contains("4 résultat(s) importé(s)", StringComparison.Ordinal))
            return false;
        var first = original.Games[0];
        if (!_importedUi.Open(first.GameId))
            return false;
        Render();
        if (!SmokeVisibleTexts().Contains("Médias référencés", StringComparison.Ordinal))
            return false;
        _importedUi.Navigate(POCView.Libraries);
        Render();
        if (!SmokeVisibleTexts().Contains("Comparaison A/B désactivée", StringComparison.Ordinal))
            return false;

        // Mutate only the synthetic gamelist in RUNNER_TEMP, always restore it.
        var xmlPath = Path.Combine(fixtureRoot, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        var saved = File.ReadAllBytes(xmlPath);
        var rejected = false;
        try
        {
            File.WriteAllText(xmlPath, "<gameList><game>", new System.Text.UTF8Encoding(false));
            rejected = !_importedUi.TryReimport() &&
                ReferenceEquals(original, _importedUi.Snapshot) &&
                _importedUi.ErrorMessage is string message &&
                message.Contains("XML ES-DE est mal formé", StringComparison.Ordinal) &&
                message.Contains("Dernier catalogue valide conservé", StringComparison.Ordinal);
            Render();
            rejected &= SmokeVisibleTexts().Contains("Dernier catalogue valide conservé", StringComparison.Ordinal);
        }
        finally { File.WriteAllBytes(xmlPath, saved); }
        if (!rejected || !_importedUi.TryReimport() || _importedUi.Snapshot?.Games.Count != 4)
            return false;

        _importedUi.ShowDemo();
        Render();
        return _state.CurrentView == POCView.Dashboard || _page.Children.Count >= 3;
    }

    private string SmokeVisibleTexts()
    {
        return string.Join("\n", _page.Children.SelectMany(SmokeText));
    }

    private static IEnumerable<string> SmokeText(Control control)
    {
        if (control is TextBlock text)
        {
            yield return text.Text ?? "";
            yield break;
        }
        IEnumerable<Control> children = control switch
        {
            Panel panel => panel.Children.OfType<Control>(),
            Decorator decorator when decorator.Child is Control child => new[] { child },
            ContentControl content when content.Content is Control child => new[] { child },
            _ => Array.Empty<Control>()
        };
        foreach (var child in children)
            foreach (var value in SmokeText(child))
                yield return value;
    }

    private void RenderImported()
    {
        var snapshot = _importedUi.Snapshot;
        if (snapshot is null)
        {
            _page.Children.Add(Text("Aucune bibliothèque importée.", 18, true));
            return;
        }
        if (_importedUi.CurrentView == POCView.Dashboard)
        {
            ImportedDashboard(snapshot);
            return;
        }
        _page.Children.Add(Text("BIBLIOTHÈQUE IMPORTÉE · ES-DE · LECTURE SEULE", 13, true));
        _page.Children.Add(Text("Cette vue reflète uniquement les gamelists trouvées. Aucun scan complet, aucune suppression déduite.", 13));
        switch (_importedUi.CurrentView)
        {
            case POCView.Dashboard: ImportedDashboard(snapshot); break;
            case POCView.Collection: ImportedCollection(); break;
            case POCView.GameDetail: ImportedDetail(); break;
            case POCView.Libraries: ImportedLibraries(snapshot); break;
        }
    }

    private void ImportedDashboard(EsDeCatalogueSnapshot snapshot)
    {
        foreach (var section in VisualDashboard.BuildImported(
            snapshot, game => CoverFor(game, 65, 77),
            () => Navigate(POCView.Collection),
            () => Navigate(POCView.Libraries),
            VisualDashboard.IsCompact(Width)))
            _page.Children.Add(section);
    }

    private void ImportedCollection()
    {
        _page.Children.Add(Text("Collection", 30, true));
        _page.Children.Add(Text("Jeux importés depuis les XML ES-DE ; deux éditions homonymes restent distinctes.", 14));
        var search = new TextBox
        {
            PlaceholderText = "Rechercher titre, plateforme ou chemin ROM",
            Text = _importedUi.SearchText
        };
        search.TextChanged += (_, _) =>
        {
            _importedUi.Search(search.Text);
            FillImportedResults();
        };
        _page.Children.Add(search);
        _importedList = new StackPanel { Spacing = 10 };
        _page.Children.Add(_importedList);
        FillImportedResults();
    }

    private void FillImportedResults()
    {
        if (_importedList is null) return;
        _importedList.Children.Clear();
        ReleaseCoverImages();
        var count = _importedUi.TotalCount;
        _importedList.Children.Add(Text(count + " résultat(s) importé(s) · page " +
            (_importedUi.PageIndex + 1) + " / " + _importedUi.PageCount, 13));
        foreach (var game in _importedUi.PageGames)
        {
            var gameId = game.GameId;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 15 };
            line.Children.Add(CoverFor(game, 75, 90));
            var details = Group(
                Text(game.Title, 18, true),
                Text(game.Platform + " · " + game.RomRelativePath, 12),
                Text(game.RomExists ? "ROM présente au dernier import" :
                    "ROM non trouvée — aucune suppression déduite", 12),
                Button("Ouvrir la fiche", () =>
                {
                    if (_importedUi.Open(gameId))
                    {
                        Render();
                    }
                })
            );
            line.Children.Add(details);
            _importedList.Children.Add(Panel(line));
        }
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        var previous = Button("← Précédente", () =>
        {
            if (_importedUi.PreviousPage()) FillImportedResults();
        });
        previous.IsEnabled = _importedUi.PageIndex > 0;
        pages.Children.Add(previous);
        var next = Button("Suivante →", () =>
        {
            if (_importedUi.NextPage()) FillImportedResults();
        });
        next.IsEnabled = _importedUi.PageIndex + 1 < _importedUi.PageCount;
        pages.Children.Add(next);
        _importedList.Children.Add(pages);
    }

    private void ImportedDetail()
    {
        var game = _importedUi.Selected;
        if (game is null)
        {
            _importedUi.Navigate(POCView.Collection);
            ImportedCollection();
            return;
        }
        _page.Children.Add(Button("← Retour à la collection", () => Navigate(POCView.Collection)));
        _page.Children.Add(Text(game.Title, 30, true));
        _page.Children.Add(Panel(Group(
            Text("Plateforme : " + game.Platform, 18, true),
            Text("Variante : " + game.VariantId),
            Text("ROM : " + game.RomRelativePath),
            Text(game.RomExists ? "Fichier présent lors de l'import" :
                "Fichier introuvable / support indisponible — suppression non déduite", 14, true),
            Text("Identité : " + game.GameId, 12),
            Text("Description : " + (string.IsNullOrWhiteSpace(game.Description) ?
                "Non renseignée" : game.Description))
        )));
        _page.Children.Add(Panel(Group(
            Text("Jaquette", 19, true),
            CoverFor(game, 240, 280)
        )));
        _page.Children.Add(Text("Galerie locale · uniquement les médias déclarés dans le XML ES-DE", 19, true));
        var gallery = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var media in game.Media.Where(m => m.Kind is "image" or "thumbnail" or "marquee"))
            gallery.Children.Add(ImportedMediaPreview(media));
        if (gallery.Children.Count > 0) _page.Children.Add(gallery);
        _page.Children.Add(Text("Médias référencés", 19, true));
        if (game.Media.Count == 0) _page.Children.Add(Text("Médias non renseignés dans le XML : état inconnu, pas absence de fichiers prouvée.", 13));
        _page.Children.Add(Text("Captures, dos et fonds non exposés par cet importeur ; aucun visuel n'est inventé.", 12));
        foreach (var media in game.Media)
            _page.Children.Add(Text(media.Kind + " · " + media.RelativePath + " · " +
                (media.Exists ? "présent au dernier import (lecture à vérifier)" : "absent au dernier import"), 13));
        if (game.DiscPaths.Count > 0)
        {
            _page.Children.Add(Text("Disques référencés dans la playlist", 19, true));
            foreach (var disc in game.DiscPaths) _page.Children.Add(Text(disc, 13));
        }
    }


    private Control ImportedMediaPreview(EsDeMedia media)
    {
        var title = media.Kind switch
        {
            "image" => "Jaquette",
            "thumbnail" => "Miniature",
            "marquee" => "Logo",
            _ => "Média"
        };
        var group = new StackPanel { Spacing = 7, Margin = new Thickness(4), Width = 170 };
        group.Children.Add(Text(title, 14, true));
        if (!media.Exists)
        {
            group.Children.Add(Text("Référencé, mais introuvable lors de l'import", 12));
            return Panel(group);
        }
        if (!_importedUi.TryOpenMedia(media, out var stream))
        {
            group.Children.Add(Text("Fichier indisponible à la lecture · état actuel inconnu", 12));
            return Panel(group);
        }
        try
        {
            using (stream)
            {
                var bitmap = new Bitmap(stream!);
                _coverImages.Add(bitmap);
                group.Children.Add(new Image
                {
                    Source = bitmap, Width = 145,
                    Height = media.Kind == "marquee" ? 84 : 178,
                    Stretch = Stretch.Uniform
                });
                group.Children.Add(Text("Décodé depuis un fichier local déclaré", 11));
            }
        }
        catch (Exception error) when (error is IOException or ArgumentException or
            InvalidDataException or InvalidOperationException or NotSupportedException or
            System.Runtime.InteropServices.COMException)
        {
            group.Children.Add(Text("Fichier média illisible · à vérifier", 12));
        }
        return Panel(group);
    }

    private Control CoverFor(EsDeGame game, double width, double height)
    {
        var candidates = ImportedCatalogueUiState.CoverCandidates(game);
        if (candidates.Count == 0) return Text("Jaquette non renseignée", 13);
        var undecodable = false;
        foreach (var media in candidates)
        {
            if (!_importedUi.TryOpenMedia(media, out var stream)) continue;
            try
            {
                using (stream)
                {
                    var bitmap = new Bitmap(stream!);
                    _coverImages.Add(bitmap);
                    return new Image
                    {
                        Source = bitmap, Width = width, Height = height,
                        Stretch = Stretch.Uniform
                    };
                }
            }
            catch (Exception error) when (error is IOException or ArgumentException or
                InvalidDataException or InvalidOperationException or NotSupportedException or
                System.Runtime.InteropServices.COMException)
            {
                // Invalid image data is a placeholder case; try the thumbnail next.
                undecodable = true;
            }
        }
        return Text(undecodable
            ? "Jaquette illisible / miniature indisponible"
            : "Jaquette absente / indisponible", 13);
    }

    private void ImportedLibraries(EsDeCatalogueSnapshot snapshot)
    {
        _page.Children.Add(Text("Bibliothèques", 30, true));
        _page.Children.Add(Panel(Group(
            Text("Bibliothèque importée (session actuelle)", 20, true),
            Text("Identité de session : " + snapshot.LibraryId, 12),
            Text(snapshot.Games.Count + " jeu(x) · " + snapshot.Diagnostics.Count + " diagnostic(s)"),
            Text("Comparaison A/B désactivée pour ce catalogue importé.", 16, true),
            Text("Les snapshots A/B du mode démonstration sont fictifs et indépendants de votre bibliothèque.", 13)
        )));
    }
}
