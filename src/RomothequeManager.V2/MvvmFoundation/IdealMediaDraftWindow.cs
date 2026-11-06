using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Owner-gated native MEDIAS workflow. ROMS observations are supplied by
/// the standard inventory window; constructing the panel never scans either root.</summary>
internal sealed class IdealMediaDraftWindow : Window
{
    private readonly IdealMediaDraftViewModel _vm;
#if NEXT05_CI
    /// <summary>CI-only real Avalonia preview image; absent from standard EXE.</summary>
    internal Image PreviewImageForCi => _previewImage;
    private Button[] _ciActionButtons = Array.Empty<Button>();

    /// <summary>Exercise native INDEX then PREVIEW with explicitly created
    /// RUNNER_TEMP files only. Not a product permission, not callable in Release.</summary>
    internal async Task ExerciseNativeMediaForCi(string root, bool xmlWithoutImage = false)
    {
        var temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            string.IsNullOrWhiteSpace(temp) ||
            !Path.GetFullPath(root).StartsWith(Path.GetFullPath(temp) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(root).StartsWith("rm-next05-native-gui-", StringComparison.Ordinal))
            throw new InvalidDataException("MEDIA_GUI_CI_ONLY_FIXTURE_REQUIRED");
        if (!_vm.SecurityApproved || _roms() is not { } roms ||
            roms.ScanStatus != InventoryScanStatus.Complete)
            throw new InvalidDataException("MEDIA_GUI_CI_ROMS_REQUIRED");
        var xmlFiles = Directory.EnumerateFiles(root, "*.xml",
            SearchOption.AllDirectories).ToArray();
        EsDeCatalogueSnapshot? imported = null;
        if (xmlWithoutImage)
        {
            if (xmlFiles.Length != 1 ||
                !xmlFiles[0].EndsWith("gamelist.xml", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("MEDIA_GUI_ESDE_NOIMAGE_XML_NOT_PRESENT");
            imported = _imported() ??
                throw new InvalidDataException("MEDIA_GUI_ESDE_IMPORT_NOT_AVAILABLE");
            if (imported.Games.Count != 1 ||
                imported.Games[0].Media.Count != 0 ||
                imported.Games[0].RomRelativePath != "ROMS/gba/Fiction.gba")
                throw new InvalidDataException("MEDIA_GUI_ESDE_NOIMAGE_CONTENT_INVALID");
        }
        else if (xmlFiles.Length != 0)
            throw new InvalidDataException("MEDIA_GUI_CI_NO_XML_REQUIRED");
        var mediaRoot = Path.Combine(root, "MEDIAS");
        if (!_vm.SelectMediaRoot(mediaRoot) || !_vm.AuthorizeIndex())
            throw new InvalidDataException("MEDIA_GUI_INDEX_NOT_AUTHORIZED");
        await _vm.StartIndexAsync();
        var index = _vm.CurrentMediaIndex ??
            throw new InvalidDataException("MEDIA_GUI_NATIVE_INDEX_MISSING");
        if (index.ScanStatus != InventoryScanStatus.Complete)
            throw new InvalidDataException("MEDIA_GUI_NATIVE_INDEX_INCOMPLETE");
        var mapping = VerifiedScopeMapping.FromExplicitSelections(roms, index,
            new Dictionary<string, string> { ["gba"] = "gba" },
            "CI_EXPLICIT_SEPARATE_SYNTHETIC_ROOTS");
        _scopeConfirmed.IsChecked = true;
        _xmlRootConfirmed.IsChecked = xmlWithoutImage;
        _vm.AttachExplicitEsDeEvidence(roms, imported, mapping);
        if (_vm.CoverProposals.Count == 1)
            _proposals.SelectedItem = _vm.CoverProposals[0];
        if (_vm.CoverProposals.Count != 1 ||
            _vm.CoverProposals[0].State != MediaUiState.CANDIDATE ||
            !_vm.SelectCoverForPreview(_vm.CoverProposals[0]) ||
            _vm.CanLoadPreview || !_vm.AuthorizePreview())
            throw new InvalidDataException("MEDIA_GUI_XML_FREE_CANDIDATE_OR_GRANT_FAILED");
        await _vm.LoadPreviewAsync();
        if (_vm.Preview is not { Status: MediaPreviewStatus.Ready, Image: not null } ||
            _previewImage.Source is not WriteableBitmap)
            throw new InvalidDataException("MEDIA_GUI_BITMAP_NOT_VISIBLE");
        if (_vm.CoverProposals.Count != 1 ||
            _vm.CoverProposals[0].State != MediaUiState.CANDIDATE)
            throw new InvalidDataException("MEDIA_GUI_ESDE_XML_WITHOUT_IMAGE_NOT_CANDIDATE");
    }

    /// <summary>CI-only proof of focusable and readable action controls under
    /// real Windows layout at both requested viewports. Uses normal Avalonia
    /// Tab navigation, never fakes a success by setting the focused element.</summary>
    internal void VerifyKeyboardAndLayoutForCi(int width, int height)
    {
        if (!IsVisible || width < 760 || height < 480 ||
            _ciActionButtons.Length != 9 ||
            Content is not ScrollViewer scroll ||
            scroll.Content is not Control root)
            throw new InvalidDataException("MEDIA_GUI_CI_ACCESSIBILITY_PRECONDITION");
        Width = width;
        Height = height;
        UpdateLayout();
        if (scroll.Bounds.Height < 290 || scroll.Bounds.Width < 620)
            throw new InvalidDataException("MEDIA_GUI_CI_COMPACT_VIEWPORT_NOT_USABLE");

        // Check every action has a readable, correctly sized control and can
        // be brought into the viewport, including items below the fold.
        foreach (var button in _ciActionButtons)
        {
            if (!button.Focusable || !button.IsTabStop ||
                button.Bounds.Width < 200 || button.Bounds.Height < 34 ||
                button.Content is not string text || text.Length < 7)
                throw new InvalidDataException("MEDIA_GUI_CI_ACTION_UNREADABLE");
            var location = button.TranslatePoint(new Point(0, 0), root) ??
                throw new InvalidDataException("MEDIA_GUI_CI_ACTION_NOT_IN_TREE");
            scroll.Offset = new Vector(0, Math.Clamp(location.Y - 50, 0,
                Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
            UpdateLayout();
            var visible = button.TranslatePoint(new Point(0, 0), scroll) ??
                throw new InvalidDataException("MEDIA_GUI_CI_ACTION_NOT_REACHABLE");
            if (visible.Y < -3 ||
                visible.Y + button.Bounds.Height > scroll.Bounds.Height + 4)
                throw new InvalidDataException("MEDIA_GUI_CI_ACTION_CLIPPED");
        }

        // Tab/Shift+Tab traversal after the chosen image is rendered, with
        // enabled controls separated by disabled one-shot INDEX/PREVIEW actions.
        var first = _ciActionButtons[0];
        if (!first.IsEnabled || !first.Focus(NavigationMethod.Tab) ||
            !ReferenceEquals(FocusManager.GetFocusedElement(), first) ||
            !FocusManager.TryMoveFocus(NavigationDirection.Next, default))
            throw new InvalidDataException("MEDIA_GUI_CI_TAB_NAVIGATION_FAILED");
        var next = FocusManager.GetFocusedElement();
        if (next is null || ReferenceEquals(next, first) ||
            !FocusManager.TryMoveFocus(NavigationDirection.Previous, default) ||
            !ReferenceEquals(FocusManager.GetFocusedElement(), first))
            throw new InvalidDataException("MEDIA_GUI_CI_SHIFT_TAB_NAVIGATION_FAILED");
        var reached = new HashSet<Button> { first };
        for (var step = 0; step < 80; ++step)
        {
            if (!FocusManager.TryMoveFocus(NavigationDirection.Next, default)) break;
            if (FocusManager.GetFocusedElement() is Button button) reached.Add(button);
            if (_ciActionButtons.Where(b => b.IsEnabled).All(reached.Contains)) break;
        }
        if (_ciActionButtons.Where(b => b.IsEnabled).Any(b => !reached.Contains(b)))
            throw new InvalidDataException("F95_12_ENABLED_ACTION_NOT_TAB_REACHABLE");
        if (_proposals.Items.Count == 0 || _evidence.Items.Count == 0 ||
            string.IsNullOrWhiteSpace(_detail.Text))
            throw new InvalidDataException("MEDIA_GUI_CI_PROVENANCE_NOT_ACCESSIBLE");
        Console.WriteLine("MEDIA163_TAB_SHIFT_TAB_LAYOUT_PASS viewport=" +
            width + "x" + height + " actions=9 scroll=true provenance=true");
    }

    internal void VerifyPreviewRevocationForCi()
    {
        var pixels = _vm.Preview?.Image?.Pixels ?? ReadOnlyMemory<byte>.Empty;
        if (pixels.IsEmpty || _bitmap is null || !_vm.ClearCommand.CanExecute(null) ||
            _vm.CancelCommand.CanExecute(null))
            throw new InvalidDataException("F95_12_CLEAR_UNAVAILABLE_AFTER_PREVIEW");
        var clear = _ciActionButtons[^1];
        if (!clear.IsEnabled || !clear.Focus(NavigationMethod.Tab))
            throw new InvalidDataException("F95_12_CLEAR_NOT_KEYBOARD_REACHABLE");
        clear.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyDownEvent, Key = Key.Space });
        clear.RaiseEvent(new KeyEventArgs { RoutedEvent = KeyUpEvent, Key = Key.Space });
        if (_previewImage.Source is not null || _bitmap is not null || _rendered is not null ||
            _vm.Preview is not null || pixels.Span.ContainsAnyExcept((byte)0) ||
            _vm.CanLoadPreview || _vm.CanAuthorizePreview || _vm.CanStartIndex)
            throw new InvalidDataException("F95_12_REVOCATION_INCOMPLETE");
        Console.WriteLine("F95_12_GUI_REVOCATION_PASS pixels_zero=true bitmap_released=true grants_revoked=true");
    }
#endif
    private readonly Func<InventorySnapshot?> _roms;
    private readonly Func<EsDeCatalogueSnapshot?> _imported;
    private WriteableBitmap? _bitmap;
    private MediaPreviewResult? _rendered;
    private readonly Image _previewImage = new() {
        Width = 320, Height = 240, Stretch = Stretch.Uniform
    };
    private readonly ListBox _proposals = new() { MinHeight = 130, MaxHeight = 230 };
    private readonly ListBox _evidence = new() { MinHeight = 70, MaxHeight = 160 };
    private readonly TextBlock _state = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _detail = new() { TextWrapping = TextWrapping.Wrap };
    private readonly CheckBox _scopeConfirmed = new() {
        Content = "Je confirme que les plateformes de ces deux racines sélectionnées correspondent."
    };
    private readonly CheckBox _ambiguityConfirmed = new() {
        Content = "Je choisis explicitement cette image parmi les alternatives (aperçu seulement)."
    };
    private readonly CheckBox _xmlRootConfirmed = new() {
        Content = "J'atteste que l'import ES-DE affiché correspond à la racine ROMS sélectionnée (XML facultatif)."
    };

    /// <summary>Create a closed-by-default GUI: normal launch needs a deliberate
    /// product-composition decision and independent native INDEX/PREVIEW grants.</summary>
    internal IdealMediaDraftWindow() :
        this(() => null, () => null, productMediaUiEnabled: false) { }

    /// <summary>Compose an already observed ROMS source and optional imported XML
    /// source. The boolean is a product UI gate only, never a native permission.</summary>
    internal IdealMediaDraftWindow(Func<InventorySnapshot?> roms,
        Func<EsDeCatalogueSnapshot?>? imported = null, bool productMediaUiEnabled = false)
    {
        _roms = roms ?? throw new ArgumentNullException(nameof(roms));
        _imported = imported ?? (() => null);
        Title = "ROMothèque · Jaquettes locales (candidates, non approuvées)";
        Width = 900;
        Height = 700;
        MinWidth = 700;
        MinHeight = 480;
        _vm = new IdealMediaDraftViewModel(new IdealMediaApplicationService(),
            productMediaUiEnabled: productMediaUiEnabled);
        DataContext = _vm;

        var choose = new Button { Content = "Choisir le dossier MEDIAS", MinHeight = 36 };
        var authorizeIndex = new Button {
            Content = "Autoriser l’analyse INDEX", Command = _vm.AuthorizeIndexCommand,
            MinHeight = 36
        };
        var startIndex = new Button {
            Content = "Analyser les médias",
            Command = _vm.StartIndexCommand, MinHeight = 36
        };
        var project = new Button {
            Content = "Proposer les jaquettes",
            MinHeight = 36
        };
        var chooseCover = new Button {
            Content = "Choisir cette image",
            MinHeight = 36
        };
        var authorizePreview = new Button {
            Content = "Autoriser cet aperçu PREVIEW",
            Command = _vm.AuthorizePreviewCommand, MinHeight = 36
        };
        var loadPreview = new Button {
            Content = "Afficher l’aperçu",
            Command = _vm.LoadPreviewCommand, MinHeight = 36
        };
        var cancel = new Button {
            Content = "Annuler l’opération",
            Command = _vm.CancelCommand, MinHeight = 36
        };

        var clear = new Button {
            Content = "Effacer l’aperçu et révoquer MEDIAS",
            Command = _vm.ClearCommand, MinHeight = 36
        };
        foreach (var button in new[] { choose, authorizeIndex, startIndex, project,
            chooseCover, authorizePreview, loadPreview, cancel, clear })
        {
            button.MinWidth = 240;
            button.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
#if NEXT05_CI
        _ciActionButtons = new[] { choose, authorizeIndex, startIndex, project,
            chooseCover, authorizePreview, loadPreview, cancel, clear };
#endif
        var observed = new ListBox { MinHeight = 80, MaxHeight = 150 };
        void RefreshObserved()
        {
            var roms = _roms();
            observed.ItemsSource = roms?.Observations.Select(o =>
                o.PlatformKey + " · " + o.RelativePath + " [" + o.Kind + "]").ToArray()
                ?? Array.Empty<string>();
        }
        RefreshObserved();

        choose.Click += async (_, _) =>
        {
            if (!_vm.CanChooseMedia) return;
            try
            {
                var selected = (await StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions {
                        Title = "Choisir la racine MEDIAS séparément (aucun grant)",
                        AllowMultiple = false
                    })).FirstOrDefault();
                if (selected?.Path.IsFile == true && _vm.CanChooseMedia)
                {
                    _scopeConfirmed.IsChecked = false;
                    _xmlRootConfirmed.IsChecked = false;
                    _ambiguityConfirmed.IsChecked = false;
                    _vm.SelectMediaRoot(selected.Path.LocalPath);
                }
            }
            catch (Exception e) when (e is IOException or ArgumentException or
                UnauthorizedAccessException or InvalidOperationException)
            {
                // No absolute user path is shown.
            }
        };

        project.Click += (_, _) =>
        {
            if (!_vm.SecurityApproved || _scopeConfirmed.IsChecked != true ||
                _vm.CurrentMediaIndex is not { } media ||
                _roms() is not { } roms) return;
            // No mapping by XML title or proximity: an independent user click
            // explicitly asserts that the two selected roots share these platforms.
            var knownRoms = roms.Observations.Select(o => o.PlatformKey)
                .Where(k => !string.IsNullOrWhiteSpace(k) &&
                    !string.Equals(k, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            var knownMedias = media.Observations.Select(o => o.PlatformKey)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var mapping = knownRoms.Where(knownMedias.Contains)
                .ToDictionary(k => k, k => k, StringComparer.OrdinalIgnoreCase);
            if (mapping.Count == 0)
            {
                _detail.Text = "Aucune plateforme commune explicitement comparable.";
                return;
            }
            _vm.AttachExplicitEsDeEvidence(roms,
                _xmlRootConfirmed.IsChecked == true ? _imported() : null,
                VerifiedScopeMapping.FromExplicitSelections(roms, media, mapping,
                    "USER_CONFIRMED_SEPARATE_ROMS_AND_MEDIAS_ROOTS"));
            RefreshObserved();
        };

        chooseCover.Click += (_, _) =>
        {
            if (!_vm.SecurityApproved ||
                _proposals.SelectedItem is not IdealMediaCoverProposal selected) return;
            if (selected.RequiresExplicitChoice && _ambiguityConfirmed.IsChecked != true)
            {
                _detail.Text = "Ambiguïté : confirmer cette observation exacte avant PREVIEW.";
                return;
            }
            _vm.SelectCoverForPreview(selected);
        };

        _proposals.SelectionChanged += (_, _) =>
        {
            _ambiguityConfirmed.IsChecked = false;
            if (_proposals.SelectedItem is IdealMediaCoverProposal selected)
                _detail.Text = selected.Label + "\nSources : " +
                    string.Join(", ", selected.Provenance) + "\nPreuves : " +
                    string.Join(", ", selected.Evidence) + "\nConflits : " +
                    string.Join(", ", selected.Conflicts);
        };

        void Refresh()
        {
            _state.Text = "Choisir → Analyser → Proposer → Consulter\nÉtape active : " +
                _vm.ActiveStep + "\n" + _vm.Status + "\n" + _vm.NextAction +
                "\n" + _vm.IndexAttemptText + "\n" + _vm.CompleteIndexText;
            choose.IsEnabled = _vm.CanChooseMedia;
            authorizeIndex.IsEnabled = _vm.CanAuthorizeIndex;
            startIndex.IsEnabled = _vm.CanStartIndex;
            project.IsEnabled = _vm.SecurityApproved &&
                _scopeConfirmed.IsChecked == true && _vm.CurrentMediaIndex is not null &&
                _roms()?.ScanStatus == InventoryScanStatus.Complete &&
                _roms()?.Availability == InventoryAvailability.Online &&
                _roms()?.Freshness == InventoryFreshness.Fresh;
            if (!ReferenceEquals(_proposals.ItemsSource, _vm.CoverProposals))
                _proposals.ItemsSource = _vm.CoverProposals;
            _evidence.ItemsSource = _vm.Evidence.Select(row =>
                row.State + " · ROM observation " + row.RomObservationId +
                " · Provenance : " + string.Join(", ", row.SourceProvenance) +
                " · Conflits : " + string.Join(", ", row.Conflicts)).ToArray();
            chooseCover.IsEnabled = _vm.SecurityApproved &&
                _proposals.SelectedItem is IdealMediaCoverProposal;
            authorizePreview.IsEnabled = _vm.CanAuthorizePreview;
            loadPreview.IsEnabled = _vm.CanLoadPreview;
            cancel.IsEnabled = _vm.CancelCommand.CanExecute(null);
            clear.IsEnabled = _vm.ClearCommand.CanExecute(null);
            var primary = !_vm.CanChooseMedia ? null :
                _vm.CanLoadPreview ? loadPreview :
                _vm.CanAuthorizePreview ? authorizePreview :
                _vm.CanStartIndex ? startIndex :
                _vm.CurrentMediaIndex is null ? (_vm.CanAuthorizeIndex ? authorizeIndex : choose) :
                _vm.CoverProposals.Count == 0 ? project : chooseCover;
            foreach (var button in new[] { choose, authorizeIndex, startIndex, project,
                chooseCover, authorizePreview, loadPreview })
                button.FontWeight = ReferenceEquals(button, primary) ? FontWeight.Bold : FontWeight.Normal;
            _scopeConfirmed.IsEnabled = _vm.SecurityApproved && _vm.CurrentMediaIndex is not null;
            _xmlRootConfirmed.IsEnabled = _vm.SecurityApproved && _imported() is not null &&
                _scopeConfirmed.IsChecked == true;
            _ambiguityConfirmed.IsEnabled = _vm.SecurityApproved &&
                _proposals.SelectedItem is IdealMediaCoverProposal { RequiresExplicitChoice: true };

            var preview = _vm.Preview;
            if (ReferenceEquals(preview, _rendered)) return;
            _previewImage.Source = null;
            IdealMediaBitmapAdapter.ClearAndDispose(_bitmap);
            _bitmap = null;
            _rendered = preview;
            // No disk or URI fallback: the ONLY source is decoded RGBA in memory.
            if (preview is { Status: MediaPreviewStatus.Ready, Image: not null })
            {
                _bitmap = IdealMediaBitmapAdapter.Create(preview);
                _previewImage.Source = _bitmap;
                if (_bitmap is null)
                    _detail.Text = "Impossible de rendre les pixels ; image non attribuée.";
            }
        }
        _vm.PropertyChanged += (_, _) =>
        {
            if (Dispatcher.UIThread.CheckAccess()) Refresh();
            else Dispatcher.UIThread.Post(Refresh);
        };
        _scopeConfirmed.Click += (_, _) => Refresh();
        _proposals.SelectionChanged += (_, _) => Refresh();
        Refresh();

        Content = new ScrollViewer {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new Border {
                Padding = new Thickness(18),
                Child = new StackPanel {
                    Spacing = 9,
                    Children = {
                        new TextBlock {
                            Text = "NEXT-05 · ROMS observées et jaquettes candidates",
                            FontSize = 19, FontWeight = FontWeight.Bold
                        },
                        new TextBlock {
                            Text = "XML facultatif. Jaquettes par plateforme et nom de fichier exact, " +
                                "aucun titre seul. Choix, INDEX, PREVIEW et association restent distincts.",
                            TextWrapping = TextWrapping.Wrap
                        },
                        _state,
                        new TextBlock { Text = "Fichiers ROMS déjà observés (pas de scan depuis cette fenêtre) :" },
                        observed,
                        choose,
                        authorizeIndex,
                        startIndex,
                        _scopeConfirmed,
                        _xmlRootConfirmed,
                        project,
                        new TextBlock {
                            Text = "REFERENCED / CANDIDATE / AMBIGUOUS — propositions non approuvées :"
                        },
                        _proposals,
                        new TextBlock {
                            Text = "Tous les statuts d'observation : REFERENCED, CANDIDATE, " +
                                "AMBIGUOUS, NOT_COMPARABLE, ABSENT_IN_COMPLETE_SCOPE"
                        },
                        new Expander { Header = "Détails et provenance", Content =
                            new StackPanel { Children = { _evidence, _detail } } },
                        _ambiguityConfirmed,
                        chooseCover,
                        authorizePreview,
                        loadPreview,
                        new TextBlock { Text = "Aperçu autorisé — pixels mémoire (aucune attribution) :" },
                        _previewImage,
                        cancel,
                        clear
                    }
                }
            }
        };
        Closed += (_, _) =>
        {
            _previewImage.Source = null;
            IdealMediaBitmapAdapter.ClearAndDispose(_bitmap);
            _bitmap = null;
            _vm.Dispose();
        };
    }
}
