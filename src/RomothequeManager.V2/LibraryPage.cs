using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RomothequeManager.V2;

// Hybrid list/inspector over a qualified synthetic projection. No comparison logic lives here.
public static class LibraryPage
{
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#E9EEF6"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#A9B8CD"));
    private static readonly IBrush Surface = new SolidColorBrush(Color.Parse("#202B3D"));

    private static readonly (string Key, string Label)[] ResultLabels =
    [
        ("ADDED", "Ajouté dans l'inventaire B"),
        ("CHANGED", "Observation différente"),
        ("UNCHANGED", "Observation identique"),
        ("NOT_OBSERVED_IN_B_COVERED_SCOPE", "Non retrouvé dans l'inventaire B (zone couverte)"),
        ("UNKNOWN", "Inconnu : preuve insuffisante"),
        ("NOT_COMPARABLE", "Non comparable")
    ];

    public static Control Build(LibraryProjection projection, LibraryViewState state,
        Action refresh, bool compact)
    {
        var root = new StackPanel { Spacing = 14 };
        root.Children.Add(Text("Données 100 % fictives · lecture seule · aucun scan ni accès à une ROM.", 14));
        root.Children.Add(Text("Les nombres de jeux du Dashboard sont distincts des unités observées dans les snapshots.", 13));

        var layout = new Grid
        {
            ColumnDefinitions = compact ? new ColumnDefinitions("*") : new ColumnDefinitions("260,*"),
            RowDefinitions = compact ? new RowDefinitions("Auto,Auto") : new RowDefinitions("Auto")
        };
        var list = new StackPanel { Spacing = 10, Margin = compact ? new Thickness(0, 0, 0, 12) : new Thickness(0, 0, 14, 0) };
        list.Children.Add(Text("Bibliothèques fictives", 19, true));
        foreach (var library in projection.Libraries)
        {
            var selected = library.LibraryId == state.SelectedLibraryId;
            var card = Group(
                Text(library.Name, 17, true),
                Text("Identité logique : " + library.LibraryId, 12),
                Text("Disponibilité : " + library.CurrentAvailability + " · Fraîcheur : " + library.Freshness, 12),
                Text(selected ? "Sélectionnée — ouvrir l'inspecteur" : "Afficher l'inspecteur", 12, selected));
            var button = new Button
            {
                Content = card, HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(12), MinHeight = 72,
                Background = selected ? new SolidColorBrush(Color.Parse("#29465A")) : Surface
            };
            button.Click += (_, _) => { state.SelectLibrary(library.LibraryId); refresh(); };
            list.Children.Add(button);
        }
        layout.Children.Add(list);

        var inspector = Panel(BuildInspector(state, refresh));
        Grid.SetColumn(inspector, compact ? 0 : 1);
        Grid.SetRow(inspector, compact ? 1 : 0);
        layout.Children.Add(inspector);
        root.Children.Add(layout);
        return root;
    }

