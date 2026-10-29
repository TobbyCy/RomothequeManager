using RomothequeManager.V2.MvvmFoundation;
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
    // Ignore callbacks from controls detached during an Avalonia visual-tree rebuild.
    private ComboBox? _liveImportedSystemSelector;
    private TextBox? _liveImportedSearchBox;
    private bool _importBusy;
    private POCView ActiveView => _importedUi.IsImported ? _importedUi.CurrentView :
        _scenarioUi.Active && _scenarioDetail ? POCView.GameDetail : _state.CurrentView;

    private void Navigate(POCView view)
    {
        var returningFromDetail = ActiveView == POCView.GameDetail && view == POCView.Collection;
        var imported = _importedUi.IsImported;
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
        if (returningFromDetail) RestoreCollectionOffset(imported);
        else if (_contentScroll is not null) _contentScroll.Offset = default;
    }

    private void OpenImportedGame(string gameId)
    {
        var offset = _contentScroll?.Offset ?? default;
        if (_importedUi.Open(gameId))
        {
            _importedCollectionOffset = offset;
            Render();
            if (_contentScroll is not null) _contentScroll.Offset = default;
        }
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
                Title = "Choisir le dossier contenant ROMS (MEDIAS / ES-DE facultatifs)",
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
            if (!await ConfirmEsDeReadOnlyAsync()) return;
            await RunImportAsync(() => _importedUi.TryImport(selected.Path.LocalPath));
        }
        catch (Exception error) when (ImportedCatalogueUiState.IsExpectedImportError(error) ||
            error is InvalidOperationException)
        {
            _importedUi.ImportFailed(error);
            Render();
        }
    }

    // A second explicit choice after the picker; no implicit scan or import.
    private async Task<bool> ConfirmEsDeReadOnlyAsync()
    {
        var dialog = new Window
        {
            Title = "Importer les références ES-DE ?",
            Width = 525, Height = 310, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.Parse("#1D2025"))
        };
        var content = new StackPanel { Margin = new Thickness(20), Spacing = 12 };
        content.Children.Add(Text("Importer le catalogue ES-DE sélectionné ?", 20, true));
        content.Children.Add(Notice("Métadonnées uniquement · lecture seule",
            "Sélectionnez la racine contenant ROMS et, selon votre installation, MEDIAS ou ES-DE/gamelists. Seuls les XML et médias explicitement déclarés seront consultés."));
        content.Children.Add(Text("Aucun scan complet, aucune écriture, aucun réseau et aucune synchronisation automatique.", 12));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        actions.Children.Add(Button("Annuler", () => dialog.Close(false)));
        actions.Children.Add(PrimaryButton("Importer en lecture seule", () => dialog.Close(true)));
        content.Children.Add(actions);
        dialog.Content = content;
        return await dialog.ShowDialog<bool>(this);
    }

    private async Task RefreshImportAsync()
    {
        if (_importBusy) return;
        await RunImportAsync(_importedUi.TryReimport);
    }

    private async Task RunImportAsync(Func<bool> operation)
    {
        var wasCollection = _importedUi.IsImported && ActiveView == POCView.Collection;
        var previousOffset = _contentScroll?.Offset ?? default;
        _importBusy = true;
        Render();
        try
        {
            // A new import adopts Dashboard; refresh keeps system, search, page and detail.
            await Task.Run(operation);
        }
        finally
        {
            _importBusy = false;
            Render();
            if (wasCollection && _importedUi.IsImported && ActiveView == POCView.Collection)
            {
                _importedCollectionOffset = previousOffset;
                RestoreCollectionOffset(true);
            }
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
                message.Contains("XML ES-DE n'a pas pu être analysé", StringComparison.Ordinal) &&
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
        // Buttons use plain string Content; include their accessible captions in CI UI assertions.
        if (control is ContentControl { Content: string caption })
            yield return caption;
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
        _page.Children.Add(Text("BIBLIOTHÈQUE IMPORTÉE  /  ES-DE  /  LECTURE SEULE", 11, true));
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
        _page.Children.Add(ImportedSystemStrip());
        _page.Children.Add(ImportedDiagnostics(snapshot, compact: true));
    }

    // Only imported XML diagnostics are displayed. A missing reference is never a deletion.
    private Control ImportedDiagnostics(EsDeCatalogueSnapshot snapshot, bool compact)
    {
        var panel = new StackPanel { Spacing = 10 };
        panel.Children.Add(Text("Diagnostics de l'import", 22, true));
        panel.Children.Add(Text(EsDeDiagnosticPresentation.Summary(snapshot.Diagnostics), 13));
        if (snapshot.Diagnostics.Count == 0)
            panel.Children.Add(Text("Aucune anomalie remontée par les XML lus. La couverture physique n'est pas certifiée.", 12));
        else
        {
            var selected = snapshot.Diagnostics.Take(compact ? 4 : 20).ToArray();
            foreach (var item in selected)
            {
                panel.Children.Add(Notice(
                    EsDeDiagnosticPresentation.Title(item) + " · " +
                    (string.IsNullOrWhiteSpace(item.Platform) ? "système inconnu" : item.Platform),
                    EsDeDiagnosticPresentation.Explanation(item),
                    EsDeDiagnosticPresentation.NeedsAttention(item)));
            }
            if (snapshot.Diagnostics.Count > selected.Length)
                panel.Children.Add(Text((snapshot.Diagnostics.Count - selected.Length) +
                    " diagnostic(s) supplémentaire(s) · consulter Bibliothèques pour la synthèse.", 12));
        }
        if (compact)
            panel.Children.Add(Button("Consulter les diagnostics", () => Navigate(POCView.Libraries)));
        panel.Children.Add(Text("État constaté au dernier import ; un fichier introuvable n'est pas un fichier supprimé.", 12));
        return Panel(panel);
    }

    private Control ImportedSystemStrip()
    {
        var group = new StackPanel { Spacing = 12 };
        group.Children.Add(Text("Systèmes et consoles · références ES-DE", 22, true));
        var options = new WrapPanel { Orientation = Orientation.Horizontal, ItemWidth = 180 };
        options.Children.Add(Button("Tous les systèmes", () =>
        {
            _importedUi.SelectPlatform(null);
            Render();
            if (_contentScroll is not null) _contentScroll.Offset = default;
        }));
        foreach (var system in _importedUi.Systems)
        {
            var platform = system.Platform;
            options.Children.Add(Button(platform + " · " + system.ReferencedGames, () =>
            {
                if (_importedUi.SelectPlatform(platform))
                {
                    Render();
                    if (_contentScroll is not null) _contentScroll.Offset = default;
                }
            }));
        }
        group.Children.Add(options);
        return Panel(group);
    }

    private void ImportedCollection()
    {
        _page.Children.Add(Hero("Explorer  /  ES-DE", "Collection",
            "Jeux référencés dans les gamelists · jouabilité non vérifiée · recherche et filtres locaux."));
        var systems = _importedUi.Systems;
        var choices = new[] { "Tous les systèmes" }
            .Concat(systems.Select(s => s.Platform + " · " + s.ReferencedGames + " jeux ES-DE"))
            .ToArray();
        var selected = _importedUi.SelectedPlatform is null ? 0 :
            Array.FindIndex(systems.ToArray(), s =>
                string.Equals(s.Platform, _importedUi.SelectedPlatform,
                    StringComparison.OrdinalIgnoreCase)) + 1;
        var selector = new ComboBox
        {
            ItemsSource = choices, MinWidth = 230,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _page.Children.Add(selector);
        // Attach and select before subscribing: initial binding/layout is not user input.
        selector.SelectedIndex = selected;
        _liveImportedSystemSelector = selector;
        selector.SelectionChanged += (_, _) =>
        {
            if (!ReferenceEquals(selector, _liveImportedSystemSelector) ||
                ActiveView != POCView.Collection) return;
            var index = selector.SelectedIndex;
            if (index < 0 || index > systems.Count) return;
            var desired = index == 0 ? null : systems[index - 1].Platform;
            if (string.Equals(_importedUi.SelectedPlatform, desired, StringComparison.OrdinalIgnoreCase))
                return; // no re-render or page reset for an unchanged Avalonia value
            if (_importedUi.SelectPlatform(desired))
            {
                Render();
                if (_contentScroll is not null) _contentScroll.Offset = default;
            }
        };
        var search = new TextBox
        {
            PlaceholderText = "Rechercher titre, plateforme ou chemin ROM",
            Text = _importedUi.SearchText
        };
        _page.Children.Add(search);
        _liveImportedSearchBox = search;
        search.TextChanged += (_, _) =>
        {
            if (!ReferenceEquals(search, _liveImportedSearchBox) ||
                ActiveView != POCView.Collection ||
                string.Equals(search.Text ?? "", _importedUi.SearchText, StringComparison.Ordinal))
                return;
            _importedUi.Search(search.Text);
            FillImportedResults();
        };
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
        _importedList.Children.Add(Notice("Résultats du catalogue · " + count + " référence(s)",
            count + " résultat(s) importé(s) · page " + (_importedUi.PageIndex + 1) +
            " / " + _importedUi.PageCount + " · 50 éléments maximum par page."));
        var cards = new WrapPanel { Orientation = Orientation.Horizontal };
        var columns = ImportedColumnCount();
        var cardWidth = Math.Max(310, (_page.Width - (columns - 1) * 18 - 12) / columns);
        foreach (var game in _importedUi.PageGames)
        {
            var gameId = game.GameId;
            var line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 15 };
            line.Children.Add(CoverFor(game, 82, 108));
            var details = Group(
                Text(game.Platform.ToUpperInvariant() + " · ES-DE", 11, true),
                Text(game.Title, 19, true),
                Text(game.RomRelativePath, 12),
                Text(game.RomExists ? "ROM présente au dernier import" :
                    "ROM non trouvée — aucune suppression déduite", 12),
                PrimaryButton("Ouvrir la fiche", () => OpenImportedGame(gameId))
            );
            line.Children.Add(details);
            var card = Panel(line);
            card.Width = cardWidth;
            card.Margin = new Thickness(0, 0, 10, 10);
            cards.Children.Add(card);
        }
        _importedList.Children.Add(cards);
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
        _page.Children.Add(Hero("Fiche de jeu  /  " + game.Platform, game.Title,
            "Données déclarées par ES-DE · consultation en lecture seule."));
        _page.Children.Add(Notice(
            game.RomExists ? "ROM référencée et présente au dernier import" :
                "ROM référencée mais indisponible au dernier import",
            game.RomExists ? "La présence du fichier ne certifie pas sa jouabilité." :
                "Le fichier peut être absent ou le support déconnecté ; aucune suppression n'est déduite.",
            !game.RomExists));
        var metadata = Panel(Group(
            Text("Informations ES-DE", 20, true),
            Text("Plateforme : " + game.Platform, 18, true),
            Text("Variante : " + game.VariantId),
            Text("Genre (ES-DE) : " + ImportedCatalogueUiState.MetadataOrUnspecified(game.Genre)),
            Text("Développeur (ES-DE) : " + ImportedCatalogueUiState.MetadataOrUnspecified(game.Developer)),
            Text("Date de sortie (ES-DE) : " + ImportedCatalogueUiState.MetadataOrUnspecified(game.ReleaseDate)),
            Text("Description : " + (string.IsNullOrWhiteSpace(game.Description) ?
                "Non renseignée" : game.Description)),
            Text("ROM : " + game.RomRelativePath, 12),
            Text("Identité : " + game.GameId, 12)
        ));
        var artwork = Panel(Group(
            Text("Jaquette · média local déclaré", 19, true),
            CoverFor(game, 220, 265)
        ));
        if (Bounds.Width >= 1150)
        {
            var details = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("270,*"),
                RowDefinitions = new RowDefinitions("Auto"),
                ColumnSpacing = 14
            };
            details.Children.Add(artwork);
            Grid.SetColumn(metadata, 1);
            details.Children.Add(metadata);
            _page.Children.Add(details);
        }
        else
        {
            _page.Children.Add(artwork);
            _page.Children.Add(metadata);
        }
        _page.Children.Add(Text("Galerie locale · uniquement les médias déclarés dans le XML ES-DE", 19, true));
        var gallery = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var media in game.Media.Where(m => m.Kind is "image" or "thumbnail" or "marquee"))
            gallery.Children.Add(ImportedMediaPreview(media));
        if (gallery.Children.Count > 0) _page.Children.Add(gallery);
        _page.Children.Add(Text("Médias référencés", 19, true));
        if (game.Media.Count == 0) _page.Children.Add(Text("Médias non renseignés dans le XML : état inconnu, pas absence de fichiers prouvée.", 13));
        _page.Children.Add(Text("Captures, dos et fonds non exposés par le XML importé ; aucun visuel inventé. Vidéos référencées : lecture non prise en charge.", 12));
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
        _page.Children.Add(Hero("Sources  /  lecture seule", "Bibliothèques",
            "Importer, consulter et actualiser une source ES-DE sans écrire dans les dossiers."));
        _page.Children.Add(Panel(Group(
            Text("Bibliothèque importée (session actuelle)", 22, true),
            Text(snapshot.Games.Count + " jeu(x) · " + snapshot.Diagnostics.Count + " diagnostic(s)", 17, true),
            Text("Identité de session : " + snapshot.LibraryId, 12),
            PrimaryButton("Choisir une bibliothèque ES-DE…", () => _ = ChooseLibraryAsync()),
            Button("Actualiser l’import ES-DE", () => _ = RefreshImportAsync()),
            Text("Catalogue en mémoire uniquement : réimport nécessaire au prochain lancement.", 12)
        )));
        _page.Children.Add(ImportedDiagnostics(snapshot, compact: false));
        _page.Children.Add(Panel(Group(
            Text("Inventaire et simulations", 22, true),
            Text("Inventaire NEXT-03 : scan de vos dossiers non autorisé dans cette version.", 14, true),
            Text("Les références ES-DE ne prouvent pas un inventaire physique complet.", 12),
            Button("Inventaire NEXT-03 · Aperçu (SIMULATION)", () =>
                new InventoryPreviewWindow().Show(this)),
            Text("L'aperçu NEXT-03 est synthétique : aucune lecture ni autorisation de scan personnel.", 12),
            Text("Comparaison A/B désactivée pour ce catalogue importé.", 16, true),
            Text("Les snapshots A/B du mode démonstration sont fictifs et indépendants de votre bibliothèque.", 13)
        )));
    }

