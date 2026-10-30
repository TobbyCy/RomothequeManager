using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;

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
    private WriteableBitmap? _mediaBitmap;

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
        _vm.Media.PropertyChanged += OnMediaChanged;
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
            _vm.Media.PropertyChanged -= OnMediaChanged;
            this.FindControl<Image>("MediaPreviewImage")!.Source = null;
            _mediaBitmap?.Dispose();
            _mediaBitmap = null;
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
        Action<int>? chunkObserved = null,
        IReadOnlyDictionary<string, string>? platformAliases = null)
    {
        if (_syntheticRoots is null || !_syntheticRoots.TryGetValue(key, out var root))
            return new(false, "SYNTHETIC_SCOPE_NOT_ALLOWED",
                _service.State.SelectionVersion);
        return _inventoryVm.PrepareSelectedFolder(new InventoryScope(root, platformAliases)
        {
            PlaylistChunkObserved = chunkObserved
        });
    }
#endif

    // Presentation-only RGBA memory adapter: no image path, file access or decoding in a View.
    private void OnMediaChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(MediaPanelViewModel.PreviewResult)) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(RefreshMediaBitmap);
            return;
        }
        RefreshMediaBitmap();
    }

    private void RefreshMediaBitmap()
    {
        var control = this.FindControl<Image>("MediaPreviewImage");
        if (control is null) return;
        control.Source = null;
        _mediaBitmap?.Dispose();
        _mediaBitmap = null;
        if (_vm.Media.PreviewResult is not { Status: MediaPreviewStatus.Ready, Image: { } decoded })
            return;
        // A capped decoded RGBA buffer is copied into the Avalonia-owned image.
        // The service/result never gives the View a path or a Skia file/bitmap.
        var bitmap = new WriteableBitmap(new PixelSize(decoded.Width, decoded.Height),
            new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Unpremul);
        try
        {
            var bytes = decoded.Pixels.ToArray();
            using (var frame = bitmap.Lock())
            {
                for (var row = 0; row < decoded.Height; row++)
                    Marshal.Copy(bytes, checked(row * decoded.Width * 4),
                        IntPtr.Add(frame.Address, checked(row * frame.RowBytes)),
                        checked(decoded.Width * 4));
            }
            control.Source = bitmap;
            _mediaBitmap = bitmap;
        }
        catch
        {
            bitmap.Dispose();
            // A rendering failure must never fall back to a guessed cover.
        }
    }

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
