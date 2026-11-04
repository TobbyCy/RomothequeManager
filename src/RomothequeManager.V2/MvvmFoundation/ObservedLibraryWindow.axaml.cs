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
using RomothequeManager.V2.Persistence;

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
    private IdealMediaDraftWindow? _localMediaWindow;
    private string? _openLocalMediaScanId;
    private readonly Func<EsDeCatalogueSnapshot?> _importedSnapshot;

    public ObservedLibraryWindow() : this(new InventoryApplicationService(), null) { }

    /// <summary>Construct a standard window with explicitly provided application
    /// services. Backend authorization, grant lifetime, roots and SQLite protection
    /// remain authoritative; this factory cannot authorize a scan or media preview.
    /// Historical SQLite state may be shown without rescanning any root.</summary>
    public static ObservedLibraryWindow FromStandardServices(
        IInventoryApplicationService service, RestoredCatalogSession? restored = null,
        Func<long, bool>? requestRomsGrant = null,
        Func<IReadOnlyList<CatalogLastKnown>>? loadSavedLibraries = null,
        Func<string, bool>? restoreSaved = null) =>
        FromStandardServicesWithImported(service, restored, requestRomsGrant,
            loadSavedLibraries, restoreSaved, importedSnapshot: null);

    /// <summary>Compose only an already accepted optional XML snapshot for
    /// source-provenance comparison. This does not import, scan or grant access.</summary>
    internal static ObservedLibraryWindow FromStandardServicesWithImported(
        IInventoryApplicationService service, RestoredCatalogSession? restored,
        Func<long, bool>? requestRomsGrant,
        Func<IReadOnlyList<CatalogLastKnown>>? loadSavedLibraries,
        Func<string, bool>? restoreSaved,
        Func<EsDeCatalogueSnapshot?>? importedSnapshot) =>
        new(service, null, standardServiceSupplied: true, restored: restored,
            requestRomsGrant: requestRomsGrant,
            loadSavedLibraries: loadSavedLibraries, restoreSaved: restoreSaved,
            importedSnapshot: importedSnapshot);

    private ObservedLibraryWindow(IInventoryApplicationService service,
        IReadOnlyDictionary<string, string>? syntheticRoots,
        bool standardServiceSupplied = false, RestoredCatalogSession? restored = null,
        Func<long, bool>? requestRomsGrant = null,
        Func<IReadOnlyList<CatalogLastKnown>>? loadSavedLibraries = null,
        Func<string, bool>? restoreSaved = null,
        Func<EsDeCatalogueSnapshot?>? importedSnapshot = null)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _importedSnapshot = importedSnapshot ?? (() => null);
        _syntheticRoots = syntheticRoots;
        InitializeComponent();
        _inventoryVm = new InventoryViewModel(_service, action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        }, requestRomsGrant, loadSavedLibraries, restoreSaved);
        _vm = new ObservedLibraryViewModel(_inventoryVm,
            _syntheticRoots is not null, standardServiceSupplied);
        if (restored is not null)
            _vm.AttachRestoredForStandard(restored);
        _vm.Media.PropertyChanged += OnMediaChanged;
        _inventoryVm.PropertyChanged += OnInventoryChangedForMedia;
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
            _inventoryVm.PropertyChanged -= OnInventoryChangedForMedia;
            _localMediaWindow?.Close();
            _localMediaWindow = null;
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

    internal void AttachRestoredForCi(RestoredCatalogSession session)
    {
        _vm.AttachRestoredForCi(session);
        this.FindControl<Button>("ChooseFixtureButton")!.IsVisible = false;
        this.FindControl<ComboBox>("FixtureChoices")!.IsVisible = false;
        this.FindControl<Button>("ChooseFolderButton")!.IsVisible = false;
    }

    // RS-A is attached only by the CI-only composition, never a normal EXE.
    internal void AttachRsAForCi(RsAUiViewModel rsA)
    {
        _vm.AttachRsAForCi(rsA);
        this.FindControl<Button>("ChooseFixtureButton")!.IsVisible = false;
        this.FindControl<ComboBox>("FixtureChoices")!.IsVisible = false;
        this.FindControl<Button>("ChooseFolderButton")!.IsVisible = false;
    }

    internal bool SelectRsAFixtureForCi(string key, string rootId)
    {
        if (!_vm.HasRsA || _syntheticRoots is null ||
            !_syntheticRoots.TryGetValue(key, out var root)) return false;
        return _vm.RsA!.SelectFixture(root, rootId);
    }

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
        // The backend Skia SKAlphaType.Premul RGBA8888 bytes must be declared
        // AlphaFormat.Premul in Avalonia (including translucent PNGs).
        // No pixel format conversion or opening of source images happens here.
        // The service/result never gives the View a path or a Skia file/bitmap.
        var bitmap = new WriteableBitmap(new PixelSize(decoded.Width, decoded.Height),
            new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
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

    // This button is present only in the standard ROMS view. Neither opening
    // MEDIAS nor confirming a root grants access; the native backend decides.
    private void OpenLocalMedia_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_syntheticRoots is not null || !_vm.CanOpenLocalMedia ||
            _inventoryVm.Current is not {
                ScanStatus: InventoryScanStatus.Complete,
                Availability: InventoryAvailability.Online,
                Freshness: InventoryFreshness.Fresh
            } current) return;
        if (_localMediaWindow is { IsVisible: true })
        {
            _localMediaWindow.Activate();
            return;
        }
        _openLocalMediaScanId = current.ScanId;
        var child = new IdealMediaDraftWindow(() => _inventoryVm.Current,
            imported: _importedSnapshot, productMediaUiEnabled: true);
        _localMediaWindow = child;
        child.Closed += (_, _) =>
        {
            if (ReferenceEquals(_localMediaWindow, child))
            {
                _localMediaWindow = null;
                _openLocalMediaScanId = null;
            }
        };
        child.Show(this);
    }

    // Revoke a child MEDIA workflow if the ROMS selection, run or availability
    // changes while that child is open. Never carry previews into another scope.
    private void OnInventoryChangedForMedia(object? sender, PropertyChangedEventArgs args)
    {
        if (_localMediaWindow is null) return;
        if (_inventoryVm.Current is not {
                ScanStatus: InventoryScanStatus.Complete,
                Availability: InventoryAvailability.Online,
                Freshness: InventoryFreshness.Fresh
            } current ||
            current.ScanId != _openLocalMediaScanId)
        {
            _localMediaWindow.Close();
            _localMediaWindow = null;
            _openLocalMediaScanId = null;
        }
    }

    private void ChooseFixture_OnClick(object? sender, RoutedEventArgs e)
    {
#if NEXT05_CI
        if (!_inventoryVm.CanChangeFolder || _vm.HasRestoredSession) return;
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

    private void RsASelect_OnClick(object? sender, RoutedEventArgs e)
    {
#if NEXT05_CI
        // Only the harness's own non-personal, freshly generated ROMS fixture
        // can be chosen. No native folder picker or persisted grant exists here.
        if (_vm.HasRsA && _vm.RsA!.CanSelect)
            SelectRsAFixtureForCi("RSA", Guid.NewGuid().ToString("D"));
#endif
    }

    private async void ChooseFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_inventoryVm.CanChangeFolder || _syntheticRoots is not null) return;
        try
        {
            var roots = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Choisir la racine contenant ROMS/ (aucun accès avant autorisation ROMS distincte et consentement)",
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
                _inventoryVm.PrepareSelectedFolder(new InventoryScope(root.Path.LocalPath,
                    IdealPlatformFolderConventions.PlatformAliases));
            // NEVER start a scan here. Explicit consent alone is insufficient.
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or
            ArgumentException or IOException or InvalidOperationException)
        {
            _inventoryVm.ReportPickerError(); // never print private absolute paths
        }
    }
}
