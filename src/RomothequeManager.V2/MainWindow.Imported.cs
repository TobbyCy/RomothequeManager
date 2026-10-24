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
    private POCView _importView = POCView.Dashboard;

    private POCView ActiveView => _importedUi.IsImported ? _importView : _state.CurrentView;

    private void Navigate(POCView view)
    {
        if (_importedUi.IsImported)
            _importView = view;
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
        _importedUi.ShowDemo();
        _state.ShowDashboard();
        Render();
    }

    private void ShowImported()
    {
        if (_importedUi.ShowImported()) _importView = POCView.Dashboard;
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
            if (success) _importView = POCView.Dashboard;
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

    private void RenderImported()
    {
        var snapshot = _importedUi.Snapshot;
        if (snapshot is null)
        {
            _page.Children.Add(Text("Aucune bibliothèque importée.", 18, true));
            return;
        }
        _page.Children.Add(Text("BIBLIOTHÈQUE IMPORTÉE · ES-DE · LECTURE SEULE", 13, true));
        _page.Children.Add(Text("Cette vue reflète uniquement les gamelists trouvées. Aucun scan complet, aucune suppression déduite.", 13));
        switch (_importView)
        {
            case POCView.Dashboard: ImportedDashboard(snapshot); break;
            case POCView.Collection: ImportedCollection(); break;
            case POCView.GameDetail: ImportedDetail(); break;
            case POCView.Libraries: ImportedLibraries(snapshot); break;
        }
    }

    private void ImportedDashboard(EsDeCatalogueSnapshot snapshot)
    {
        _page.Children.Add(Text("Tableau de bord", 30, true));
        var romMissing = snapshot.Games.Count(x => !x.RomExists);
        var platforms = snapshot.Games.Select(x => x.Platform).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        _page.Children.Add(Panel(Group(
            Text(snapshot.Games.Count + " jeu(x) référencé(s)", 25, true),
            Text(platforms + " plateforme(s) · " + romMissing + " ROM(s) indisponible(s) au dernier import"),
            Text(snapshot.Diagnostics.Count + " diagnostic(s) · origine " + snapshot.SourceKind),
            Text("Un fichier indisponible ou un support retiré ne signifie pas qu'un jeu a été supprimé.", 14, true)
        )));
        if (snapshot.Diagnostics.Count > 0)
        {
            _page.Children.Add(Text("Diagnostics de l'import", 20, true));
            foreach (var diagnostic in snapshot.Diagnostics.Take(8))
                _page.Children.Add(Text(diagnostic.Code + " · " + diagnostic.Platform +
                    " · " + diagnostic.Entry, 13));
            if (snapshot.Diagnostics.Count > 8)
                _page.Children.Add(Text("Diagnostics supplémentaires : " + (snapshot.Diagnostics.Count - 8), 13));
        }
        _page.Children.Add(Button("Parcourir la collection", () => Navigate(POCView.Collection)));
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
                        _importView = POCView.GameDetail;
                        Render();
                    }
                })
            );
            line.Children.Add(details);
            _importedList.Children.Add(Panel(line));
        }
        var pages = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        pages.Children.Add(Button("← Précédente", () =>
        {
            if (_importedUi.PreviousPage()) FillImportedResults();
        }));
        pages.Children.Add(Button("Suivante →", () =>
        {
            if (_importedUi.NextPage()) FillImportedResults();
        }));
        _importedList.Children.Add(pages);
    }

    private void ImportedDetail()
    {
        var game = _importedUi.Selected;
        if (game is null)
        {
            _importView = POCView.Collection;
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
        _page.Children.Add(Text("Médias référencés", 19, true));
        if (game.Media.Count == 0) _page.Children.Add(Text("Aucun média déclaré dans le XML.", 13));
        foreach (var media in game.Media)
            _page.Children.Add(Text(media.Kind + " · " + media.RelativePath + " · " +
                (media.Exists ? "présent au dernier import (lecture à vérifier)" : "absent au dernier import"), 13));
        if (game.DiscPaths.Count > 0)
        {
            _page.Children.Add(Text("Disques référencés dans la playlist", 19, true));
            foreach (var disc in game.DiscPaths) _page.Children.Add(Text(disc, 13));
        }
    }

    private Control CoverFor(EsDeGame game, double width, double height)
    {
        var media = game.Media.FirstOrDefault(x => x.Kind == "image")
            ?? game.Media.FirstOrDefault(x => x.Kind == "thumbnail");
        if (media is null) return Text("Jaquette non renseignée", 13);
        if (!_importedUi.TryOpenMedia(media, out var stream))
            return Text("Jaquette absente / indisponible", 13);
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
            return Text("Jaquette illisible (format ou données invalides)", 13);
        }
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
