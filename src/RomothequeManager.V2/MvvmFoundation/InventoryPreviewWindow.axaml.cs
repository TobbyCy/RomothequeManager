using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

namespace RomothequeManager.V2.MvvmFoundation;

// Isolated Avalonia preview until #75/#76/#77 are integrated and DEV 1 service
// is accepted. This NEVER scans user content: injected service is simulation-only.
public partial class InventoryPreviewWindow : Window
{
    private readonly IInventoryApplicationService _service;
    private readonly InventoryPreviewService? _preview;
    private readonly IReadOnlyDictionary<string, string>? _syntheticRoots;
    private readonly InventoryViewModel _vm;
    private Bitmap? _brand;

    public InventoryPreviewWindow() : this(new InventoryPreviewService(), null) { }

    // Only the CI fixture harness injects an approved canonical service. Normal
    // windows keep the read-free historical simulation; no public scan switch.
    internal InventoryPreviewWindow(
        IInventoryApplicationService service,
        IReadOnlyDictionary<string, string>? syntheticRoots)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _preview = service as InventoryPreviewService;
        _syntheticRoots = syntheticRoots;
        if ((_preview is null) != (_syntheticRoots is not null))
            throw new InvalidOperationException("NEXT03_INVALID_VIEW_COMPOSITION");

        InitializeComponent();
        if (_syntheticRoots is not null) ConfigureSyntheticMode();
        _vm = new InventoryViewModel(_service, action =>
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
            await _service.DisposeAsync();
        };
    }

    // Presentation-only change: keep the legacy simulation XAML and show a
    // clearly different real fixture mode without duplicating inventory logic.
    private void ConfigureSyntheticMode()
    {
        Title = "ROMothèque Manager · NEXT-03 · INVENTAIRE RÉEL SUR FIXTURES SYNTHÉTIQUES";
        void ReplaceText(string controlName, string value)
        {
            var text = this.FindControl<TextBlock>(controlName) ??
                throw new InvalidOperationException("NEXT03_VIEW_BINDING_MISSING");
            text.Text = value;
        }
        ReplaceText("ModeHeading", "NEXT-03 — VRAI SERVICE / FIXTURES IDEAL, CHAOS, MYSTERY");
        ReplaceText("ModeDescription",
            "Observations issues du véritable InventoryApplicationService sur les seules fixtures fraîchement générées par la CI. Aucune ROMothèque personnelle autorisée.");
        ReplaceText("ModeNotice",
            "L'autorisation est injectée par le harnais CI uniquement ; le constructeur public du service refuse tous les scans. Aucun dossier arbitraire n'est accepté.");
        ReplaceText("ScopeDescription",
            "Portée : ROMS/ uniquement · métadonnées en lecture seule · fichiers observés, pas identités de jeux.");
        ReplaceText("PreflightDescription",
            "Choisir explicitement une fixture ci-dessous, confirmer séparément, puis inventorier. Changement interdit pendant Running ou CancellationRequested.");
        ReplaceText("CaseHeading", "2 · Bibliothèque synthétique autorisée");
        ReplaceText("ResultHeading", "4 · Résultats réels du service / couverture prouvée");
        ReplaceText("CoverageHeading", "Portées réellement couvertes par l'inventaire :");
        ReplaceText("ObservationHeading", "Fichiers observés — PREUVE FILESYSTEM (aucun GameId inféré)");
        ReplaceText("DiagnosticHeading", "Diagnostics du véritable service");
        ReplaceText("CloseNotice",
            "Aucun état persisté après fermeture ; aucun XML ES-DE importé automatiquement.");

        this.FindControl<Button>("ChooseFolderButton")!.IsVisible = false;
        this.FindControl<ComboBox>("PreviewCase")!.IsVisible = false;
        this.FindControl<ComboBox>("SyntheticFixtureChoice")!.IsVisible = true;
        this.FindControl<Button>("ChooseSyntheticButton")!.IsVisible = true;
        this.FindControl<CheckBox>("ConsentCheckBox")!.Content =
            "Je confirme l'inventaire en lecture seule de la fixture synthétique sélectionnée";
        this.FindControl<Button>("StartSimulationButton")!.Content =
            "Inventorier la fixture synthétique";
    }

    internal InventoryViewModel ModelForSyntheticRecipe => _vm;

    internal InventorySelectionResult SelectSyntheticFixtureForRecipe(
        string name, Action<int>? playlistChunkObserved = null)
    {
        if (_syntheticRoots is null || !_syntheticRoots.TryGetValue(name, out var root))
            return new(false, "SYNTHETIC_FIXTURE_NOT_ALLOWED", _service.State.SelectionVersion);
        // The allowlist is immutable at construction and the service re-checks its
        // internal permission gate immediately before the real InventorySession.
        return _vm.PrepareSelectedFolder(new InventoryScope(root)
        {
            PlaylistChunkObserved = playlistChunkObserved
        });
    }

    private void ChooseSynthetic_OnClick(object? sender, RoutedEventArgs e)
    {
        if (!_vm.CanChangeFolder) return;
        var choice = this.FindControl<ComboBox>("SyntheticFixtureChoice")?.SelectedIndex ?? -1;
        var name = choice switch
        {
            0 => "IDEAL", 1 => "CHAOS", 2 => "MYSTERY", _ => null
        };
        if (name is not null) SelectSyntheticFixtureForRecipe(name);
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
            if (folder is null) return; // cancelling the picker preserves the prior root
            if (!folder.Path.IsFile)
            {
                _vm.ReportPickerError();
                return;
            }
            if (!_vm.CanChangeFolder) return; // selection completed after close/cancellation
            var path = folder.Path.LocalPath;
            // The VM delegates syntax validation to preview service, and DEV 1
            // will later own bounded preflight / fixture-only authority.
            _vm.PrepareSelectedFolder(new InventoryScope(path));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or
            ArgumentException or IOException or InvalidOperationException)
        {
            // Do not expose the private absolute path from the exception.
            _vm.ReportPickerError();
        }
    }

    private void ClearFolder_OnClick(object? sender, RoutedEventArgs e)
    {
        _vm.ClearSelection(); // the service rejects this during an active simulation
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
        _preview?.SelectPreviewOutcome(mode);
    }
}
