using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;

namespace RomothequeManager.V2.MvvmFoundation;

// Isolated Avalonia preview until #75/#76/#77 are integrated and DEV 1 service
// is accepted. This NEVER scans user content: injected service is simulation-only.
public partial class InventoryPreviewWindow : Window
{
    private readonly InventoryPreviewService _preview = new();
    private readonly InventoryViewModel _vm;
    private Bitmap? _brand;

    public InventoryPreviewWindow()
    {
        InitializeComponent();
        _vm = new InventoryViewModel(_preview, action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        });
        DataContext = _vm;

        var dir = AppContext.BaseDirectory;
        var iconFile = Path.Combine(dir, "RM_V2_Favicon.ico");
        var logoFile = Path.Combine(dir, "RM_V2_Horizontal_Fond_Sombre.png");
        if (File.Exists(iconFile)) Icon = new WindowIcon(iconFile);
        if (File.Exists(logoFile))
        {
            _brand = new Bitmap(logoFile);
            var image = this.FindControl<Image>("BrandImage");
            if (image is not null) image.Source = _brand;
        }
        Closed += async (_, _) =>
        {
            _vm.Dispose();
            _brand?.Dispose();
            await _preview.DisposeAsync();
        };
    }

    private async void ChooseFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_vm.CanChangeFolder) return;
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions
                {
                    Title = "NEXT-03 · sélectionner un dossier (simulation : aucun scan)",
                    AllowMultiple = false
                });
            var folder = folders.FirstOrDefault();
            if (folder is null || !folder.Path.IsFile) return;
            var path = folder.Path.LocalPath;
            // The VM delegates syntax validation to preview service, and DEV 1
            // will later own bounded preflight / fixture-only authority.
            _vm.PrepareSelectedFolder(path, new InventoryScope(path));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            ArgumentException or IOException or InvalidOperationException)
        {
            // Do not expose a private absolute path from exception messages.
            // The only action on this dialog is to select; no scan has started.
        }
    }

    private void PreviewCase_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is not ComboBox { SelectedIndex: >= 0 } combo) return;
        var mode = combo.SelectedIndex switch
        {
            1 => InventoryPreviewOutcome.Partial,
            2 => InventoryPreviewOutcome.Failed,
            3 => InventoryPreviewOutcome.Offline,
            4 => InventoryPreviewOutcome.Stale,
            _ => InventoryPreviewOutcome.Complete
        };
        _preview.SelectPreviewOutcome(mode);
    }
}
