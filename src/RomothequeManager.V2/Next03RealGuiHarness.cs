#if NEXT03_CI
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2;

// Compiled into the CI-only TEST binary, not into the owner-facing portable ZIP.
// The production application retains InventoryApplicationService() deny-by-default.
internal static class Next03RealGuiHarness
{
    private static void Demand(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }

    private static bool HasReparseAncestor(string path)
    {
        for (DirectoryInfo? p = new(path); p is not null; p = p.Parent)
            if ((p.Attributes & FileAttributes.ReparsePoint) != 0) return true;
        return false;
    }

    public static void Configure(MainWindow main, string userProvidedOutput)
    {
        var runnerTemp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        Demand(OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
            !string.IsNullOrWhiteSpace(runnerTemp), "NEXT03_GUI_CI_ONLY");

        var runner = Path.GetFullPath(runnerTemp!);
        var output = Path.GetFullPath(userProvidedOutput);
        var parent = Directory.GetParent(output);
        Demand(parent is not null &&
            output.StartsWith(runner + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase) &&
            Path.GetFileName(output).Equals("demo-output", StringComparison.Ordinal) &&
            Path.GetFileName(parent!.FullName).StartsWith("NEXT03-REALGUI-", StringComparison.Ordinal) &&
            parent.Parent is not null &&
            Path.GetFullPath(parent.Parent.FullName).Equals(runner, StringComparison.OrdinalIgnoreCase) &&
            !HasReparseAncestor(output), "NEXT03_NOT_EXCLUSIVE_RUNNER_FIXTURE");
        var names = new[] { "IDEAL", "CHAOS", "MYSTERY" };
        var roots = names.ToDictionary(name => name, name => Path.Combine(output, name));
        foreach (var root in roots.Values)
            Demand(Directory.Exists(Path.Combine(root, "ROMS")) &&
                !HasReparseAncestor(root), "NEXT03_FIXTURE_NOT_GENERATED");
        var permitted = new HashSet<string>(roots.Values, StringComparer.OrdinalIgnoreCase);

        // Only these three exact, freshly generated CI paths can pass to DEV 1.
        // There is no public/environment/manifest permission in the distributed EXE.
        var service = new InventoryApplicationService(scope =>
            permitted.Contains(scope.LibraryRoot) &&
            scope.LibraryRoot.StartsWith(output + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase));
        var window = new InventoryPreviewWindow(service, roots);
        var captureFolder = Path.Combine(runner, "next03-real-gui-captures");
        Directory.CreateDirectory(captureFolder);

        main.Opened += (_, _) =>
            Dispatcher.UIThread.Post(() => window.Show(main), DispatcherPriority.Background);
        window.Opened += async (_, _) =>
        {
            try
            {
                await ExerciseAsync(window, roots, captureFolder);
                Console.WriteLine("NEXT03_REAL_GUI_WIRED_PASS service=canonical fixtures=IDEAL,CHAOS,MYSTERY");
            }
            catch (Exception error)
            {
                Environment.ExitCode = 43;
                Console.Error.WriteLine("NEXT03_REAL_GUI_WIRING_FAIL " +
                    error.GetType().Name + " / " + error.Message);
            }
            finally
            {
                window.Close();
                main.Close();
            }
        };
    }

    private static void Capture(InventoryPreviewWindow window, string folder, string name,
        int width, int height)
    {
        window.Width = width;
        window.Height = height;
        Demand(window.Content is Control visual && window.IsVisible,
            "NEXT03_REAL_WINDOW_NOT_RENDERED");
        visual.Measure(new Size(width - 2, height - 2));
        visual.Arrange(new Rect(0, 0, width - 2, height - 2));
        using var image = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        image.Render(visual);
        var filename = Path.Combine(folder, name + "-" + width + "x" + height + ".png");
        image.Save(filename, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(filename).Length > 4000, "NEXT03_REAL_SCREENSHOT_BLANK");
        Console.WriteLine("NEXT03_REAL_GUI_CAPTURE_PASS name=" + Path.GetFileName(filename));
    }

