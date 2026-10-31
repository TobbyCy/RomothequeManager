#if NEXT05_CI
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.Inventory.RsA;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2;

/// <summary>Actual Avalonia Windows proof using a newly created, disposable RS-A fixture.
/// Only the CI-only executable contains this entrypoint. The normal EXE cannot enter it.</summary>
internal static class Next05RsAGuiHarness
{
    private static void Demand(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    public static void Configure(MainWindow main)
    {
        Demand(OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RUNNER_TEMP")),
            "UI_RSA_CI_WINDOWS_HOST_REQUIRED");

        var fixture = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"));
        var roms = Path.Combine(fixture, "ROMS");
        var gba = Path.Combine(roms, "gba");
        Directory.CreateDirectory(gba);
        // Every byte is invented in this process, under a new fixed RS-A temp scope.
        File.WriteAllText(Path.Combine(gba, "Fictitious.gba"), "FICTIONAL_METADATA_ONLY");
        for (var i = 0; i < 200; i++)
            File.WriteAllText(Path.Combine(gba, "Dummy-" + i.ToString("D3") + ".gba"),
                "SYNTHETIC_" + i);

        var roots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["RSA"] = roms
        };
        var service = new InventoryApplicationService(); // public service stays DENY
        var observed = ObservedLibraryWindow.ForVerifiedFixtures(service, roots);
        var rsA = new RsAUiViewModel(action =>
        {
            if (Dispatcher.UIThread.CheckAccess()) action();
            else Dispatcher.UIThread.Post(action);
        });
        observed.AttachRsAForCi(rsA);
        var captures = Path.Combine(
            Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "next05-rsa-ui-captures");
        Directory.CreateDirectory(captures);
        main.Opened += (_, _) =>
            Dispatcher.UIThread.Post(() => observed.Show(main), DispatcherPriority.Background);
        observed.Opened += async (_, _) =>
        {
            try
            {
                await ExerciseAsync(observed, rsA, captures);
                Console.WriteLine("UI_RSA_WINDOWS_GUI_PASS captures=5 fixture_only=true " +
                    "public_scan_hold=true sqlite_unchanged=true");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 47;
                Console.Error.WriteLine("UI_RSA_WINDOWS_GUI_FAIL " + ex.GetType().Name +
                    " " + (ex is InvalidDataException ? ex.Message : "UNEXPECTED"));
            }
            finally
            {
                observed.Close();
                main.Close();
                try
                {
                    if (Directory.Exists(fixture))
                        Directory.Delete(fixture, recursive: true);
                }
                catch (IOException) { Environment.ExitCode = 48; }
            }
        };
    }

    private static void Capture(ObservedLibraryWindow window, string captures,
        string name, int width, int height)
    {
        window.Width = width;
        window.Height = height;
        var scroll = window.FindControl<ScrollViewer>("RootScroll")!;
        var target = window.FindControl<TextBlock>("RsAPhaseLabel")!;
        scroll.Offset = Vector.Zero;
        window.UpdateLayout();
        var content = scroll.Content as Control ??
            throw new InvalidDataException("UI_RSA_CAPTURE_CONTENT_MISSING");
        var place = target.TranslatePoint(new Point(0, 0), content) ??
            throw new InvalidDataException("UI_RSA_CAPTURE_TARGET_MISSING");
        var desiredY = height == 480 ? 160.0 : 210.0;
        scroll.Offset = new Vector(0, Math.Clamp(place.Y - desiredY, 0,
            Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
        window.UpdateLayout();
        var surface = window.Content as Control ??
            throw new InvalidDataException("UI_RSA_CAPTURE_VISUAL_MISSING");
        var at = target.TranslatePoint(new Point(0, 0), surface);
        Demand(at.HasValue && at.Value.Y >= 45 && at.Value.Y < height - 45 &&
            !string.IsNullOrWhiteSpace(target.Text) &&
            window.FindControl<Border>("RsAUiPanel") is { IsVisible: true },
            "UI_RSA_CAPTURE_STATUS_OUTSIDE_VIEWPORT");
        surface.Measure(new Size(width - 2, height - 2));
        surface.Arrange(new Rect(0, 0, width - 2, height - 2));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height),
            new Vector(96, 96));
        bitmap.Render(surface);
        var file = Path.Combine(captures, name + "-" + width + "x" + height + ".png");
        bitmap.Save(file, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(file).Length > 4000, "UI_RSA_CAPTURE_EMPTY");
        Console.WriteLine("UI_RSA_CAPTURE_PASS " + Path.GetFileName(file));
    }

