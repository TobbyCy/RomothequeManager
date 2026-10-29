#if NEXT04_CI
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RomothequeManager.V2;

// Test-only attached Avalonia renderer. Not compiled into a normal Windows ZIP.
// It consumes only verified synthetic fixture paths supplied by a CI workflow.
internal static class Next04Capture
{
    public static void Configure(MainWindow window, string fixtureRoot)
    {
        var runner = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            string.IsNullOrWhiteSpace(runner) ||
            !Path.GetFullPath(fixtureRoot).StartsWith(
                Path.GetFullPath(runner).TrimEnd(Path.DirectorySeparatorChar) +
                Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("NEXT04_CI_FIXTURE_SCOPE_REQUIRED");
        foreach (var name in new[] { "IDEAL", "CHAOS", "MYSTERY" })
            if (!Directory.Exists(Path.Combine(fixtureRoot, name, "ROMS")))
                throw new InvalidDataException("NEXT04_CI_SYNTHETIC_REQUIRED:" + name);

        var output = Path.Combine(runner, "next04-ux-captures");
        Directory.CreateDirectory(output);
        void Capture(string name, int width, int height)
        {
            window.Width = width;
            window.Height = height;
            if (window.Content is not Control visual || !window.IsVisible)
                throw new InvalidDataException("NEXT04_GUI_NOT_ATTACHED");
            visual.Measure(new Size(width - 2, height - 2));
            visual.Arrange(new Rect(0, 0, width - 2, height - 2));
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            bitmap.Render(visual);
            var target = Path.Combine(output, name + ".png");
            bitmap.Save(target, PngBitmapEncoderOptions.Default);
            if (new FileInfo(target).Length < 4000)
                throw new InvalidDataException("NEXT04_GUI_CAPTURE_EMPTY:" + name);
            Console.WriteLine("NEXT04_GUI_CAPTURE_PASS name=" + name);
        }

        window.Opened += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            try
            {
                if (!window.CiNext04Import(Path.Combine(fixtureRoot, "IDEAL"), 30, 13))
                    throw new InvalidDataException("NEXT04_IDEAL_IMPORT_FAILED");
                Capture("IDEAL-Dashboard-1120x720", 1120, 720);
                Capture("IDEAL-Dashboard-Ultrawide-2200x900", 2200, 900);
                if (!window.CiNext04FirstSystem())
                    throw new InvalidDataException("NEXT04_SYSTEM_NAVIGATION_FAILED");
                Capture("IDEAL-System-Collection-1120x720", 1120, 720);
                if (!window.CiNext04OpenFirst())
                    throw new InvalidDataException("NEXT04_GAME_NAVIGATION_FAILED");
                Capture("IDEAL-GameDetail-1120x720", 1120, 720);
                if (!window.CiNext04ReturnCollection())
                    throw new InvalidDataException("NEXT04_RETURN_CONTEXT_FAILED");
                Capture("IDEAL-Collection-Return-760x480", 760, 480);
                if (!window.CiNext04Import(Path.Combine(fixtureRoot, "CHAOS"), 30, 13))
                    throw new InvalidDataException("NEXT04_CHAOS_IMPORT_FAILED");
                Capture("CHAOS-Dashboard-1120x720", 1120, 720);
                if (!window.CiNext04RejectMystery(Path.Combine(fixtureRoot, "MYSTERY")))
                    throw new InvalidDataException("NEXT04_MYSTERY_INVALID_IMPORT_NOT_PRESERVED");
                Capture("MYSTERY-Rejected-LastGood-1120x720", 1120, 720);
                Console.WriteLine("NEXT04_REAL_ESDE_GUI_PASS captures=7 ideal=30 systems=13 chaos=30 mystery=REJECTED lastGood=KEPT");
            }
            catch (Exception error)
            {
                Environment.ExitCode = 42;
                Console.Error.WriteLine("NEXT04_GUI_FAIL " + error.GetType().Name + ": " + error.Message);
            }
            finally { window.Close(); }
        }, DispatcherPriority.Background);
    }
}
#endif