    private static Control BuildInspector(LibraryViewState state, Action refresh)
    {
        var library = state.SelectedLibrary;
        if (library is null) return Text("Aucune bibliothèque sélectionnée.", 16);
        var panel = new StackPanel { Spacing = 12 };
        panel.Children.Add(Text(library.Name + " — inspecteur", 22, true));
        panel.Children.Add(Text("Source : " + library.CurrentAvailability, 15, true));
        panel.Children.Add(Text("Fraîcheur : " + library.Freshness + " · Index : " +
            (library.IndexAccessible ? "ACCESSIBLE" : "INACCESSIBLE"), 14));
        panel.Children.Add(Text("Disponibilité observée : " +
            (library.AvailabilityObservedAtUtc ?? "NON OBSERVÉE"), 13));
        panel.Children.Add(Text("Dernier snapshot complet vérifié : " +
            (library.LastVerifiedCompleteRef ?? "AUCUN"), 14, true));
        var lastAttempt = library.Generations.OrderByDescending(item => item.CapturedAtUtc,
            StringComparer.Ordinal).FirstOrDefault();
        panel.Children.Add(Text("Dernière tentative : " +
            (lastAttempt is null ? "INCONNUE" :
                lastAttempt.SnapshotId + " · " + lastAttempt.ScanStatus + " · " + lastAttempt.CapturedAtUtc), 13));

        panel.Children.Add(Text("Historique des snapshots", 18, true));
        foreach (var generation in library.Generations.OrderBy(item => item.CapturedAtUtc, StringComparer.Ordinal))
        {
            var observedCount = generation.CountKnown && generation.ObservedUnitCount is not null
                ? generation.ObservedUnitCount.Value.ToString() : "INCONNU";
            var coverage = string.Join(" · ", generation.Coverage.Select(item =>
                item.Console + ":" + item.Status));
            panel.Children.Add(Panel(Group(
                Text(generation.SnapshotId, 15, true),
                Text("Génération : " + generation.GenerationId + " · scan : " + generation.ScanStatus, 12),
                Text("Capturé : " + generation.CapturedAtUtc, 12),
                Text("Unités observées : " + observedCount + " (non assimilées au total de jeux)", 12),
                Text("Couverture : " + coverage, 12),
                Text("Provenance : " + generation.Provenance, 12)
            )));
        }

        panel.Children.Add(Text("Comparaison A / B", 18, true));
        if (!library.IndexAccessible)
        {
            panel.Children.Add(Text("Index inaccessible : nombre d'unités inconnu et comparaison indisponible.", 14, true));
            return panel;
        }
        var snapshotIds = library.Generations.OrderBy(item => item.CapturedAtUtc, StringComparer.Ordinal)
            .Select(item => item.SnapshotId).ToArray();
        var selectors = new StackPanel { Spacing = 8 };
        selectors.Children.Add(Text("Snapshot A (avant)", 13, true));
        var before = new ComboBox
        {
            ItemsSource = snapshotIds, SelectedItem = state.BeforeSnapshotId,
            HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 38
        };
        before.SelectionChanged += (_, _) => state.SetBefore(before.SelectedItem as string);
        selectors.Children.Add(before);
        selectors.Children.Add(Text("Snapshot B (après)", 13, true));
        var after = new ComboBox
        {
            ItemsSource = snapshotIds, SelectedItem = state.AfterSnapshotId,
            HorizontalAlignment = HorizontalAlignment.Stretch, MinHeight = 38
        };
        after.SelectionChanged += (_, _) => state.SetAfter(after.SelectedItem as string);
        selectors.Children.Add(after);
        var compare = new Button
        {
            Content = "Comparer A / B (résultats préqualifiés)",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Padding = new Thickness(12), MinHeight = 40
        };
        compare.Click += (_, _) => { state.TryCompare(); refresh(); };
        selectors.Children.Add(compare);
        panel.Children.Add(selectors);

        if (state.ErrorMessage is not null)
            panel.Children.Add(Text("Comparaison refusée : " + state.ErrorMessage, 14, true));
        if (state.CurrentComparison is not null)
            panel.Children.Add(BuildComparison(state.CurrentComparison));
        return panel;
    }

    private static Control BuildComparison(UiComparison comparison)
    {
        var content = new StackPanel { Spacing = 8 };
        content.Children.Add(Text("Résultat de comparaison synthétique", 18, true));
        content.Children.Add(Text("Couverture : " + comparison.CoverageStatus, 14, true));
        content.Children.Add(Text("Dénominateur : " + comparison.CountDenominator +
            " unités distinctes observées dans A∪B, pas des fichiers physiques supprimés.", 13));
        foreach (var item in ResultLabels)
            content.Children.Add(Text(item.Label + " : " + comparison.Counts[item.Key], 13));
        content.Children.Add(Text("Périmètre prouvé : " +
            (comparison.ScopeCompared.Count == 0 ? "AUCUN" :
                string.Join(", ", comparison.ScopeCompared.Select(item => item.Console))), 13));
        content.Children.Add(Text("Périmètre exclu : " +
            (comparison.ScopeNotComparable.Count == 0 ? "AUCUN" :
                string.Join(", ", comparison.ScopeNotComparable.Select(item => item.Console +
                    " (" + item.BeforeStatus + " / " + item.AfterStatus + ")"))), 13));
        content.Children.Add(Text("Limitations : " + string.Join(", ", comparison.Limitations), 12));
        content.Children.Add(Text("Observations détaillées", 15, true));
        foreach (var entry in comparison.Entries)
        {
            var label = ResultLabels.Single(item => item.Key == entry.Classification).Label;
            content.Children.Add(Text(entry.Console + " · " + entry.RomUnitId + " — " +
                label + " [" + entry.Reason + "]", 12));
        }
        content.Children.Add(Text("Une absence dans un inventaire couvert n'est pas une preuve d'effacement physique.", 13, true));
        return Panel(content);
    }

    private static TextBlock Text(string value, double size, bool bold = false) => new()
    {
        Text = value, FontSize = size, Foreground = bold ? Ink : Muted,
        TextWrapping = TextWrapping.Wrap,
        FontWeight = bold ? FontWeight.Bold : FontWeight.Normal
    };

    private static Border Panel(Control content) => new()
    {
        Background = Surface, CornerRadius = new CornerRadius(12),
        Padding = new Thickness(16), Child = content
    };

    private static StackPanel Group(params Control[] controls)
    {
        var group = new StackPanel { Spacing = 6 };
        foreach (var item in controls) group.Children.Add(item);
        return group;
    }
}
