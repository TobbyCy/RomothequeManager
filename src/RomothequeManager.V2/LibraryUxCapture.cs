using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RomothequeManager.V2;

// Real Avalonia renders on Windows CI, never screenshots of native OS chrome.
// Scenario selection is presentation-only and deliberately never opens a ROM.
internal static class LibraryUxCapture
{
    public static void Configure(MainWindow window)
    {
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RUNNER_TEMP")))
            throw new InvalidOperationException("LIBRARY_UX02_CAPTURE_CI_ONLY");

        var root = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "library-ux02");
        Directory.CreateDirectory(root);
        var cases = new (string Scenario, POCView View, int Width, int Height)[]
        {
            ("IDEAL", POCView.Dashboard, 1120, 720),
            ("IDEAL", POCView.Collection, 1120, 720),
            ("IDEAL", POCView.GameDetail, 1120, 720),
            ("CHAOS", POCView.Collection, 760, 480),
            ("CHAOS", POCView.GameDetail, 1120, 720),
            ("MYSTERY", POCView.Dashboard, 1120, 720),
            ("MYSTERY", POCView.Collection, 760, 480),
            ("IDEAL", POCView.Libraries, 760, 480)
        };

        void RenderNext(int index)
        {
            if (index == cases.Length) { window.Close(); return; }
            var item = cases[index];
            window.Width = item.Width;
            window.Height = item.Height;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (!window.IsVisible || !window.PrepareScenarioCapture(item.Scenario, item.View) ||
                        window.Content is not Control visual)
                        throw new InvalidDataException("LIBRARY_UX02_SCENARIO_NOT_RENDERED");
                    visual.Measure(new Size(item.Width - 2, item.Height - 2));
                    visual.Arrange(new Rect(0, 0, item.Width - 2, item.Height - 2));
                    using var screenshot = new RenderTargetBitmap(
                        new PixelSize(item.Width, item.Height), new Vector(96, 96));
                    screenshot.Render(visual);
                    var path = Path.Combine(root,
                        $"{item.Scenario}-{item.View}-{item.Width}x{item.Height}.png");
                    screenshot.Save(path, PngBitmapEncoderOptions.Default);
                    if (new FileInfo(path).Length < 4000)
                        throw new InvalidDataException("LIBRARY_UX02_EMPTY_RENDER");
                    Console.WriteLine("LIBRARY_UX02_CAPTURE_PASS " + Path.GetFileName(path));
                    RenderNext(index + 1);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("LIBRARY_UX02_CAPTURE_FAIL " +
                        error.GetType().Name + ": " + error.Message);
                    Environment.ExitCode = 31;
                    window.Close();
                }
            }, DispatcherPriority.Background);
        }
        window.Opened += (_, _) =>
            Dispatcher.UIThread.Post(() =>
            {
                if (!window.SmokeExerciseScenarioJourneys())
                {
                    Console.Error.WriteLine("LIBRARY_UX02_JOURNEYS_FAIL");
                    Environment.ExitCode = 32;
                    window.Close();
                    return;
                }
                Console.WriteLine("LIBRARY_UX02_JOURNEYS_PASS ideal=1 chaos=1 mystery_refused=1 returns=1");
                RenderNext(0);
            }, DispatcherPriority.Background);
    }
}