    private static async Task ExerciseAsync(
        InventoryPreviewWindow window, IReadOnlyDictionary<string, string> roots, string captures)
    {
        var vm = window.ModelForSyntheticRecipe;
        Demand(vm.Phase == InventoryRunPhase.NoSelection &&
            vm.LastAttempt is null && !vm.CanStart, "NEXT03_REAL_INITIAL_STATE_WRONG");
        Capture(window, captures, "NEXT03-Empty", 760, 480);

        foreach (var (name, count, duplicates) in new[]
        {
            ("IDEAL", 32, 0), ("CHAOS", 33, 3), ("MYSTERY", 32, 0)
        })
        {
            Demand(window.SelectSyntheticFixtureForRecipe(name).Accepted &&
                vm.Phase == InventoryRunPhase.Ready && vm.Current is null &&
                vm.LastAttempt is null && !vm.CanStart,
                "NEXT03_REAL_EXPLICIT_SELECT_FAILED_" + name);
            Demand(!vm.PrepareSelectedFolder(new InventoryScope(Path.Combine(
                roots[name], "NOT_A_LIBRARY"))).Accepted,
                "NEXT03_REAL_UNSAFE_SCOPE_ACCEPTED");
            Demand(!vm.CanStart, "NEXT03_REAL_UNCONFIRMED_START_AVAILABLE");
            vm.ConsentConfirmed = true;
            await vm.StartConfirmedAsync();
            Demand(vm.Phase == InventoryRunPhase.Completed &&
                vm.Current?.ScanStatus == InventoryScanStatus.Complete &&
                vm.Current.ObservedFileCount == count &&
                vm.Current.TotalFileCount == count &&
                vm.Current.PotentialDuplicates.Count == duplicates &&
                vm.Current.ConfirmedGameCount is null &&
                vm.Observations.Count == count &&
                vm.SourceLabel == "FILESYSTEM_OBSERVATION" &&
                !vm.ConsentConfirmed && !vm.IsBusy, "NEXT03_REAL_SCAN_FAILED_" + name);
            Capture(window, captures, "NEXT03-" + name + "-Complete", 1120, 720);
            Console.WriteLine("NEXT03_REAL_" + name + "_GUI_PASS observed=" + count +
                " duplicates=" + duplicates + " identities=UNKNOWN");
        }

        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        bool block = false;
        var once = 0;
        Demand(window.SelectSyntheticFixtureForRecipe("IDEAL", _ =>
            {
                if (!block || Interlocked.Exchange(ref once, 1) != 0) return;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(25)))
                    throw new InvalidOperationException("NEXT03_GUI_PLAYLIST_WAIT_TIMEOUT");
            }).Accepted, "NEXT03_REAL_CANCEL_SELECT_FAILED");
        vm.ConsentConfirmed = true;
        await vm.StartConfirmedAsync();
        var lastComplete = vm.Current;
        Demand(lastComplete?.ScanStatus == InventoryScanStatus.Complete,
            "NEXT03_REAL_NO_COMPLETE_BEFORE_CANCEL");

        block = true;
        vm.ConsentConfirmed = true;
        var running = vm.StartConfirmedAsync();
        try
        {
            Demand(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(22))),
                "NEXT03_REAL_PLAYLIST_NOT_REACHED");
            Demand(vm.IsBusy && vm.IsProgressIndeterminate &&
                !vm.CanChangeFolder && !vm.CanStart &&
                !window.SelectSyntheticFixtureForRecipe("CHAOS").Accepted,
                "NEXT03_REAL_GUI_SINGLE_FLIGHT_FAILED");
            Capture(window, captures, "NEXT03-Running", 760, 480);
            Demand(vm.RequestCancellation() &&
                vm.Phase == InventoryRunPhase.CancellationRequested &&
                !vm.CanChangeFolder, "NEXT03_REAL_CANCEL_NOT_REQUESTED");
            Capture(window, captures, "NEXT03-CancellationRequested", 760, 480);
        }
        finally { block = false; release.Set(); }
        await running;
        Demand(ReferenceEquals(lastComplete, vm.Current) &&
            vm.LastAttempt?.ScanStatus == InventoryScanStatus.Partial &&
            vm.LastAttempt.Diagnostics.Any(x => x.Code == "CANCELLED") &&
            vm.LastAttempt.TotalFileCount is null &&
            !vm.IsBusy && vm.CanChangeFolder,
            "NEXT03_REAL_CANCEL_DID_NOT_PRESERVE_CURRENT");
        vm.ShowAttempt();
        Capture(window, captures, "NEXT03-CancelledPartial", 1120, 720);
        vm.ShowComplete();
        Demand(ReferenceEquals(lastComplete, vm.DisplayedSnapshot),
            "NEXT03_REAL_LAST_COMPLETE_NOT_VISIBLE");

        // The default public constructor must still refuse the same generated
        // scope even with consent: never relax this safety boundary.
        await using var denied = new InventoryApplicationService();
        var selection = denied.PrepareSelection(new InventoryScope(roots["IDEAL"]));
        Demand(selection.Accepted, "NEXT03_PUBLIC_PREFLIGHT_FAILED");
        var refusal = await denied.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        Demand(refusal.Phase == InventoryRunPhase.Rejected &&
            refusal.SafeDiagnosticCode == "REAL_LIBRARY_SCAN_HOLD" &&
            refusal.LastAttempt is null, "NEXT03_PUBLIC_DENY_BY_DEFAULT_BYPASSED");
        Console.WriteLine("NEXT03_REAL_GUI_DENY_BY_DEFAULT_PASS realLibraries=BLOCKED");
    }
}
#endif
