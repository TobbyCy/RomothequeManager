#if NEXT05_CI
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory.Media;
using SkiaSharp;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2;

// This class cannot exist in a standard Windows build. Only approved, generated
// runner-temp fixture roots receive an internal service permission predicate.
internal static class Next05GuiHarness
{
    private static void Demand(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    private static bool HasLinkedAncestor(string path)
    {
        for (DirectoryInfo? dir = new(path); dir is not null; dir = dir.Parent)
            if ((dir.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    public static void Configure(MainWindow main, string fixtureRoot)
    {
        var runner = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        Demand(OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
            !string.IsNullOrWhiteSpace(runner), "N05_GUI_CI_HOST_REQUIRED");
        var canonicalRunner = Path.GetFullPath(runner!).TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetFullPath(fixtureRoot);
        var parent = Directory.GetParent(root);
        Demand(parent is not null &&
            Path.GetFileName(root).Equals("no-xml", StringComparison.Ordinal) &&
            Path.GetFileName(parent!.FullName).StartsWith("NEXT05-AVALONIA-",
                StringComparison.Ordinal) &&
            Path.GetFullPath(parent.Parent!.FullName).Equals(canonicalRunner,
                StringComparison.OrdinalIgnoreCase) &&
            root.StartsWith(canonicalRunner + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) &&
            !HasLinkedAncestor(root), "N05_GUI_FIXTURE_ROOT_NOT_EXCLUSIVE");

        var roots = new[] { "IDEAL", "CHAOS", "MYSTERY" }
            .ToDictionary(name => name, name => Path.Combine(root, name));
        foreach (var candidate in roots.Values)
            Demand(Directory.Exists(Path.Combine(candidate, "ROMS")) &&
                !HasLinkedAncestor(candidate), "N05_GUI_SYNTHETIC_TREE_REQUIRED");
        Demand(!Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories).Any(),
            "N05_GUI_XML_FREE_FIXTURES_REQUIRED");
        var permitted = new HashSet<string>(roots.Values, StringComparer.OrdinalIgnoreCase);
        var service = new InventoryApplicationService(scope =>
            permitted.Contains(scope.LibraryRoot) &&
            scope.LibraryRoot.StartsWith(root + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        var observed = ObservedLibraryWindow.ForVerifiedFixtures(service, roots);
        var captures = Path.Combine(canonicalRunner, "next05-gui-captures");
        Directory.CreateDirectory(captures);
        main.Opened += (_, _) =>
            Dispatcher.UIThread.Post(() => observed.Show(main), DispatcherPriority.Background);
        observed.Opened += async (_, _) =>
        {
            try
            {
                await ExerciseAsync(observed, roots, captures);
                Console.WriteLine("N05_GUI_REAL_SERVICE_PASS fixture_count=3 xml=0 personal_scan=false");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 44;
                Console.Error.WriteLine("N05_GUI_FAIL " + ex.GetType().Name +
                    " / " + (ex is InvalidDataException ? ex.Message : "UNEXPECTED_ERROR"));
            }
            finally
            {
                observed.Close();
                main.Close();
            }
        };
    }

    private static void Capture(ObservedLibraryWindow window, string directory,
        string name, int width, int height)
    {
        window.Width = width;
        window.Height = height;
        Demand(window.Content is Control && window.IsVisible, "N05_GUI_NOT_VISIBLE");
        var visual = (Control)window.Content!;
        visual.Measure(new Size(width - 2, height - 2));
        visual.Arrange(new Rect(0, 0, width - 2, height - 2));
        if (name.StartsWith("MEDIA-", StringComparison.Ordinal))
        {
            var scroll = window.FindControl<ScrollViewer>("RootScroll")!;
            scroll.Offset = new Vector(0, Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height));
        }
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height),
            new Vector(96, 96));
        bitmap.Render(visual);
        var filename = Path.Combine(directory, name + "-" + width + "x" + height + ".png");
        bitmap.Save(filename, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(filename).Length > 4000, "N05_GUI_SCREENSHOT_EMPTY");
        Console.WriteLine("N05_GUI_CAPTURE_PASS " + Path.GetFileName(filename));
    }

    // Extends the SAME runner-only harness; product build never includes this method.
    // All images are invented within a disposable, independently scoped MEDIAS root.
    private static async Task ExerciseMediaAsync(ObservedLibraryWindow window,
        InventorySnapshot roms, string captures)
    {
        var vm = window.ModelForCi;
        var eligible = roms.Observations
            .Where(o => o.Kind == InventoryKind.PotentialRom &&
                !string.Equals(o.PlatformKey, "UNKNOWN", StringComparison.OrdinalIgnoreCase))
            .GroupBy(o => o.PlatformKey + "/" + Path.GetFileNameWithoutExtension(o.RelativePath),
                StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() == 1)
            .Select(g => g.Single())
            .OrderBy(o => o.RelativePath, StringComparer.Ordinal)
            .Take(3).ToArray();
        Demand(eligible.Length == 3, "MEDIA_GUI_THREE_DISTINCT_ROMS_REQUIRED");
        var basePath = Path.Combine(Path.GetTempPath(), "rm-next06-fixture-" +
            Guid.NewGuid().ToString("N"));
        var root = Path.Combine(basePath, "MEDIAS");
        Directory.CreateDirectory(root);
        try
        {
            static byte[] Encode(SKEncodedImageFormat format)
            {
                using var bitmap = new SKBitmap(24, 16);
                bitmap.Erase(SKColors.CadetBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(format, 85);
                return data.ToArray();
            }
            static string Relative(FileObservation rom, string extension) =>
                rom.PlatformKey + "/covers/" +
                Path.GetFileNameWithoutExtension(rom.RelativePath) + extension;
            void Write(string relative, byte[] bytes)
            {
                var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(file)!);
                File.WriteAllBytes(file, bytes);
            }
            Write(Relative(eligible[0], ".png"), Encode(SKEncodedImageFormat.Png));
            Write(Relative(eligible[1], ".png"), Encode(SKEncodedImageFormat.Png));
            Write(Relative(eligible[1], ".jpg"), Encode(SKEncodedImageFormat.Jpeg));
            Write(eligible[0].PlatformKey + "/logos/Independent.png",
                Encode(SKEncodedImageFormat.Png));
            Write(eligible[0].PlatformKey + "/logos/Unsupported.webp",
                Encode(SKEncodedImageFormat.Webp));
            // Valid PNG header but truncated pixel data: not a decodable preview.
            Write(eligible[0].PlatformKey + "/logos/Truncated.png",
                Encode(SKEncodedImageFormat.Png).Take(33).ToArray());

            var aliases = eligible.Select(o => o.PlatformKey)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
            var kinds = new Dictionary<string, MediaKind>(StringComparer.OrdinalIgnoreCase)
                { ["covers"] = MediaKind.Cover, ["logos"] = MediaKind.Logo };
            var reader = new MediaIndexApplicationService(scope =>
                string.Equals(scope.MediaRoot, root, StringComparison.OrdinalIgnoreCase));
            var media = reader.ScanConfirmed(new MediaRootScope(root, aliases, kinds), true);
            Demand(media.ScanStatus == InventoryScanStatus.Complete &&
                media.Observations.Count == 6, "MEDIA_GUI_INDEX_REAL_SCAN_REQUIRED");
            var mapping = VerifiedScopeMapping.FromExplicitSelections(roms, media,
                aliases, "CI_EXPLICIT_VERIFIED_SCOPES");
            var preview = new MediaPreviewService(root, media);
            vm.Media.AttachSynthetic(roms, media, mapping, preview);
            vm.ShowCollectionCommand.Execute(null);
            void Select(FileObservation rom)
            {
                vm.SelectedRow = vm.Rows.Single(x =>
                    x.Observation.ObservationId == rom.ObservationId);
                vm.OpenFileCommand.Execute(null);
                Demand(vm.IsFileDetail, "MEDIA_GUI_DETAIL_NAVIGATION_FAILED");
            }
            Select(eligible[0]);
            Demand(vm.Media.EvidenceStatus.Contains("Correspondance possible",
                StringComparison.Ordinal) && vm.Media.Choices.Count >= 3 &&
                vm.Media.PreviewResult is null, "MEDIA_GUI_CANDIDATE_NOT_EXPLICIT");
            Capture(window, captures, "MEDIA-Candidate", 1120, 720);
            vm.Media.SelectedChoice = vm.Media.Choices.Single(x =>
                x.Observation.RelativePath.EndsWith(Relative(eligible[0], ".png"),
                    StringComparison.OrdinalIgnoreCase));
            Demand(vm.Media.PreviewResult is null, "MEDIA_GUI_AUTOMATIC_PREVIEW");
            await vm.Media.PreviewSelectedAsync();
            Demand(vm.Media.PreviewResult is { Status: MediaPreviewStatus.Ready, Image: not null } &&
                window.FindControl<Image>("MediaPreviewImage")?.Source is not null,
                "MEDIA_GUI_PNG_NOT_RENDERED");
            Capture(window, captures, "MEDIA-Ready-PNG", 1120, 720);
            vm.Media.ClearPreview();
            Demand(window.FindControl<Image>("MediaPreviewImage")?.Source is null,
                "MEDIA_GUI_PREVIEW_NOT_DISPOSED");
            Capture(window, captures, "MEDIA-Cancelled", 760, 480);

            vm.ShowCollectionCommand.Execute(null);
            Select(eligible[1]);
            Demand(vm.Media.EvidenceStatus.Contains("Plusieurs médias",
                StringComparison.Ordinal) &&
                vm.Media.Choices.Count(x => x.Relationship.Contains("ambiguë",
                    StringComparison.Ordinal)) == 2 &&
                vm.Media.PreviewResult is null, "MEDIA_GUI_AMBIGUITY_AUTOMATCH");
            Capture(window, captures, "MEDIA-Ambiguous", 1120, 720);
            vm.Media.SelectedChoice = vm.Media.Choices.Single(x =>
                x.Observation.RelativePath.EndsWith(Relative(eligible[1], ".jpg"),
                    StringComparison.OrdinalIgnoreCase));
            await vm.Media.PreviewSelectedAsync();
            Demand(vm.Media.PreviewResult?.Status == MediaPreviewStatus.Ready &&
                window.FindControl<Image>("MediaPreviewImage")?.Source is not null,
                "MEDIA_GUI_JPEG_NOT_RENDERED");
            Capture(window, captures, "MEDIA-Ready-JPEG", 1120, 720);

            var claim = new ExplicitMediaReference(eligible[0].ObservationId,
                "MEDIAS/" + Relative(eligible[0], ".png"), "SYNTHETIC_PATH_CLAIM",
                "CI_ONLY_REFERENCE_NOT_APPROVAL");
            vm.Media.AttachSynthetic(roms, media, mapping, preview, [claim]);
            vm.ShowCollectionCommand.Execute(null);
            Select(eligible[0]);
            Demand(vm.Media.EvidenceStatus.Contains("Référence de chemin",
                StringComparison.Ordinal) &&
                vm.Media.EvidenceDetails.Contains("CI_ONLY_REFERENCE_NOT_APPROVAL",
                    StringComparison.Ordinal), "MEDIA_GUI_REFERENCE_PROVENANCE_MISSING");
            Capture(window, captures, "MEDIA-Referenced", 1120, 720);

            vm.ShowCollectionCommand.Execute(null);
            Select(eligible[2]);
            Demand(vm.Media.EvidenceStatus.Contains("Aucun média correspondant",
                StringComparison.Ordinal), "MEDIA_GUI_ABSENCE_WITHOUT_COMPLETE");
            Capture(window, captures, "MEDIA-Absent", 1120, 720);

            vm.Media.AttachSynthetic(roms,
                media with { ScanStatus = InventoryScanStatus.Partial }, mapping, preview);
            vm.ShowCollectionCommand.Execute(null);
            Select(eligible[0]);
            Demand(vm.Media.EvidenceStatus.Contains("Comparaison indisponible",
                StringComparison.Ordinal), "MEDIA_GUI_PARTIAL_FALSE_ABSENCE");
            Capture(window, captures, "MEDIA-NotComparable", 760, 480);

            vm.Media.AttachSynthetic(roms, media, mapping, preview);
            vm.ShowCollectionCommand.Execute(null);
            Select(eligible[0]);
            vm.Media.SelectedChoice = vm.Media.Choices.Single(x =>
                x.Observation.RelativePath.EndsWith("Unsupported.webp",
                    StringComparison.OrdinalIgnoreCase));
            await vm.Media.PreviewSelectedAsync();
            Demand(vm.Media.PreviewResult?.Status == MediaPreviewStatus.Unsupported,
                "MEDIA_GUI_WEBP_NOT_UNSUPPORTED");
            Capture(window, captures, "MEDIA-Unsupported", 1120, 720);

            vm.Media.SelectedChoice = vm.Media.Choices.Single(x =>
                x.Observation.RelativePath.EndsWith("Truncated.png",
                    StringComparison.OrdinalIgnoreCase));
            await vm.Media.PreviewSelectedAsync();
            Demand(vm.Media.PreviewResult?.Status is MediaPreviewStatus.DecodeFailed or
                MediaPreviewStatus.Rejected, "MEDIA_GUI_TRUNCATED_NOT_REFUSED");
            Capture(window, captures, "MEDIA-DecodeFailure", 1120, 720);

            using var refused = await new MediaPreviewService().LoadAsync(
                new MediaPreviewRequest(media.RunId, media.MediaRootRef,
                    media.Observations[0].MediaObservationId, 512));
            Demand(refused.Status == MediaPreviewStatus.Rejected &&
                refused.DiagnosticCode == "REAL_MEDIA_PREVIEW_HOLD",
                "MEDIA_GUI_PUBLIC_PREVIEW_DENY_FAILED");
            var deniedMedia = new MediaIndexApplicationService();
            var deniedScan = deniedMedia.ScanConfirmed(new MediaRootScope(root), true);
            Demand(deniedScan.Diagnostics.Any(d => d.Code == "REAL_MEDIA_SCAN_HOLD"),
                "MEDIA_GUI_PUBLIC_MEDIA_DENY_FAILED");
            Console.WriteLine("MEDIA_GUI_WINDOWS_CAPTURE_PASS count=10 no_xml=true " +
                "manual_selection=true all_covers_unapproved=true public_preview_denied=true");
        }
        finally
        {
            vm.Media.ClearPreview();
            if (Directory.Exists(basePath)) Directory.Delete(basePath, recursive: true);
        }
    }

    private static async Task ExerciseAsync(ObservedLibraryWindow window,
        IReadOnlyDictionary<string, string> roots, string captures)
    {
        var vm = window.ModelForCi;
        var inventory = vm.Inventory;
        Demand(!vm.HasSnapshot && !vm.CanInventoryFixture &&
            inventory.Current is null && inventory.LastAttempt is null,
            "N05_GUI_EMPTY_START_FAILED");
        Capture(window, captures, "N05-Empty", 760, 480);

        foreach (var name in new[] { "IDEAL", "CHAOS", "MYSTERY" })
        {
            // Explicit aliases for generated fixture platforms; no implicit game identification.
            var aliases = Directory.EnumerateDirectories(Path.Combine(roots[name], "ROMS"))
                .Select(Path.GetFileName).Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x!)
                .ToDictionary(x => x, x => x, StringComparer.OrdinalIgnoreCase);
            var selected = window.SelectFixtureForCi(name, platformAliases: aliases);
            Demand(selected.Accepted && !vm.CanInventoryFixture &&
                inventory.Phase == InventoryRunPhase.Ready,
                "N05_GUI_SELECTION_OR_CONSENT_FAILED_" + name);
            inventory.ConsentConfirmed = true;
            Demand(vm.CanInventoryFixture, "N05_GUI_CONFIRMATION_NOT_ACCEPTED");
            await inventory.StartConfirmedAsync();
            var snapshot = inventory.Current ?? throw new InvalidDataException("N05_CURRENT_MISSING");
            Demand(inventory.Phase == InventoryRunPhase.Completed &&
                snapshot is { ScanStatus: InventoryScanStatus.Complete } &&
                snapshot.Observations.Count > 0 &&
                snapshot.ConfirmedGameCount is null &&
                vm.HasSnapshot && !inventory.IsBusy &&
                snapshot.Observations.All(x => x.SourceKind == "FILESYSTEM_OBSERVATION"),
                "N05_GUI_REAL_SCANNER_FAILED_" + name);
            Demand(vm.Summary.Contains("jeux identifiés : inconnu",
                StringComparison.Ordinal), "N05_GUI_FALSE_GAME_COUNT_" + name);
            vm.ShowDashboardCommand.Execute(null);
            Capture(window, captures, "N05-" + name + "-Dashboard", 1120, 720);

            vm.ShowCollectionCommand.Execute(null);
            Demand(vm.IsCollection && vm.Rows.Count > 0 &&
                vm.MatchingFileCount == snapshot.Observations.Count,
                "N05_GUI_COLLECTION_NOT_OBSERVATIONS_" + name);
            if (name == "MYSTERY")
            {
                Demand(!Directory.EnumerateFiles(roots[name], "*.xml",
                    SearchOption.AllDirectories).Any(), "N05_MYSTERY_XML_PRESENT");
                vm.PlatformFilter = vm.PlatformOptions.FirstOrDefault(x => x != "Toutes") ?? "Toutes";
                Demand(vm.Rows.Count > 0, "N05_MYSTERY_PLATFORM_FILTER_EMPTY");
                vm.PlatformFilter = "Toutes";
                vm.ExtensionFilter = vm.ExtensionOptions.FirstOrDefault(x => x != "Toutes") ?? "Toutes";
                Demand(vm.Rows.Count > 0, "N05_MYSTERY_EXTENSION_FILTER_EMPTY");
                vm.ExtensionFilter = "Toutes";
                vm.SearchText = vm.Rows[0].ProvisionalLabel;
                Demand(vm.Rows.Count > 0, "N05_MYSTERY_SEARCH_EMPTY");
                vm.SearchText = "";
                Capture(window, captures, "N05-MYSTERY-Collection", 1120, 720);
                Capture(window, captures, "N05-MYSTERY-Compact", 760, 480);
                vm.SelectedRow = vm.Rows[0];
                vm.OpenFileCommand.Execute(null);
                Demand(vm.IsFileDetail && vm.SelectedRow is not null &&
                    vm.SelectedRow.Observation.SourceKind == "FILESYSTEM_OBSERVATION" &&
                    vm.MediaNotice.Contains("non associée", StringComparison.Ordinal),
                    "N05_MYSTERY_FILE_DETAILS_FALSE_IDENTIFICATION");
                Capture(window, captures, "N05-MYSTERY-FileDetail", 1120, 720);
                await ExerciseMediaAsync(window, snapshot, captures);
                vm.ReturnCollectionCommand.Execute(null);
                Demand(vm.IsCollection, "N05_MYSTERY_DETAIL_RETURN_FAILED");
            }
            else
                Capture(window, captures, "N05-" + name + "-Collection", 1120, 720);
            Console.WriteLine("N05_GUI_" + name + "_PASS observed=" +
                snapshot.ObservedFileCount + " confirmedGames=UNKNOWN no_xml=true");
        }

        // No public production service can scan even these same disposable fixtures.
        await using var denied = new InventoryApplicationService();
        var preflight = denied.PrepareSelection(new InventoryScope(roots["MYSTERY"]));
        Demand(preflight.Accepted, "N05_PUBLIC_PREFLIGHT_FAILED");
        var refusal = await denied.StartConfirmedScanAsync(
            new InventoryRunRequest(preflight.SelectionVersion, ExplicitUserConsent: true));
        Demand(refusal.Phase == InventoryRunPhase.Rejected &&
            refusal.SafeDiagnosticCode == "REAL_LIBRARY_SCAN_HOLD" &&
            refusal.Current is null && refusal.LastAttempt is null,
            "N05_PUBLIC_DENY_BY_DEFAULT_FAILED");

        // The windows GUI must not manufacture a zero-count or a deleted state.
        Demand(!vm.SnapshotStatus.Contains("DELETED", StringComparison.OrdinalIgnoreCase) &&
            !vm.Summary.Contains("jeux identifiés : 0", StringComparison.OrdinalIgnoreCase),
            "N05_GUI_FALSE_ABSENCE");
        Console.WriteLine("N05_GUI_PRODUCTION_DENY_PASS consent_is_not_permission=true");
    }
}
#endif
