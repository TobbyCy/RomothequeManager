using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace RomothequeManager.V2;

// First visible V2-01B slice. The typed snapshot projection and A/B inspector follow.
public static class LibraryPage
{
    private static readonly IBrush Ink = new SolidColorBrush(Color.Parse("#E9EEF6"));
    private static readonly IBrush Muted = new SolidColorBrush(Color.Parse("#A9B8CD"));
    private static readonly IBrush Surface = new SolidColorBrush(Color.Parse("#202B3D"));

    public static Control Build(ReadOnlyCatalogue catalogue)
    {
        var root = new StackPanel { Spacing = 12 };
        root.Children.Add(new TextBlock
        {
            Text = "Démonstration synthétique · lecture seule. Les observations ne prouvent pas la complétude d'un support.",
            Foreground = Muted, TextWrapping = TextWrapping.Wrap
        });
        foreach (var group in catalogue.Games.GroupBy(game => game.Library).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var content = new StackPanel { Spacing = 6 };
            content.Children.Add(new TextBlock
            {
                Text = group.Key, FontSize = 20, FontWeight = FontWeight.Bold, Foreground = Ink
            });
            content.Children.Add(new TextBlock
            {
                Text = group.Count() + " jeux fictifs connus dans le catalogue de démonstration",
                Foreground = Muted, TextWrapping = TextWrapping.Wrap
            });
            content.Children.Add(new TextBlock
            {
                Text = "Snapshots observés : " + string.Join(", ", group.Select(game => game.SnapshotId).Distinct(StringComparer.Ordinal)),
                Foreground = Muted, TextWrapping = TextWrapping.Wrap
            });
            root.Children.Add(new Border
            {
                Background = Surface, CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20), Child = content
            });
        }
        return root;
    }
}