    private static async Task ExerciseAsync(ObservedLibraryWindow window,
        RsAUiViewModel rsA, string captures)
    {
        var vm = window.ModelForCi;
        Demand(vm.HasRsA && !vm.HasRestoredSession && !vm.HasSnapshot &&
            !rsA.CanStart && rsA.CurrentComplete is null &&
            window.FindControl<Border>("LiveInventoryPanel") is { IsVisible: false },
            "UI_RSA01_NO_AUTOMATIC_SCAN");
        Capture(window, captures, "RSA-Idle", 1120, 720);

        Demand(window.SelectRsAFixtureForCi("RSA", Guid.NewGuid().ToString("D")) &&
            rsA.HasSelection && !rsA.CanStart && !rsA.ConsentConfirmed &&
            rsA.Phase == RsAUiPhase.AwaitingConsent, "UI_RSA02_SELECTION_IS_NOT_PERMISSION");
        rsA.ConsentConfirmed = true;
        Demand(rsA.CanStart, "UI_RSA03_EXPLICIT_CONFIRMATION_REQUIRED");
        Capture(window, captures, "RSA-Consent", 760, 480);

        await rsA.StartConfirmedAsync();
        // Dispatcher may receive completion on a subsequent UI tick.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        var complete = rsA.CurrentComplete;
        Demand(rsA.Phase == RsAUiPhase.Complete &&
            complete is { ScanStatus: InventoryScanStatus.Complete } &&
            complete.ObservedFileCount == 201 &&
            rsA.LastAttempt == complete && !rsA.ConsentConfirmed &&
            vm.HasSnapshot && vm.Rows.Count > 0 &&
            complete.Observations.All(x => x.SourceKind == "RS_A_HANDLE_METADATA_FIXTURE"),
            "UI_RSA03_NATIVE_HANDLE_SCAN_NOT_DISPLAYED");
        vm.ShowCollectionCommand.Execute(null);
        Capture(window, captures, "RSA-Complete", 1120, 720);

        rsA.ConsentConfirmed = true;
        var scan = rsA.StartConfirmedAsync();
        Demand(rsA.RequestCancellation(), "UI_RSA04_CANCEL_NOT_ACCEPTED");
        await scan;
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Demand(ReferenceEquals(complete, rsA.CurrentComplete) &&
            !rsA.CanStart && !rsA.ConsentConfirmed &&
            rsA.Phase is RsAUiPhase.Failed or RsAUiPhase.Partial or RsAUiPhase.Complete,
            "UI_RSA04_CURRENT_CHANGED_OR_GRANT_REUSED");
        Capture(window, captures, "RSA-Cancelled", 760, 480);

        rsA.MarkOfflineForCi();
        Demand(rsA.IsOffline && !rsA.CanStart && !rsA.ConsentConfirmed &&
            ReferenceEquals(complete, rsA.CurrentComplete) &&
            rsA.SnapshotLabel.Contains("OFFLINE / STALE", StringComparison.Ordinal),
            "UI_RSA06_OFFLINE_STALE_NOT_PRESERVED");
        Capture(window, captures, "RSA-Offline", 1120, 720);

        try
        {
            RealRomInventoryGate.Scan();
            throw new InvalidDataException("UI_RSA08_PUBLIC_SCAN_UNEXPECTEDLY_OPEN");
        }
        catch (UnauthorizedAccessException error)
        {
            Demand(error.Message == "REAL_LIBRARY_SCAN_HOLD",
                "UI_RSA08_PUBLIC_GATE_DIAGNOSTIC_CHANGED");
        }
        Demand(!vm.HasRestoredSession && !vm.HasLiveMediaPanel &&
            window.FindControl<Border>("MediaPanel") is { IsVisible: false } &&
            window.FindControl<Border>("RestoredCoverPanel") is { IsVisible: false },
            "UI_RSA07_MEDIA_OR_SQLITE_MIXED");
        Console.WriteLine("UI_RSA01_08_GUI_FIXTURE_PASS current_retained=true " +
            "not_auto_scanned=true consent_one_shot=true cancelled=true offline_stale=true");
    }
}
#endif