#if NEXT04_CI
    private double _ciNext04CollectionOffsetY;
    private string? _ciNext04SelectedGame;
    // Test-only entrypoints for the real Avalonia tree; never compiled in the public ZIP.
    internal bool CiNext04Import(string syntheticRoot, int games, int systems)
    {
        if (!_importedUi.TryImport(syntheticRoot) || !(_importedUi.Snapshot?.Games.Count == games) ||
            _importedUi.Systems.Count != systems) return false;
        Render();
        return true;
    }

    internal bool CiNext04FirstSystem()
    {
        var system = _importedUi.Systems.FirstOrDefault();
        if (system is null || !_importedUi.SelectPlatform(system.Platform)) return false;
        Render();
        return _importedUi.PageGames.All(g => string.Equals(g.Platform, system.Platform,
            StringComparison.OrdinalIgnoreCase)) && _importedUi.TotalCount == system.ReferencedGames;
    }

    internal bool CiNext04OpenFirst()
    {
        var game = _importedUi.PageGames.FirstOrDefault();
        if (game is null) return false;
        OpenImportedGame(game.GameId);
        return _importedUi.CurrentView == POCView.GameDetail &&
            _importedUi.SelectedGameId == game.GameId;
    }

    internal bool CiNext04ReturnCollection()
    {
        var platform = _importedUi.SelectedPlatform;
        Navigate(POCView.Collection);
        return _importedUi.CurrentView == POCView.Collection &&
            string.Equals(_importedUi.SelectedPlatform, platform, StringComparison.OrdinalIgnoreCase);
    }

    internal bool CiNext04RejectMystery(string syntheticRoot)
    {
        var lastGood = _importedUi.Snapshot;
        var rejected = !_importedUi.TryImport(syntheticRoot);
        Render();
        return rejected && ReferenceEquals(lastGood, _importedUi.Snapshot) &&
            _importedUi.IsImported &&
            _importedUi.ErrorMessage?.Contains("Dernier catalogue valide conservé",
                StringComparison.Ordinal) == true;
    }

    // 155 dedicated synthetic entries -> 50-card second page and nonzero real viewport scroll.
    internal bool CiNext04PrepareDeep(string fixture)
    {
        if (!_importedUi.TryImport(fixture) || _importedUi.Snapshot?.Games.Count != 155)
            return false;
        if (!_importedUi.SelectPlatform("gba")) return false;
        _importedUi.Search("Game ");
        if (!_importedUi.NextPage() || _importedUi.PageIndex != 1 ||
            _importedUi.PageGames.Count != 50) return false;
        Render();
        return _importedUi.CurrentView == POCView.Collection;
    }

    internal bool CiNext04DeepOpen()
    {
        if (_contentScroll is null || Content is not Control visual) return false;
        visual.Measure(new Size(1118, 718));
        visual.Arrange(new Rect(0, 0, 1118, 718));
        var available = _contentScroll.Extent.Height - _contentScroll.Viewport.Height;
        Console.WriteLine("NEXT04_DEEP_SCROLL_MEASURE extent=" + _contentScroll.Extent.Height +
            " viewport=" + _contentScroll.Viewport.Height + " available=" + available);
        if (available < 320) return false;
        _contentScroll.Offset = new Vector(0, Math.Min(490, available - 10));
        _ciNext04CollectionOffsetY = _contentScroll.Offset.Y;
        Console.WriteLine("NEXT04_DEEP_SCROLL_BEFORE_DETAIL offset=" + _ciNext04CollectionOffsetY);
        if (_ciNext04CollectionOffsetY < 200) return false;
        var target = _importedUi.PageGames[0];
        Console.WriteLine("NEXT04_DEEP_PAGE_CHECK page=" + _importedUi.PageIndex +
            " search=" + _importedUi.SearchText + " system=" + _importedUi.SelectedPlatform);
        if (_importedUi.PageIndex != 1) return false;
        Console.WriteLine("NEXT04_DEEP_FIRST_GAME title=" + target.Title + " metadata=" +
            target.Genre + "/" + target.Developer + "/" + target.ReleaseDate);
        _ciNext04SelectedGame = target.GameId;
        OpenImportedGame(target.GameId);
        var visible = SmokeVisibleTexts();
        var genre = visible.Contains("Genre (ES-DE) : Exploration", StringComparison.Ordinal);
        var developer = visible.Contains("Développeur (ES-DE) : Test Studio", StringComparison.Ordinal);
        var date = visible.Contains("Date de sortie (ES-DE) : 20240101", StringComparison.Ordinal);
        Console.WriteLine("NEXT04_DEEP_DETAIL_CHECK view=" + _importedUi.CurrentView +
            " selected=" + (_importedUi.SelectedGameId == target.GameId) +
            " genre=" + genre + " developer=" + developer + " date=" + date);
        return _importedUi.CurrentView == POCView.GameDetail &&
               _importedUi.SelectedGameId == target.GameId && genre && developer && date;
    }

    internal async Task<bool> CiNext04DeepReimportWhileOnDetail()
    {
        if (_importedUi.CurrentView != POCView.GameDetail) return false;
        var before = _importedUi.Snapshot;
        await RefreshImportAsync();
        return _importedUi.CurrentView == POCView.GameDetail &&
            _importedUi.PageIndex == 1 &&
            _importedUi.SearchText == "Game " &&
            _importedUi.SelectedPlatform == "gba" &&
            _importedUi.SelectedGameId == _ciNext04SelectedGame &&
            ReferenceEquals(before, _importedUi.Snapshot) == false;
    }

    internal bool CiNext04DeepReturn()
    {
        if (_importedUi.CurrentView != POCView.GameDetail) return false;
        Navigate(POCView.Collection);
        return _importedUi.CurrentView == POCView.Collection &&
            _importedUi.PageIndex == 1 &&
            _importedUi.SearchText == "Game " &&
            _importedUi.SelectedPlatform == "gba";
    }

    internal bool CiNext04DeepOffsetRestored()
    {
        if (_contentScroll is null || _importedUi.CurrentView != POCView.Collection)
            return false;
        return _ciNext04CollectionOffsetY >= 200 &&
            Math.Abs(_contentScroll.Offset.Y - _ciNext04CollectionOffsetY) <= 8;
    }

    internal async Task<bool> CiNext04DeepReimportWhileOnCollection()
    {
        await RefreshImportAsync();
        return _importedUi.CurrentView == POCView.Collection &&
            _importedUi.PageIndex == 1 &&
            _importedUi.SearchText == "Game " &&
            _importedUi.SelectedPlatform == "gba";
    }

    internal async Task<bool> CiNext04DeepMalformedXmlPreservesLastGood(string fixture)
    {
        var xml = Path.Combine(fixture, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml");
        var saved = File.ReadAllBytes(xml);
        var previous = _importedUi.Snapshot;
        try
        {
            File.WriteAllText(xml, "<gameList><game><path>", new System.Text.UTF8Encoding(false));
            await RefreshImportAsync();
            return ReferenceEquals(previous, _importedUi.Snapshot) &&
                _importedUi.CurrentView == POCView.Collection &&
                _importedUi.PageIndex == 1 &&
                _importedUi.SearchText == "Game " &&
                _importedUi.SelectedPlatform == "gba" &&
                _importedUi.ErrorMessage?.Contains("Dernier catalogue valide conservé",
                    StringComparison.Ordinal) == true;
        }
        finally { File.WriteAllBytes(xml, saved); }
    }

    internal bool CiNext04MissingMetadataAndInventoryLink()
    {
        // The second entry on the deep page has no optional metadata.
        var other = _importedUi.PageGames.Skip(1).FirstOrDefault();
        if (other is null) return false;
        OpenImportedGame(other.GameId);
        var labels = SmokeVisibleTexts();
        if (!labels.Contains("Genre (ES-DE) : Non renseigné", StringComparison.Ordinal) ||
            !labels.Contains("Développeur (ES-DE) : Non renseigné", StringComparison.Ordinal) ||
            !labels.Contains("Date de sortie (ES-DE) : Non renseigné", StringComparison.Ordinal))
            return false;
        _importedUi.Navigate(POCView.Libraries);
        Render();
        var libraries = SmokeVisibleTexts();
        return libraries.Contains("L\'aperçu NEXT-03 est synthétique", StringComparison.Ordinal) &&
            libraries.Contains("Inventaire NEXT-03 · Aperçu (SIMULATION)", StringComparison.Ordinal) &&
            libraries.Contains("Choisir une bibliothèque ES-DE…", StringComparison.Ordinal) &&
            libraries.Contains("Actualiser l’import ES-DE", StringComparison.Ordinal);
    }
#endif
}
