using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Unpublished, code-only UI candidate. It is intentionally not composed
/// by Program, MainWindow, or any public navigation route before review #161.</summary>
internal sealed class IdealMediaDraftWindow : Window
{
    private readonly IdealMediaDraftViewModel _vm;

    /// <summary>Prepare independent MEDIAS controls without opening a source.</summary>
    internal IdealMediaDraftWindow()
    {
        Title = "ROMothèque · MEDIAS / PREVIEW (Draft non activé)";
        Width = 900;
        Height = 650;
        MinWidth = 700;
        MinHeight = 460;
        _vm = new IdealMediaDraftViewModel(new IdealMediaApplicationService());
        DataContext = _vm;

        var state = new TextBlock { Text = _vm.Status, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var choose = new Button { Content = "1 · Choisir MEDIAS (sélection seule)" };
        var authorizeIndex = new Button {
            Content = "2 · Autoriser INDEX (une tentative)",
            Command = _vm.AuthorizeIndexCommand
        };
        var startIndex = new Button {
            Content = "3 · Indexer les métadonnées MEDIAS",
            Command = _vm.StartIndexCommand
        };
        var authorizePreview = new Button {
            Content = "4 · Autoriser PREVIEW pour l'image sélectionnée",
            Command = _vm.AuthorizePreviewCommand
        };
        var loadPreview = new Button {
            Content = "5 · Charger l'aperçu contrôlé",
            Command = _vm.LoadPreviewCommand
        };
        var cancel = new Button {
            Content = "Annuler / révoquer",
            Command = _vm.CancelCommand
        };
        var referenced = new ListBox { MinHeight = 110, MaxHeight = 220 };
        var outcomes = new TextBlock {
            Text = "Absent / refusé / OFFLINE / STALE / corrompu / hors scope : aucune association automatique.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };
        var caution = new TextBlock {
            Text = "Commandes désactivées : revue sécurité #161 et GO distinct requis. " +
                "L'import XML ES-DE est indépendant. Aucune lecture ROMS, XML ou média " +
                "depuis la View ; les jaquettes candidates n'impliquent aucun droit PREVIEW.",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        choose.Click += async (_, _) =>
        {
            if (!_vm.CanChooseMedia) return;
            try
            {
                var selections = await StorageProvider.OpenFolderPickerAsync(
                    new FolderPickerOpenOptions {
                        Title = "Choisir explicitement la racine MEDIAS (aucun droit de lecture accordé)",
                        AllowMultiple = false
                    });
                if (!_vm.CanChooseMedia) return;
                var selected = selections.FirstOrDefault();
                if (selected?.Path.IsFile == true)
                    _vm.SelectMediaRoot(selected.Path.LocalPath);
                // Cancel or nonlocal providers never confer any grant.
            }
            catch (Exception error) when (error is ArgumentException or IOException or
                UnauthorizedAccessException or InvalidOperationException)
            {
                // No exception text or private filesystem path is displayed.
            }
        };
        referenced.SelectionChanged += (_, _) =>
        {
            if (referenced.SelectedItem is MediaUiRow row)
                _vm.SelectReferencedCover(row);
        };
        _vm.PropertyChanged += (_, _) =>
        {
            state.Text = _vm.Status;
            choose.IsEnabled = _vm.CanChooseMedia;
            referenced.ItemsSource = _vm.ReferencedCovers;
            referenced.IsEnabled = _vm.SecurityApproved &&
                _vm.CanChooseMedia && _vm.ReferencedCovers.Count > 0;
            authorizeIndex.IsEnabled = _vm.CanAuthorizeIndex;
            startIndex.IsEnabled = _vm.CanStartIndex;
            authorizePreview.IsEnabled = _vm.CanAuthorizePreview;
            loadPreview.IsEnabled = _vm.CanLoadPreview;
            cancel.IsEnabled = _vm.SecurityApproved;
            outcomes.Text = _vm.Preview?.Status switch
            {
                MediaPreviewStatus.Ready => "Aperçu RGBA en mémoire (non approuvé comme jaquette).",
                MediaPreviewStatus.DecodeFailed => "Image corrompue.",
                MediaPreviewStatus.Stale => "Aperçu périmé.",
                MediaPreviewStatus.Unavailable => "Média absent ou inaccessible.",
                MediaPreviewStatus.Rejected => "Autorisation refusée / hors périmètre.",
                _ => "Aucun aperçu affiché automatiquement."
            };
        };

        // Defense in depth: even a future accidental Window construction leaves
        // all actionable controls disabled until the hard security gate is changed.
        choose.IsEnabled = _vm.CanChooseMedia;
        authorizeIndex.IsEnabled = _vm.CanAuthorizeIndex;
        startIndex.IsEnabled = _vm.CanStartIndex;
        authorizePreview.IsEnabled = _vm.CanAuthorizePreview;
        loadPreview.IsEnabled = _vm.CanLoadPreview;
        referenced.IsEnabled = _vm.SecurityApproved &&
            _vm.CanChooseMedia && _vm.ReferencedCovers.Count > 0;
        cancel.IsEnabled = _vm.SecurityApproved;

        var steps = new StackPanel {
            Spacing = 9,
            Children = {
                new TextBlock {
                    Text = "NEXT-05 · MEDIAS / PREVIEW · CANDIDAT NON PUBLIÉ",
                    FontSize = 19, FontWeight = Avalonia.Media.FontWeight.Bold
                },
                caution,
                state,
                choose,
                authorizeIndex,
                startIndex,
                new TextBlock {
                    Text = "Références ES-DE déclarées et observées (preuve, pas attribution) :"
                },
                referenced,
                authorizePreview,
                loadPreview,
                outcomes,
                cancel
            }
        };
        Content = new ScrollViewer {
            Content = new Border { Padding = new Thickness(18), Child = steps }
        };
        Closed += (_, _) => _vm.Dispose();
    }
}
