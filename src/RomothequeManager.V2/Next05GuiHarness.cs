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
using RomothequeManager.V2.Persistence;
using System.Diagnostics;

namespace RomothequeManager.V2;

// This class cannot exist in a standard Windows build. Only approved, generated
// runner-temp fixture roots receive an internal service permission predicate.
internal static class Next05GuiHarness
{
    private static void Demand(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    // The separate child process restores SQLite without ever opening an IDEAL source.
    internal static int VerifyReopened(string dbPath, string romRootId,
        string mediaRootId, string expectedCover)
    {
        Demand(OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true",
            "UI05_CHILD_CI_HOST_REQUIRED");
        using var store = new SqliteCatalogStore(dbPath);
        using var restored = new RestoredCatalogSession(store, romRootId, mediaRootId);
        Demand(restored.StatusLabel.Contains("OFFLINE / STALE", StringComparison.Ordinal) &&
            restored.DisplayedInventory?.Observations.Count > 0,
            "UI05_CHILD_OFFLINE_RESTORE_FAILED");
        restored.SelectRom(restored.DisplayedInventory!.Observations.First(x =>
            x.Kind == InventoryKind.PotentialRom &&
            string.Equals(x.PlatformKey, expectedCover.Split('|')[0], StringComparison.Ordinal)));
        Demand(restored.CurrentChoiceLabel.Contains(expectedCover.Split('|')[1],
                StringComparison.Ordinal) && !restored.FixtureWriteAllowed,
            "UI05_CHILD_DECISION_NOT_RESTORED");
        Console.WriteLine("SQLITE_UI_NEW_PROCESS_PASS offline_stale=true personal_access=false");
        return 0;
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
        Point? previewPixel = null;
        if (name.StartsWith("MEDIA-", StringComparison.Ordinal))
        {
            // Aim at the actual evidence/status or image. Scrolling to the end showed
            // ONLY diagnostics below the media panel, invalidating the old screenshots.
            var scroll = window.FindControl<ScrollViewer>("RootScroll")!;
            var evidence = window.FindControl<TextBlock>("MediaEvidenceStatus")!;
            var previewStatus = window.FindControl<TextBlock>("MediaPreviewStatusText")!;
            var image = window.FindControl<Image>("MediaPreviewImage")!;
            var isImage = name is "MEDIA-Ready-PNG" or "MEDIA-Ready-JPEG" or
                "MEDIA-Alpha-PNG";
            var isPreviewStatus = name is "MEDIA-Cancelled" or "MEDIA-Unsupported" or
                "MEDIA-DecodeFailure";
            Control target = isImage ? image : isPreviewStatus ? previewStatus : evidence;
            // The full ScrollViewer content has stable, unscrolled coordinates.
            // Relative-to-visual + previous Offset was incorrect before the actual
            // Avalonia layout pass; use absolute content Y then flush layout.
            window.UpdateLayout();
            var content = scroll.Content as Control ??
                throw new InvalidDataException("MEDIA_SCREENSHOT_CONTENT_MISSING");
            var contentTop = target.TranslatePoint(new Point(0, 0), content) ??
                throw new InvalidDataException("MEDIA_SCREENSHOT_TARGET_UNAVAILABLE");
            var desiredTop = isImage
                ? (height == 480 ? 175.0 : 275.0)
                : (height == 480 ? 155.0 : 190.0);
            var maxOffset = Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height);
            scroll.Offset = new Vector(0,
                Math.Clamp(contentTop.Y - desiredTop, 0, maxOffset));
            window.UpdateLayout();
            visual.Measure(new Size(width - 2, height - 2));
            visual.Arrange(new Rect(0, 0, width - 2, height - 2));
            var after = target.TranslatePoint(new Point(0, 0), visual);
            if (after is null || after.Value.Y < 65 ||
                after.Value.Y > height - (isImage ? 120 : 50))
                throw new InvalidDataException(
                    "MEDIA_SCREENSHOT_TARGET_OUTSIDE_VIEWPORT y=" +
                    (after?.Y.ToString("F1") ?? "null") +
                    " expected=65.." + (height - (isImage ? 120 : 50)) +
                    " targetContentY=" + contentTop.Y.ToString("F1") +
                    " offset=" + scroll.Offset.Y.ToString("F1") +
                    " extent=" + scroll.Extent.Height.ToString("F1") +
                    " viewport=" + scroll.Viewport.Height.ToString("F1"));
            // This assertion proves the requested state is in the viewport, not
            // merely present somewhere inside an oversized ScrollViewer.
            if (isImage)
            {
                var center = image.TranslatePoint(new Point(
                    image.Bounds.Width / 2, image.Bounds.Height / 2), visual);
                Demand(center.HasValue && center.Value.Y > 110 &&
                    center.Value.Y < height - 30 && image.Source is not null,
                    "MEDIA_SCREENSHOT_IMAGE_NOT_VISIBLE");
                previewPixel = center;
            }
            else
            {
                var label = target as TextBlock;
                Demand(label?.Text is { Length: > 0 } &&
                    (isPreviewStatus || label.Text.Contains("non", StringComparison.OrdinalIgnoreCase) ||
                     label.Text.Contains("média", StringComparison.OrdinalIgnoreCase) ||
                     label.Text.Contains("Correspondance", StringComparison.OrdinalIgnoreCase) ||
                     label.Text.Contains("Plusieurs", StringComparison.OrdinalIgnoreCase) ||
                     label.Text.Contains("Référence", StringComparison.OrdinalIgnoreCase) ||
                     label.Text.Contains("Comparaison", StringComparison.OrdinalIgnoreCase)),
                    "MEDIA_SCREENSHOT_STATE_NOT_VISIBLE");
            }
        }
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height),
            new Vector(96, 96));
        bitmap.Render(visual);
        var filename = Path.Combine(directory, name + "-" + width + "x" + height + ".png");
        bitmap.Save(filename, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(filename).Length > 4000, "N05_GUI_SCREENSHOT_EMPTY");
        if (previewPixel is { } point)
        {
            // Decode the ACTUAL Avalonia screenshot and inspect the visible center
            // pixel; checking Image.Source is not evidence that pixels were drawn.
            using var captured = SKBitmap.Decode(filename) ??
                throw new InvalidDataException("MEDIA_SCREENSHOT_PNG_UNREADABLE");
            var px = captured.GetPixel((int)Math.Round(point.X), (int)Math.Round(point.Y));
            var expected = name switch
            {
                "MEDIA-Alpha-PNG" => new SKColor(134, 36, 27), // RGBA(240,40,16,128) over #1B1F25
                _ => SKColors.CadetBlue
            };
            var tolerance = name == "MEDIA-Ready-JPEG" ? 30 : 22;
            Demand(Math.Abs(px.Red - expected.Red) <= tolerance &&
                Math.Abs(px.Green - expected.Green) <= tolerance &&
                Math.Abs(px.Blue - expected.Blue) <= tolerance,
                "MEDIA_SCREENSHOT_VISIBLE_PIXEL_MISMATCH_" + name);
            Console.WriteLine("MEDIA_SCREENSHOT_PIXEL_PASS " + name +
                " rgb=" + px.Red + "," + px.Green + "," + px.Blue +
                " expected=" + expected.Red + "," + expected.Green + "," + expected.Blue +
                " viewport=" + width + "x" + height);
        }
        if (name.StartsWith("MEDIA-", StringComparison.Ordinal))
            Console.WriteLine("MEDIA_SCREENSHOT_FRAMED_PASS " + name +
                " viewport=" + width + "x" + height);
        Console.WriteLine("N05_GUI_CAPTURE_PASS " + Path.GetFileName(filename));
    }

    // Capture the real user-visible historical cover controls, never the diagnostics
    // at the end of an oversized ScrollViewer.
    private static void CaptureRestored(ObservedLibraryWindow window,
        string captures, string name, int width, int height)
    {
        var isOffline = name == "SQLITE-Offline";
        window.Width = width;
        window.Height = height;
        // The preceding MEDIA scenario was captured at 760x480 with a very deep
        // ScrollViewer offset. Reset, RESIZE and flush layout before positioning
        // the restored status. The old screenshot was 76% black pixels and only
        // showed the footer, despite assertions that a snapshot existed.
        var scroll = window.FindControl<ScrollViewer>("RootScroll")!;
        scroll.Offset = Vector.Zero;
        window.UpdateLayout();
        var target = isOffline
            ? window.FindControl<TextBlock>("RestoredStatusBanner")!
            : window.FindControl<TextBlock>("RestoredCoverStatus")!;
        var content = (Control)scroll.Content!;
        var point = target.TranslatePoint(new Point(0, 0), content) ??
            throw new InvalidDataException("UI05_RESTORED_TARGET_NOT_LAID_OUT");
        var desiredTop = isOffline ? 190.0 : height == 480 ? 140.0 : 230.0;
        scroll.Offset = new Vector(0, Math.Clamp(point.Y - desiredTop, 0,
            Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
        window.UpdateLayout();
        var viewport = target.TranslatePoint(new Point(0, 0), (Control)window.Content!);
        Demand(viewport.HasValue && viewport.Value.Y >= 50 &&
            viewport.Value.Y < height - 80 &&
            !string.IsNullOrWhiteSpace(target.Text) &&
            (!isOffline || target.Text.Contains("OFFLINE / STALE",
                StringComparison.Ordinal)), "UI05_RESTORED_TARGET_NOT_IN_VIEWPORT");

        Capture(window, captures, name, width, height);
        if (isOffline)
        {
            // Verify the ACTUAL saved pixels, not merely a ViewModel text or
            // a control's layout coordinates. Reject a mostly-black output.
            var path = Path.Combine(captures, name + "-" + width + "x" + height + ".png");
            using var bitmap = SKBitmap.Decode(path) ??
                throw new InvalidDataException("UI05_OFFLINE_SCREENSHOT_UNREADABLE");
            var samples = 0;
            var visible = 0;
            for (var y = 80; y <= height - 80; y += 70)
            {
                for (var x = 80; x <= width - 80; x += 160)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    samples++;
                    if (pixel.Red + pixel.Green + pixel.Blue > 15) visible++;
                }
            }
            Demand(visible >= samples * 3 / 4,
                "UI05_OFFLINE_SCREENSHOT_BLACK_FRAME");
            Console.WriteLine("SQLITE_UI_OFFLINE_VISIBLE_PIXELS_PASS samples=" +
                samples + " visible=" + visible + " status=OFFLINE_STALE");
        }
        Console.WriteLine("SQLITE_UI_SCREENSHOT_FRAMED_PASS " + name +
            " viewport=" + width + "x" + height);
    }

    private static void ExercisePersistence(ObservedLibraryWindow window,
        InventorySnapshot ideal, string captures)
    {
        var vm = window.ModelForCi;
        var eligible = ideal.Observations.First(x =>
            x.Kind == InventoryKind.PotentialRom &&
            !string.Equals(x.PlatformKey, "UNKNOWN", StringComparison.OrdinalIgnoreCase));
        var fixture = Path.Combine(Path.GetTempPath(),
            "rm-persist01-fixture-" + Guid.NewGuid().ToString("N"));
        var dbPath = Path.Combine(fixture, "ROMOTHEQUE_MANAGER", "data", "catalog.sqlite");
        var mediaFixture = Path.Combine(Path.GetTempPath(),
            "rm-next06-fixture-" + Guid.NewGuid().ToString("N"));
        var mediaRoot = Path.Combine(mediaFixture, "MEDIAS");
        var libraryId = Guid.NewGuid().ToString("D");
        var romRootId = Guid.NewGuid().ToString("D");
        var mediaRootId = Guid.NewGuid().ToString("D");
        try
        {
            var imageDirectory = Path.Combine(mediaRoot, eligible.PlatformKey, "covers");
            Directory.CreateDirectory(imageDirectory);
            using (var bitmap = new SKBitmap(24, 16))
            {
                bitmap.Erase(SKColors.CadetBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 90);
                File.WriteAllBytes(Path.Combine(imageDirectory, "FixtureCover.png"),
                    data.ToArray());
            }
            var aliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                { [eligible.PlatformKey] = eligible.PlatformKey };
            var kinds = new Dictionary<string, MediaKind>(StringComparer.OrdinalIgnoreCase)
                { ["covers"] = MediaKind.Cover };
            var reader = new MediaIndexApplicationService(scope =>
                string.Equals(scope.MediaRoot, mediaRoot, StringComparison.OrdinalIgnoreCase));
            var media = reader.ScanConfirmed(new MediaRootScope(mediaRoot, aliases, kinds), true);
            Demand(media.ScanStatus == InventoryScanStatus.Complete &&
                media.Observations.Count == 1 &&
                media.Observations[0].HeaderValidated,
                "UI05_REAL_SYNTHETIC_MEDIA_INDEX_FAILED");
            using (var seed = new SqliteCatalogStore(dbPath))
            {
                seed.AddLibrary(new CatalogLibrary(libraryId, "IDEAL / fictive"));
                seed.AddStorageRoot(new CatalogStorageRoot(romRootId, libraryId, "ROMS", "ROMS"));
                seed.AddStorageRoot(new CatalogStorageRoot(mediaRootId, libraryId, "MEDIAS", "MEDIAS"));
                seed.SaveAttempt(romRootId, ideal);
                seed.SaveAttempt(mediaRootId, media);
            }
            // Remove the image BEFORE restoring the catalogue: no source pixels
            // can be accidentally loaded; only the saved historical description remains.
            Directory.Delete(mediaFixture, recursive: true);
            var session = new RestoredCatalogSession(new SqliteCatalogStore(dbPath),
                romRootId, mediaRootId, fixtureWriteAllowed: true);
            window.AttachRestoredForCi(session);
            Demand(vm.HasRestoredSession && !vm.CanInventoryFixture &&
                window.FindControl<Border>("LiveInventoryPanel") is { IsVisible: false } &&
                vm.HasSnapshot && vm.SnapshotStatus.Contains("OFFLINE / STALE",
                    StringComparison.Ordinal) && vm.MatchingFileCount == ideal.Observations.Count,
                "UI05_RESTORED_IDEAL_NOT_HISTORICAL");
            vm.ShowDashboardCommand.Execute(null);
            CaptureRestored(window, captures, "SQLITE-Offline", 1120, 720);
            vm.ShowCollectionCommand.Execute(null);
            Demand(vm.Rows.Count > 0 && vm.MatchingFileCount == ideal.Observations.Count,
                "UI05_SAVED_COLLECTION_MISSING");
            vm.SelectedRow = vm.Rows.Single(x =>
                x.Observation.ObservationId == eligible.ObservationId);
            vm.OpenFileCommand.Execute(null);
            Demand(vm.IsFileDetail && session.Candidates.Count == 1 &&
                session.CurrentChoiceLabel.Contains("Aucune jaquette", StringComparison.Ordinal) &&
                !session.SaveCommand.CanExecute(null),
                "UI05_COVER_NOT_EXPLICIT");
            CaptureRestored(window, captures, "SQLITE-Cover-Unlinked", 760, 480);
            session.SelectedCandidate = session.Candidates.Single();
            Demand(!session.SaveCommand.CanExecute(null),
                "UI05_PREVIEW_OR_SELECTION_AUTO_APPROVAL");
            session.Confirmation = true;
            Demand(session.SaveCommand.CanExecute(null), "UI05_CONFIRMATION_MISSING");
            session.SaveCommand.Execute(null);
            Demand(session.SaveStatus.Contains("enregistrée", StringComparison.Ordinal) &&
                session.CurrentChoiceLabel.Contains("FixtureCover.png", StringComparison.Ordinal) &&
                !session.Confirmation,
                "UI05_SAVE_OR_ACTIVE_DECISION_FAILED");
            CaptureRestored(window, captures, "SQLITE-Cover-Saved", 1120, 720);
            using (var reopened = new SqliteCatalogStore(dbPath))
                Demand(reopened.LoadLastKnown(romRootId).Decisions.Count == 1,
                    "UI05_SAVE_NOT_IN_SQLITE");
            // Child binary genuinely starts a NEW OS process, with no ROM/MEDIA path
            // permission, reads only the same fixture-owned SQLite DB.
            var exe = Environment.ProcessPath ?? throw new InvalidDataException("UI05_EXE_MISSING");
            var start = new ProcessStartInfo(exe)
            {
                UseShellExecute = false, RedirectStandardOutput = true,
                RedirectStandardError = true, CreateNoWindow = true
            };
            start.ArgumentList.Add("--ci-next05-sqlite-reopen");
            start.ArgumentList.Add(dbPath);
            start.ArgumentList.Add(romRootId);
            start.ArgumentList.Add(mediaRootId);
            start.ArgumentList.Add(eligible.PlatformKey + "|FixtureCover.png");
            using var child = Process.Start(start) ??
                throw new InvalidDataException("UI05_CHILD_START_FAILED");
            var stdout = child.StandardOutput.ReadToEnd();
            var stderr = child.StandardError.ReadToEnd();
            if (!child.WaitForExit(90000))
            {
                child.Kill(entireProcessTree: true);
                throw new InvalidDataException("UI05_CHILD_TIMEOUT");
            }
            Demand(child.ExitCode == 0 &&
                stdout.Contains("SQLITE_UI_NEW_PROCESS_PASS", StringComparison.Ordinal),
                "UI05_CHILD_RESTORE_FAILED_" + child.ExitCode);
            Console.WriteLine("SQLITE_UI_IDEAL_PASS offline_stale=true explicit_save=true " +
                "sqlite_reopen_new_process=true no_personal_io=true captures=3");
            session.Dispose();
        }
        finally
        {
            if (Directory.Exists(mediaFixture))
                Directory.Delete(mediaFixture, recursive: true);
            if (Directory.Exists(fixture))
                Directory.Delete(fixture, recursive: true);
        }
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
            static byte[] Encode(SKEncodedImageFormat format, SKColor? color = null)
            {
                using var bitmap = new SKBitmap(24, 16);
                bitmap.Erase(color ?? SKColors.CadetBlue);
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
            // Source alpha 128: backend Skia returns *premultiplied* RGBA.
            // Avalonia must compose this translucent PNG correctly over #1B1F25.
            Write(eligible[0].PlatformKey + "/logos/Transparent.png",
                Encode(SKEncodedImageFormat.Png, new SKColor(240, 40, 16, 128)));
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
                media.Observations.Count == 7, "MEDIA_GUI_INDEX_REAL_SCAN_REQUIRED");
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
            // Independent translucent PNG: real 760x480 Avalonia compositing canary.
            vm.Media.SelectedChoice = vm.Media.Choices.Single(x =>
                x.Observation.RelativePath.EndsWith("Transparent.png",
                    StringComparison.OrdinalIgnoreCase));
            Demand(vm.Media.PreviewResult is null, "MEDIA_GUI_ALPHA_AUTO_PREVIEW");
            await vm.Media.PreviewSelectedAsync();
            Demand(vm.Media.PreviewResult is { Status: MediaPreviewStatus.Ready, Image: not null },
                "MEDIA_GUI_ALPHA_DECODER_NOT_READY");
            Capture(window, captures, "MEDIA-Alpha-PNG", 760, 480);
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
            Console.WriteLine("MEDIA_GUI_WINDOWS_CAPTURE_PASS count=11 no_xml=true " +
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
        InventorySnapshot? preparedIdeal = null;
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
            if (name == "IDEAL") preparedIdeal = snapshot;
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

        // A second, independent phase restores ONLY saved IDEAL facts in SQLite.
        ExercisePersistence(window, preparedIdeal ??
            throw new InvalidDataException("UI05_IDEAL_NOT_PREPARED"), captures);

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
