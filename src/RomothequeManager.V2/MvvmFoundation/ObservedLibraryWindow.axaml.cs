using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

namespace RomothequeManager.V2.MvvmFoundation;

// UI-only composition, native folder picker and dispatcher. Scanner/file classification
// never live in this Window. Production construction is always deny-by-default.
public partial class ObservedLibraryWindow : Window
{
    private readonly IInventoryApplicationService _service;
    private readonly IReadOnlyDictionary<string, string>? _syntheticRoots;
    private readonly InventoryViewModel _inventoryVm;
    private readonly ObservedLibraryViewModel _vm;
    private Bitmap? _brand;

    public ObservedLibraryWindow() : this(new InventoryApplicationService(), null) { }

    private ObservedLibraryWindow(IInventoryApplicationService service,
        IReadOnlyDictionary<string, string>? syntheticRoots)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _syntheticRoots = syntheticRoots;
        InitializeComponent();
        _inventoryVm = new InventoryViewModel(_service, action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        });
        _vm = new ObservedLibraryViewModel(_inventoryVm, _syntheticRoots is not null);
        DataContext = _vm;

        var root = AppContext.BaseDirectory;
        var icon = Path.Combine(root, "RM_V2_Favicon.ico");
        var logo = Path.Combine(root, "RM_V2_Horizontal_Fond_Sombre.png");
        if (File.Exists(icon)) Icon = new WindowIcon(icon);
        if (File.Exists(logo))
        {
            _brand = new Bitmap(logo);
            this.FindControl<Image>("OfficialLogo")!.Source = _brand;
        }

        if (_syntheticRoots is not null)
        {
            Title += " · FIXTURES AUTORISÉES UNIQUEMENT";
            this.FindControl<Button>("ChooseFolderButton")!.IsVisible = false;
            this.FindControl<Button>("ChooseFixtureButton")!.IsVisible = true;
            this.FindControl<ComboBox>("FixtureChoices")!.IsVisible = true;
        }
        Closed += async (_, _) =>
        {
            _vm.Dispose();
            _inventoryVm.Dispose();
            _brand?.Dispose();
            await _service.DisposeAsync();
        };
    }

#if NEXT05_CI
    // Only the synthetic CI executable exposes this composition seam.
    // Standard portable EXE has no fixture-injection entrypoint.
    internal static ObservedLibraryWindow ForVerifiedFixtures(
        IInventoryApplicationService service, IReadOnlyDictionary<string, string> roots) =>
        new(service, roots);

    internal ObservedLibraryViewModel ModelForCi => _vm;

    internal InventorySelectionResult SelectFixtureForCi(string key,
        Action<int>? chunkObserved = null)
    {
        if (_syntheticRoots is null || !_syntheticRoots.TryGetValue(key, out var root))
            return new(false, "SYNTHETIC_SCOPE_NOT_ALLOWED",
                _service.State.SelectionVersion);
        return _inventoryVm.PrepareSelectedFolder(new InventoryScope(root)
        {
            PlaylistChunkObserved = chunkObserved
        });
    }
#endif

    private void ChooseFixture_OnClick(object? sender, RoutedEventArgs e)
    {
#if NEXT05_CI
        if (!_inventoryVm.CanChangeFolder) return;
        var selected = this.FindControl<ComboBox>("FixtureChoices")?.SelectedIndex ?? -1;
        var name = selected switch
        {
            0 => "IDEAL",
            1 => "CHAOS",
            2 => "MYSTERY",
            _ => null
        };
        if (name is not null) SelectFixtureForCi(name);
#endif
    }

    private async void ChooseFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_inventoryVm.CanChangeFolder || _syntheticRoots is not null) return;
        try
        {
            var roots = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Sélectionner la racine contenant ROMS/ (scan personnel désactivé)",
                AllowMultiple = false
            });
            var root = roots.FirstOrDefault();
            if (root is null) return; // cancellation never alters selection
            if (!root.Path.IsFile)
            {
                _inventoryVm.ReportPickerError();
                return;
            }
            if (_inventoryVm.CanChangeFolder)
                _inventoryVm.PrepareSelectedFolder(new InventoryScope(root.Path.LocalPath));
            // NEVER start a scan here. Explicit consent alone is insufficient.
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or
            ArgumentException or IOException or InvalidOperationException)
        {
            _inventoryVm.ReportPickerError(); // never print private absolute paths
        }
    }
}
