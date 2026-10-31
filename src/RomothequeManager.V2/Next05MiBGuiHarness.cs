#if NEXT05_CI
using System.Buffers.Binary;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media.MiB;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2;

/// <summary>Windows native fixture UI evidence; compiled out of the normal EXE.</summary>
internal static class Next05MiBGuiHarness
{
    private static void Demand(bool ok, string code)
    {
        if (!ok) throw new InvalidDataException(code);
    }
    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (var n = 0; n < 8; n++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }
    private static byte[] Png()
    {
        var b = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(b, 0);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(b.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20, 4), 1);
        b[24] = 8; b[25] = 6;
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(29, 4), Crc(b.AsSpan(12, 17)));
        return b;
    }
    private static string CreateFixture(int count)
    {
        var root = Path.Combine(Path.GetTempPath(),
            "rm-mia-fixture-" + Guid.NewGuid().ToString("N"));
        var media = Path.Combine(root, "MEDIAS");
        var folder = Path.Combine(media, "gba");
        Directory.CreateDirectory(folder);
        var png = Png();
        for (var n = 0; n < count; n++)
            File.WriteAllBytes(Path.Combine(folder, "Synthetic-" + n.ToString("D4") + ".png"), png);
        return media;
    }
    private static void Capture(MiBFixtureWindow window, string output, string state, int width, int height)
    {
        window.Width = width; window.Height = height;
        var scroll = window.FindControl<ScrollViewer>("RootScroll")!;
        var target = window.FindControl<TextBlock>("MiBPhase")!;
        scroll.Offset = Vector.Zero;
        window.UpdateLayout();
        var content = scroll.Content as Control ??
            throw new InvalidDataException("UI_MIB_CAPTURE_CONTENT_MISSING");
        var point = target.TranslatePoint(new Point(0, 0), content) ??
            throw new InvalidDataException("UI_MIB_CAPTURE_TARGET_MISSING");
        scroll.Offset = new Vector(0, Math.Clamp(point.Y - 165, 0,
            Math.Max(0, scroll.Extent.Height - scroll.Viewport.Height)));
        window.UpdateLayout();
        var surface = window.Content as Control ??
            throw new InvalidDataException("UI_MIB_CAPTURE_VISUAL_MISSING");
        var at = target.TranslatePoint(new Point(0, 0), surface);
        Demand(at.HasValue && at.Value.Y >= 35 && at.Value.Y < height - 35 &&
            !string.IsNullOrWhiteSpace(target.Text), "UI_MIB_CAPTURE_LABEL_NOT_VISIBLE");
        surface.Measure(new Size(width - 2, height - 2));
        surface.Arrange(new Rect(0, 0, width - 2, height - 2));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(surface);
        var file = Path.Combine(output, "MIB-" + state + "-" + width + "x" + height + ".png");
        bitmap.Save(file, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(file).Length > 4000, "UI_MIB_CAPTURE_EMPTY");
        Console.WriteLine("UI_MIB_CAPTURE_PASS " + Path.GetFileName(file));
    }
    private static void Pair(MiBFixtureWindow window, string output, string state)
    {
        Capture(window, output, state, 1120, 720);
        Capture(window, output, state, 760, 480);
    }

    private sealed class ArchiveVisualService : IMiBFixtureMediaApplicationService
    {
        public ArchiveVisualService(MiBRunView run) =>
            State = new(0, MiBPhase.NoSelection, false,
                run with { Availability = InventoryAvailability.Offline,
                    SourceFreshness = InventoryFreshness.Stale, IsHistoricalFixture = true },
                null, true, "MI_B_HISTORY_OFFLINE");
        public MiBProjectionState State { get; }
        public event EventHandler<MiBProjectionState>? StateChanged { add { } remove { } }
        public MiBSelectionResult SelectFixtureRoot(string root) => new(false, 0, "MI_B_ARCHIVE_READ_ONLY");
        public bool ConfirmMediaIndexConsent(long version, bool explicitUserAction) => false;
        public Task<MiBProjectionState> StartConfirmedAsync(long version, CancellationToken cancellationToken = default) =>
            Task.FromResult(State);
        public bool RequestCancellation() => false;
        public bool ClearSelection() => false;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    public static void Configure(MainWindow main)
    {
        Demand(OperatingSystem.IsWindows() &&
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true" &&
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RUNNER_TEMP")),
            "UI_MIB_WINDOWS_CI_HOST_REQUIRED");
        var a = CreateFixture(2000);
        var b = CreateFixture(2000);
        var output = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!,
            "next05-mib-ui-captures");
        Directory.CreateDirectory(output);
        var service = new MiBFixtureMediaApplicationService();
        var window = new MiBFixtureWindow(service, a);
        main.Opened += (_, _) =>
            Dispatcher.UIThread.Post(() => window.Show(main), DispatcherPriority.Background);
        window.Opened += async (_, _) =>
        {
            try
            {
                var vm = window.ModelForCi;
                Demand(!vm.CanStart && vm.CurrentComplete is null, "UI_MIB01_NO_AUTO_SCAN");
                Pair(window, output, "Idle");
                Demand(vm.Select() && !vm.CanStart, "UI_MIB01_SELECTION_NOT_CONSENT");
                Pair(window, output, "SelectedA");
                Demand(vm.Confirm() && vm.CanStart, "UI_MIB01_CONSENT_REQUIRED");
                Pair(window, output, "ConsentA");
                var runningA = vm.StartAsync();
                Demand(!vm.ConsentConfirmed, "UI_MIB01_CONSENT_NOT_CONSUMED");
                if (vm.IsBusy) Pair(window, output, "RunningA");
                await runningA;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(vm.CurrentComplete is { ScanStatus: InventoryScanStatus.Complete } &&
                    vm.LastAttempt is { ObservedMediaCount: 2000 }, "UI_MIB03_A_NOT_COMPLETE");
                var currentA = vm.CurrentComplete!;
                Pair(window, output, "CompleteA");
                vm.ShowAttemptCommand.Execute(null);
                Pair(window, output, "LastAttemptA");

                var pickedB = service.SelectFixtureRoot(b);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(pickedB.Accepted && vm.CurrentComplete is null &&
                    vm.LastAttempt is null && !vm.ConsentConfirmed &&
                    vm.State.SelectionVersion == pickedB.SelectionVersion,
                    "UI_MIB04_A_CARRIED_TO_B");
                Pair(window, output, "SelectedB");
                Demand(vm.Confirm() && vm.CanStart, "UI_MIB01_B_CONSENT_REQUIRED");
                Pair(window, output, "ConsentB");
                var runningB = vm.StartAsync();
                if (vm.IsBusy) Pair(window, output, "RunningB");
                await runningB;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(vm.CurrentComplete is { ScanStatus: InventoryScanStatus.Complete } &&
                    vm.CurrentComplete.RunRef != currentA.RunRef &&
                    vm.LastAttempt?.ObservedMediaCount == 2000,
                    "UI_MIB04_B_COMPLETE_WRONG_ROOT");
                Pair(window, output, "CompleteB");

                Demand(vm.Confirm(), "UI_MIB02_RECONSENT_REQUIRED");
                var secondB = vm.StartAsync();
                if (vm.Cancel())
                    Pair(window, output, "CancellingB");
                await secondB;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(vm.CurrentComplete?.RunRef is { } currentB &&
                    currentB != currentA.RunRef, "UI_MIB02_CURRENT_B_LOST");
                if (vm.DiagnosticLabel.Contains("CANCELLED", StringComparison.Ordinal))
                    Pair(window, output, "CancelledB");
                else Console.WriteLine("UI_MIB_CANCEL_CAPTURE_NOT_EXECUTED no_proven_cancelled_result");
                var archive = vm.CurrentComplete;
                window.Close();

                // Visual-only archived DTO: validates Offline/Stale binding, not a real regrant.
                var archiveWindow = new MiBFixtureWindow(new ArchiveVisualService(archive!), a);
                archiveWindow.Show(main);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(!archiveWindow.ModelForCi.CanStart &&
                    archiveWindow.ModelForCi.AvailabilityLabel.Contains("Offline", StringComparison.Ordinal),
                    "UI_MIB05_ARCHIVE_AUTO_GRANTED");
                Pair(archiveWindow, output, "OfflineStaleVisualFixture");
                archiveWindow.Close();
                Console.WriteLine("UI_MIB_WINDOWS_GUI_PASS real_service=true archive_visual_only=true");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 49;
                Console.Error.WriteLine("UI_MIB_WINDOWS_GUI_FAIL " + ex.GetType().Name + " " +
                    (ex is InvalidDataException ? ex.Message : "UNEXPECTED"));
            }
            finally
            {
                if (window.IsVisible) window.Close();
                main.Close();
                foreach (var path in new[] { a, b })
                {
                    try { Directory.Delete(Directory.GetParent(path)!.FullName, true); }
                    catch (IOException) { Environment.ExitCode = 50; }
                }
            }
        };
    }
}
#endif
