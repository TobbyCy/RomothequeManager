using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;

namespace RomothequeManager.V2;

internal sealed class App : Application { }

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args, ShutdownMode = ShutdownMode.OnMainWindowClose
        };
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .AfterSetup(builder => builder.Instance?.Styles.Add(new FluentTheme()))
            .SetupWithLifetime(lifetime);
        var window = new MainWindow();
        lifetime.MainWindow = window;
        if (args.Contains("--smoke-window", StringComparer.Ordinal))
        {
            // Real native Avalonia window must open. No artificial test-only success path.
            window.Opened += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    if (!window.IsVisible || window.Content is null ||
                        window.Title != "ROMothèque Manager — POC synthétique")
                    {
                        Environment.ExitCode = 17;
                        Console.Error.WriteLine("POC_WINDOW_SMOKE_FAIL");
                    }
                    else Console.WriteLine("POC_WINDOW_OPENED_PASS view=Dashboard fixtures=6 synthetic=1");
                    window.Close();
                });
            };
        }
        lifetime.Start(args);
    }
}
