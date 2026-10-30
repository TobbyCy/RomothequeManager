#if NEXT05_CI
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
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
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height),
            new Vector(96, 96));
        bitmap.Render(visual);
        var filename = Path.Combine(directory, name + "-" + width + "x" + height + ".png");
        bitmap.Save(filename, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(filename).Length > 4000, "N05_GUI_SCREENSHOT_EMPTY");
        Console.WriteLine("N05_GUI_CAPTURE_PASS " + Path.GetFileName(filename));
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
            var selected = window.SelectFixtureForCi(name);
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
