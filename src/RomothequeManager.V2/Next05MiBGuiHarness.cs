#if NEXT05_CI
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    private static readonly string[] CommandNames =
        ["MiBSelect", "MiBConsent", "MiBStart", "MiBCancel", "MiBCurrent", "MiBAttempt"];

    // Only an actual rendered control in the capture viewport counts as visual proof.
    // Rectangle sidecars are verified against PNG pixels by the Windows-only workflow.
    private static void Capture(MiBFixtureWindow window, string output, string state, int width, int height)
    {
        window.Width = width; window.Height = height;
        var scroll = window.FindControl<ScrollViewer>("RootScroll")!;
        scroll.Offset = Vector.Zero; // Keep both command groups visible; never auto-scroll past them.
        window.UpdateLayout();
        var surface = window.Content as Control ??
            throw new InvalidDataException("UI_MIB_CAPTURE_VISUAL_MISSING");
        surface.Measure(new Size(width - 2, height - 2));
        surface.Arrange(new Rect(0, 0, width - 2, height - 2));
        scroll.Offset = Vector.Zero;
        window.UpdateLayout();

        string? expectedFocus = state switch
        {
            "ConsentA" or "ConsentB" => "MiBStart",
            "CurrentA" or "CurrentB" => "MiBCurrent",
            _ => null
        };
        if (expectedFocus is not null)
        {
            var focused = window.FindControl<Button>(expectedFocus)!;
            Demand(focused.IsEnabled && focused.Focus(NavigationMethod.Tab) && focused.IsFocused,
                "UI_MIB_KEYBOARD_STYLE_FOCUS_UNAVAILABLE_" + expectedFocus);
            window.UpdateLayout();
        }

        var target = window.FindControl<TextBlock>("MiBPhase")!;
        var at = target.TranslatePoint(new Point(0, 0), surface);
        Demand(at.HasValue && at.Value.Y >= 0 && at.Value.Y + target.Bounds.Height < height - 2 &&
            !string.IsNullOrWhiteSpace(target.Text), "UI_MIB_CAPTURE_LABEL_NOT_VISIBLE");
        var rectangles = new List<string> { "Name,X,Y,Width,Height,Enabled,Focused" };
        foreach (var name in CommandNames)
        {
            var button = window.FindControl<Button>(name) ??
                throw new InvalidDataException("UI_MIB_CONTROL_MISSING_" + name);
            var point = button.TranslatePoint(new Point(0, 0), surface) ??
                throw new InvalidDataException("UI_MIB_BUTTON_POSITION_MISSING_" + name);
            Demand(button.IsVisible && button.Bounds.Width >= 50 &&
                button.Bounds.Height >= 35 && point.X >= 0 && point.Y >= 0 &&
                point.X + button.Bounds.Width < width &&
                point.Y + button.Bounds.Height < height - 2,
                "UI_MIB_BUTTON_OUTSIDE_CAPTURE_" + name + "_" + state);
            rectangles.Add(string.Join(",", name,
                (int)Math.Round(point.X), (int)Math.Round(point.Y),
                (int)Math.Floor(button.Bounds.Width), (int)Math.Floor(button.Bounds.Height),
                button.IsEnabled && button.Command?.CanExecute(button.CommandParameter) == true
                    ? "true" : "false", button.IsFocused ? "true" : "false"));
        }
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(surface);
        var file = Path.Combine(output, "MIB-" + state + "-" + width + "x" + height + ".png");
        bitmap.Save(file, PngBitmapEncoderOptions.Default);
        File.WriteAllLines(file + ".rects.csv", rectangles);
        Demand(new FileInfo(file).Length > 4000, "UI_MIB_CAPTURE_EMPTY");
        Console.WriteLine("UI_MIB_BUTTON_GEOMETRY_PASS state=" + state +
            " size=" + width + "x" + height + " buttons=6 focus=" + (expectedFocus ?? "none"));
        Console.WriteLine("UI_MIB_CAPTURE_PASS " + Path.GetFileName(file));
    }
    private static void Pair(MiBFixtureWindow window, string output, string state)
    {
        Capture(window, output, state, 1120, 720);
        Capture(window, output, state, 760, 480);
    }


    // Windows native keyboard proof: actual Win32 input events, NOT a Focus(Tab) simulation.
    // SendInput injects synthetic OS keystrokes into the foreground window on CI Windows,
    // which Avalonia must receive as real KeyDown routed events and process for navigation.
    // The first enabled button is primed by API only as a baseline; it is never counted
    // as a successful keyboard transition. No physical USB keyboard is claimed.
    [StructLayout(LayoutKind.Sequential)]
    private struct WinInput
    {
        public uint Type;
        public WinInputUnion Union;
    }
    [StructLayout(LayoutKind.Explicit)]
    private struct WinInputUnion
    {
        [FieldOffset(0)] public WinKeyboardInput Keyboard;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct WinKeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public UIntPtr ExtraInfo;
    }
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint number, [In] WinInput[] events, int structureSize);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr window);

    private static WinInput NativeKey(ushort virtualKey, bool keyUp = false) =>
        new() { Type = 1, Union = new WinInputUnion {
            Keyboard = new WinKeyboardInput { VirtualKey = virtualKey, Flags = keyUp ? 2u : 0u } } };

    private static async Task NativeTabAsync(bool reverse)
    {
        // SendInput goes through the OS input queue; it does not call Focus or RaiseEvent.
        Demand(Marshal.SizeOf<WinInput>() == 40, "UI_MIB_WIN64_SENDINPUT_LAYOUT");
        var events = reverse
            ? new[] { NativeKey(0x10), NativeKey(0x09), NativeKey(0x09, true), NativeKey(0x10, true) }
            : new[] { NativeKey(0x09), NativeKey(0x09, true) };
        var injected = await Task.Run(() => SendInput((uint)events.Length, events,
            Marshal.SizeOf<WinInput>()));
        Demand(injected == events.Length, "UI_MIB_NATIVE_KEY_INJECTION_FAILED");
        await Task.Delay(85);
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
    }

    private static bool ActiveButton(Button button) =>
        button.IsVisible && button.IsEnabled &&
        button.Command?.CanExecute(button.CommandParameter) == true;

    private static async Task ProveNativeKeyboardAsync(MiBFixtureWindow window,
        MiBUiViewModel vm, string output, string scenario, string[] activeOrder,
        int width, int height)
    {
        Demand(OperatingSystem.IsWindows() && activeOrder.Length >= 2,
            "UI_MIB_KEYBOARD_TEST_HOST_OR_ORDER_INVALID");
        window.Width = width; window.Height = height;
        window.UpdateLayout();
        window.Activate();
        await Task.Delay(100);
        var handle = window.TryGetPlatformHandle();
        Demand(handle is { HandleDescriptor: "HWND" }, "UI_MIB_KEYBOARD_HWND_MISSING");
        var hwnd = handle!.Handle;
        if (GetForegroundWindow() != hwnd) SetForegroundWindow(hwnd);
        await Task.Delay(80);
        Demand(GetForegroundWindow() == hwnd, "UI_MIB_KEYBOARD_FOREGROUND_NOT_CONFIRMED");

        var buttons = CommandNames.ToDictionary(name => name,
            name => window.FindControl<Button>(name)!,
            StringComparer.Ordinal);
        var actualActive = CommandNames.Where(name => ActiveButton(buttons[name])).ToArray();
        Demand(actualActive.SequenceEqual(activeOrder, StringComparer.Ordinal),
            "UI_MIB_KEYBOARD_ACTIVE_ORDER_UNEXPECTED_" + scenario + "_" +
            string.Join("_", actualActive));
        // Start cannot be activated or focused before the one-shot consent.
        if (scenario == "SelectedA")
        {
            Demand(!vm.CanStart && !vm.ConsentConfirmed &&
                !buttons["MiBStart"].IsEnabled &&
                !buttons["MiBStart"].Command!.CanExecute(null) &&
                vm.LastAttempt is null && vm.CurrentComplete is null,
                "UI_MIB_KEYBOARD_NO_CONSENT_START_ENABLED");
        }
        var received = new List<bool>();
        EventHandler<KeyEventArgs> onKeyDown = (_, key) =>
        {
            if (key.Key == Key.Tab)
                received.Add(key.KeyModifiers.HasFlag(KeyModifiers.Shift));
        };
        window.AddHandler(InputElement.KeyDownEvent, onKeyDown,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        try
        {
            Demand(buttons[activeOrder[0]].Focus(NavigationMethod.Pointer) &&
                buttons[activeOrder[0]].IsFocused,
                "UI_MIB_KEYBOARD_PRIME_FAILED_" + scenario);
            // Only subsequent focus changes caused by native Tab/Shift+Tab are PASS.
            async Task StepAsync(int index, int next, bool reverse)
            {
                var priorKeyCount = received.Count;
                var from = activeOrder[index];
                var to = activeOrder[next];
                Demand(buttons[from].IsFocused,
                    "UI_MIB_KEYBOARD_FROM_NOT_FOCUSED_" + scenario);
                await NativeTabAsync(reverse);
                Demand(received.Skip(priorKeyCount).Any(shift => shift == reverse),
                    "UI_MIB_KEYBOARD_AVALONIA_KEYDOWN_NOT_RECEIVED_" + scenario);
                Demand(buttons[to].IsFocused && ActiveButton(buttons[to]) &&
                    CommandNames.Count(name => buttons[name].IsFocused) == 1 &&
                    CommandNames.Where(name => !ActiveButton(buttons[name]))
                        .All(name => !buttons[name].IsFocused),
                    "UI_MIB_KEYBOARD_TAB_ORDER_OR_DISABLED_SKIP_FAILED_" +
                    scenario + "_" + from + "_to_" + to);
                Demand(scenario != "SelectedA" ||
                    (!vm.CanStart && !vm.ConsentConfirmed && vm.LastAttempt is null),
                    "UI_MIB_KEYBOARD_START_WITHOUT_CONSENT");
                var stepName = "Keyboard-" + scenario + (reverse ? "-Reverse-" : "-Forward-") +
                    next.ToString("D2") + "-" + to;
                Capture(window, output, stepName, width, height);
                var record = string.Join(",", scenario, width, height,
                    reverse ? "ShiftTab" : "Tab", from, to, "WIN32_SENDINPUT",
                    "Avalonia_KeyDown", buttons[to].IsFocused ? "true" : "false");
                File.AppendAllText(Path.Combine(output, "MIB-keyboard-trace.csv"),
                    record + Environment.NewLine);
                Console.WriteLine("UI_MIB_NATIVE_KEYBOARD_STEP_PASS " + record);
            }
            for (var i = 0; i < activeOrder.Length - 1; i++)
                await StepAsync(i, i + 1, reverse: false);
            for (var i = activeOrder.Length - 1; i > 0; i--)
                await StepAsync(i, i - 1, reverse: true);
            Console.WriteLine("UI_MIB_NATIVE_KEYBOARD_PASS state=" + scenario +
                " size=" + width + "x" + height +
                " enabled=" + string.Join(">", activeOrder) +
                " forward_and_reverse=true disabled_skipped=true no_implicit_start=true");
        }
        finally
        {
            window.RemoveHandler(InputElement.KeyDownEvent, onKeyDown);
        }
    }

    private sealed class ArchiveVisualService : IMiBFixtureMediaApplicationService
    {
        public ArchiveVisualService(MiBRunView run) =>
            State = new(0, MiBPhase.NoSelection, false,
                run with { Availability = InventoryAvailability.Offline,
                    SourceFreshness = InventoryFreshness.Stale, IsHistoricalFixture = true },
                null, true, "MI_B_HISTORY_OFFLINE");
        public ArchiveVisualService(MiBProjectionState state) => State = state;
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
        File.WriteAllText(Path.Combine(output, "MIB-keyboard-trace.csv"),
            "Scenario,Width,Height,Direction,From,To,Injection,AvaloniaEvent,Focused" + Environment.NewLine);
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
                foreach (var (width, height) in new[] { (1120, 720), (760, 480) })
                    await ProveNativeKeyboardAsync(window, vm, output, "SelectedA",
                        ["MiBSelect", "MiBConsent"], width, height);
                Demand(vm.Confirm() && vm.CanStart, "UI_MIB01_CONSENT_REQUIRED");
                Pair(window, output, "ConsentA");
                foreach (var (width, height) in new[] { (1120, 720), (760, 480) })
                    await ProveNativeKeyboardAsync(window, vm, output, "ConsentA",
                        ["MiBSelect", "MiBStart"], width, height);
                var runningA = vm.StartAsync();
                Demand(!vm.ConsentConfirmed, "UI_MIB01_CONSENT_NOT_CONSUMED");
                if (vm.IsBusy) Pair(window, output, "RunningA");
                await runningA;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(vm.CurrentComplete is { ScanStatus: InventoryScanStatus.Complete } &&
                    vm.LastAttempt is { ObservedMediaCount: 2000 }, "UI_MIB03_A_NOT_COMPLETE");
                var currentA = vm.CurrentComplete!;
                Pair(window, output, "CompleteA");
                foreach (var (width, height) in new[] { (1120, 720), (760, 480) })
                    await ProveNativeKeyboardAsync(window, vm, output, "CompleteA",
                        ["MiBSelect", "MiBConsent", "MiBCurrent", "MiBAttempt"], width, height);
                vm.ShowAttemptCommand.Execute(null);
                Pair(window, output, "LastAttemptA");
                vm.ShowCurrentCommand.Execute(null);
                Pair(window, output, "CurrentA");

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
                vm.ShowAttemptCommand.Execute(null);
                Pair(window, output, "LastAttemptB");
                vm.ShowCurrentCommand.Execute(null);
                Pair(window, output, "CurrentB");

                Demand(vm.Confirm(), "UI_MIB02_RECONSENT_REQUIRED");
                var secondB = vm.StartAsync();
                Demand(vm.Cancel(), "UI_MIB03_CANCEL_NOT_ACCEPTED_IN_GUI");
                Pair(window, output, "CancellingB");
                await secondB;
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                var currentB = vm.CurrentComplete?.RunRef;
                Demand(currentB is not null &&
                    currentB != currentA.RunRef, "UI_MIB02_CURRENT_B_LOST");
                Demand(vm.DiagnosticLabel == "MI_A_CANCELLED" &&
                    vm.PhaseLabel.Contains("Annulée", StringComparison.Ordinal) &&
                    !vm.PhaseLabel.Contains("OFFLINE / STALE", StringComparison.Ordinal) &&
                    vm.LastAttempt?.SafeDiagnosticCodes.Contains("MI_A_CANCELLED") == true &&
                    vm.CurrentComplete?.RunRef == currentB,
                    "UI_MIB03_CANCELLED_SHOWN_AS_OFFLINE_OR_CURRENT_LOST");
                Pair(window, output, "CancelledB");
                var archive = vm.CurrentComplete;
                window.Close();

                // Visual-only archived DTO: validates Offline/Stale binding, not a real regrant.
                var archiveWindow = new MiBFixtureWindow(new ArchiveVisualService(archive!), a);
                archiveWindow.Show(main);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                Demand(!archiveWindow.ModelForCi.CanStart &&
                    archiveWindow.ModelForCi.AvailabilityLabel.Contains("Offline", StringComparison.Ordinal) &&
                    archiveWindow.ModelForCi.PhaseLabel.Contains("Historique OFFLINE / STALE", StringComparison.Ordinal),
                    "UI_MIB05_ARCHIVE_BANNER_OR_GRANT");
                Pair(archiveWindow, output, "OfflineStaleVisualFixture");
                archiveWindow.Close();

                // Synthetic running UI for deterministic Cancel tabbing (NOT a media scan).
                // Retains a complete fixture record so Cancel, Current, Attempt are enabled;
                // all three preceding buttons must be skipped by actual OS key events.
                var keyboardState = new MiBProjectionState(
                    vm.State.SelectionVersion, MiBPhase.Running, true,
                    archive, archive, false, null);
                var keyboardWindow = new MiBFixtureWindow(
                    new ArchiveVisualService(keyboardState), a);
                keyboardWindow.Show(main);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                foreach (var (width, height) in new[] { (1120, 720), (760, 480) })
                    await ProveNativeKeyboardAsync(keyboardWindow,
                        keyboardWindow.ModelForCi, output, "RunningCancelKeyboardFixture",
                        ["MiBCancel", "MiBCurrent", "MiBAttempt"], width, height);
                keyboardWindow.Close();

                Console.WriteLine("UI_MIB_WINDOWS_GUI_PASS real_service=true archive_visual_only=true keyboard_injection=SendInput");
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
