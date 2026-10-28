using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RomothequeManager.V2;

// Captures the actual attached Avalonia UI on Windows CI, NOT native OS chrome or taskbar.
// No user filesystem or real ROM collection is touched.
internal static class VisualCapture
{
    public static void Configure(MainWindow window)
    {
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("RUNNER_TEMP")))
            throw new InvalidOperationException("VISUAL_CAPTURE_WINDOWS_CI_ONLY");
        var folder = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, "visual-poc-01");
        Directory.CreateDirectory(folder);
        var cases = new (int Width, int Height, int Dpi)[]
        {
            (1120, 720, 100), (760, 480, 100),
            (1120, 720, 125), (1120, 720, 150),
            (760, 480, 125), (760, 480, 150)
        };
        void Next(int index)
        {
            if (index >= cases.Length) { window.Close(); return; }
            var item = cases[index];
            window.Width = item.Width;
            window.Height = item.Height;
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (window.Content is not Control visual || !window.IsVisible)
                        throw new InvalidDataException("VISUAL_CAPTURE_WINDOW_NOT_OPEN");
                    visual.Measure(new Size(item.Width - 2, item.Height - 2));
                    visual.Arrange(new Rect(0, 0, item.Width - 2, item.Height - 2));
                    double scale = item.Dpi / 100.0;
                    using var frame = new RenderTargetBitmap(
                        new PixelSize((int)(item.Width * scale), (int)(item.Height * scale)),
                        new Vector(96 * scale, 96 * scale));
                    frame.Render(visual);
                    string path = Path.Combine(folder, $"Dashboard-{item.Width}x{item.Height}-DPI{item.Dpi}.png");
                    frame.Save(path, PngBitmapEncoderOptions.Default);
                    if (new FileInfo(path).Length < 4000)
                        throw new InvalidDataException("VISUAL_CAPTURE_EMPTY_RENDER");
                    Console.WriteLine("VISUAL_CAPTURE_PASS " + Path.GetFileName(path));
                    Next(index + 1);
                }
                catch (Exception error)
                {
                    Console.Error.WriteLine("VISUAL_CAPTURE_FAIL " + error.GetType().Name + " " + error.Message);
                    Environment.ExitCode = 19;
                    window.Close();
                }
            }, DispatcherPriority.Background);
        }
        window.Opened += (_, _) => Dispatcher.UIThread.Post(() => Next(0), DispatcherPriority.Background);
    }
}
