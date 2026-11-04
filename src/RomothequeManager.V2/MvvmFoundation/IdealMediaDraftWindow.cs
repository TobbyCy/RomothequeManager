using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
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

        var choose = new Button { Content = "1 · Choisir MEDIAS (sélection seule)", MinHeight = 36 };
        var authorizeIndex = new Button {
            Content = "2 · Autoriser INDEX pour une tentative", Command = _vm.AuthorizeIndexCommand,
            MinHeight = 36
        };
        var startIndex = new Button {
            Content = "3 · Indexer les métadonnées MEDIAS",
            Command = _vm.StartIndexCommand, MinHeight = 36
        };
        var project = new Button {
            Content = "4 · Comparer les fichiers observés et proposer des jaquettes",
            MinHeight = 36
        };
        var chooseCover = new Button {
            Content = "5 · Choisir l'image exacte pour aperçu (pas une association)",
            MinHeight = 36
        };
        var authorizePreview = new Button {
            Content = "6 · Autoriser PREVIEW (cette image seulement)",
            Command = _vm.AuthorizePreviewCommand, MinHeight = 36
        };
        var loadPreview = new Button {
            Content = "7 · Afficher réellement l'image PNG/JPEG",
            Command = _vm.LoadPreviewCommand, MinHeight = 36
        };
        var cancel = new Button {
            Content = "Annuler / révoquer INDEX et PREVIEW",
            Command = _vm.CancelCommand, MinHeight = 36
        };

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
            _vm.AttachExplicitEsDeEvidence(roms, _imported(),
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
            _state.Text = _vm.Status;
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
            cancel.IsEnabled = _vm.SecurityApproved;
            _scopeConfirmed.IsEnabled = _vm.SecurityApproved && _vm.CurrentMediaIndex is not null;
            _ambiguityConfirmed.IsEnabled = _vm.SecurityApproved &&
                _proposals.SelectedItem is IdealMediaCoverProposal { RequiresExplicitChoice: true };

            var preview = _vm.Preview;
            if (ReferenceEquals(preview, _rendered)) return;
            _previewImage.Source = null;
            _bitmap?.Dispose();
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
                        project,
                        new TextBlock {
                            Text = "REFERENCED / CANDIDATE / AMBIGUOUS — propositions non approuvées :"
                        },
                        _proposals,
                        new TextBlock {
                            Text = "Tous les statuts d'observation : REFERENCED, CANDIDATE, " +
                                "AMBIGUOUS, NOT_COMPARABLE, ABSENT_IN_COMPLETE_SCOPE"
                        },
                        _evidence,
                        _detail,
                        _ambiguityConfirmed,
                        chooseCover,
                        authorizePreview,
                        loadPreview,
                        new TextBlock { Text = "Aperçu autorisé — pixels mémoire (aucune attribution) :" },
                        _previewImage,
                        cancel
                    }
                }
            }
        };
        Closed += (_, _) =>
        {
            _previewImage.Source = null;
            _bitmap?.Dispose();
            _bitmap = null;
            _vm.Dispose();
        };
    }
}
