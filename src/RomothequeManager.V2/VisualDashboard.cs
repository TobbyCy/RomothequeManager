using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RomothequeManager.V2;

/// <summary>Read-only Dashboard projection; numbers are never inferred from unavailable sources.</summary>
public sealed record VisualDashboardNumbers(
    int ReferencedGames, int KnownPlatforms, int KnownLibraries, int Diagnostics,
    int RomUnavailable, bool Synthetic, string Provenance);

public static class VisualDashboardData
{
    public static VisualDashboardNumbers Demo(ReadOnlyCatalogue catalogue, LibraryProjection libraries) =>
        new(catalogue.Games.Count,
            catalogue.Games.Select(x => x.Platform).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            libraries.Libraries.Count, 0, 0, true, "Simulation 100 % fictive");

    public static VisualDashboardNumbers Imported(EsDeCatalogueSnapshot snapshot) =>
        new(snapshot.Games.Count,
            snapshot.Games.Select(x => x.Platform).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            1, snapshot.Diagnostics.Count, snapshot.Games.Count(x => !x.RomExists),
            false, "ES-DE · données référencées, lecture seule");
}

/// <summary>
/// Dashboard visual-only: no scan, export, storage, job or network requests.
/// All imagery is either a locally opened ES-DE medium or an original text placeholder.
/// </summary>
public static class VisualDashboard
{
    private static readonly IBrush Base = Solid("#16181D");
    private static readonly IBrush Card = Solid("#23262B");
    private static readonly IBrush BorderInk = Solid("#393C42");
    private static readonly IBrush Primary = Solid("#E8E6E3");
    private static readonly IBrush Muted = Solid("#AAB2BF");
    private static readonly IBrush Red = Solid("#B3202E");
    private static readonly IBrush Amber = Solid("#E4B36C");
    private static readonly IBrush Soft = Solid("#2D3036");
    private static IBrush Solid(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static TextBlock Label(string text, double size = 13, bool bold = false, IBrush? color = null) => new()
    {
        Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap,
        Foreground = color ?? (bold ? Primary : Muted),
        FontWeight = bold ? FontWeight.SemiBold : FontWeight.Normal
    };
    private static StackPanel Stack(params Control[] items)
    {
        var panel = new StackPanel { Spacing = 8 };
        foreach (var item in items) panel.Children.Add(item);
        return panel;
    }
    private static Border Tile(Control child, double minHeight = 0, IBrush? background = null) => new()
    {
        Background = background ?? Card,
        BorderBrush = BorderInk,
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(16),
        MinHeight = minHeight,
        Child = child
    };
    private static Button Action(string text, Action handler)
    {
        var button = new Button
        {
            Content = text, Background = Red, Foreground = Primary,
            BorderThickness = new Thickness(0), Padding = new Thickness(14, 8),
            HorizontalAlignment = HorizontalAlignment.Left, MinHeight = 36
        };
        button.Click += (_, _) => handler();
        return button;
    }
    private static Border Placeholder(string title) => new()
    {
        Width = 112, Height = 92,
        Background = Soft,
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(8),
        Child = Stack(
            Label("◆", 22, true, Red),
            Label(title, 11, true, Primary))
    };

    private static Control Header(string subtitle)
    {
        var titleRow = new StackPanel { Spacing = 2 };
        titleRow.Children.Add(Label("TABLEAU DE BORD", 29, true, Primary));
        titleRow.Children.Add(Label(subtitle, 13));
        return titleRow;
    }

    public static Control[] BuildDemo(ReadOnlyCatalogue catalogue,
        LibraryProjection projection, Action openCollection, Action openLibraries, bool compact)
    {
        var numbers = VisualDashboardData.Demo(catalogue, projection);
        var previews = catalogue.Games.Take(3).Select(game =>
            (Control)Placeholder(game.Title)).ToArray();
        var status = Stack(
            Label(projection.Libraries.Count + " bibliothèques entièrement fictives", 16, true),
            Label("États de démonstration : ONLINE · OFFLINE · UNKNOWN"),
            Label("Une source OFFLINE ne signifie pas que ses jeux ont été supprimés."),
            Action("Voir les bibliothèques", openLibraries));
        return Build(numbers,
            "Démo fictive · lecture seule · aucun scan",
            previews, status, "Aucune anomalie physique contrôlée : valeurs de démonstration uniquement.",
            openCollection, compact);
    }

    public static Control[] BuildImported(EsDeCatalogueSnapshot snapshot, Func<EsDeGame, Control> cover,
        Action openCollection, Action openLibraries, bool compact)
    {
        var numbers = VisualDashboardData.Imported(snapshot);
        var previews = snapshot.Games.Take(3).Select(game =>
        {
            var row = new StackPanel { Spacing = 3, Width = 110 };
            row.Children.Add(cover(game));
            row.Children.Add(Label(game.Title, 11, true, Primary));
            return (Control)row;
        }).ToArray();
        var status = Stack(
            Label("1 bibliothèque ES-DE sélectionnée", 16, true),
            Label("Disponibilité physique actuelle : NON OBSERVÉE", 13, true, Amber),
            Label("Aucun scan complet et aucune vérification de disque effectués."),
            Label("Identité et lecture limitées à la session importée."),
            Action("Voir les bibliothèques", openLibraries));
        var attention = snapshot.Diagnostics.Count == 0
            ? "Aucun diagnostic remonté par cet import XML. Cela ne certifie pas une bibliothèque complète."
            : EsDeDiagnosticPresentation.Summary(snapshot.Diagnostics) + " " +
              string.Join(" · ", snapshot.Diagnostics.Take(2)
                  .Select(EsDeDiagnosticPresentation.Title));
        return Build(numbers,
            "Bibliothèque ES-DE importée · métadonnées lues sur demande · aucun scan physique",
            previews, status, attention, openCollection, compact);
    }

    private static Control[] Build(VisualDashboardNumbers numbers, string subtitle,
        IReadOnlyList<Control> preview, Control support, string diagnostic,
        Action openCollection, bool compact)
    {
        var previewRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        foreach (var item in preview) previewRow.Children.Add(item);
        var collection = Tile(Stack(
            Label("MA COLLECTION", 12, true, Red),
            Label(numbers.ReferencedGames + " jeux référencés  ·  " +
                  numbers.KnownPlatforms + " plateformes connues", 21, true, Primary),
            Label(numbers.Synthetic
                ? "Jeux inventés pour la démonstration ; aucune couverture réelle déduite."
                : "Éléments du XML ES-DE uniquement ; la couverture totale reste inconnue."),
            preview.Count == 0 ? Label("Aucun jeu référencé dans les gamelists disponibles.") : previewRow,
            Action("Parcourir la collection", openCollection)
        ), 240);
        var library = Tile(Stack(
            Label("BIBLIOTHÈQUES ET SUPPORTS", 12, true, Red),
            support), 240);
        var exportButton = new Button
        {
            Content = "Préparer un export — indisponible",
            IsEnabled = false,
            Padding = new Thickness(12, 7),
            HorizontalAlignment = HorizontalAlignment.Left
        };
        var export = Tile(Stack(
            Label("PRÉPARATIONS D'EXPORT", 12, true, Red),
            Label("Fonctionnalité à venir — aucun plan ni copie n'est exécuté.", 14),
            exportButton), 128);
        var attention = Tile(Stack(
            Label("POINTS D'ATTENTION", 12, true, Red),
            Label(diagnostic, 13),
            Label(numbers.RomUnavailable > 0
                ? numbers.RomUnavailable + " ROM(s) indisponible(s) au dernier import ; aucune suppression déduite."
                : "Aucune conclusion physique à partir d'un inventaire ancien ou partiel.", 12)),
            128);
        var first = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "1.3*,1*"),
            RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto"),
            ColumnSpacing = 12, RowSpacing = 12
        };
        first.Children.Add(collection);
        Grid.SetColumn(library, compact ? 0 : 1);
        Grid.SetRow(library, compact ? 1 : 0);
        first.Children.Add(library);
        var second = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions(compact ? "*" : "1*,1.3*"),
            RowDefinitions = new RowDefinitions(compact ? "Auto,Auto" : "Auto"),
            ColumnSpacing = 12, RowSpacing = 12
        };
        second.Children.Add(export);
        Grid.SetColumn(attention, compact ? 0 : 1);
        Grid.SetRow(attention, compact ? 1 : 0);
        second.Children.Add(attention);
        return [Header(subtitle), first, second];
    }
}
