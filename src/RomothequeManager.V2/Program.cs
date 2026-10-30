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
#if NEXT04_IMPORT_CI
        // The native import proof is compiled only into an isolated CI executable.
        // A normal Windows portable build contains no such entrypoint.
        if (args.Length == 2 && args[0] == "--ci-next04-import-engine")
        {
            try
            {
                Environment.ExitCode = Next04ImportEngineHarness.Run(args[1]);
            }
            catch (Exception error)
            {
                Environment.ExitCode = 43;
                // Only the type/code: never print a selected root or XML text.
                Console.Error.WriteLine("NEXT04_IMPORT_ENGINE_NATIVE_FAIL " +
                    error.GetType().Name + " " +
                    (error is InvalidDataException ? error.Message : "IMPORT_ENGINE_ERROR"));
            }
            return;
        }
#endif
        var lifetime = new ClassicDesktopStyleApplicationLifetime
        {
            Args = args
        };
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .AfterSetup(builder => builder.Instance?.Styles.Add(new FluentTheme()))
            .SetupWithLifetime(lifetime);
        var window = new MainWindow();
        lifetime.MainWindow = window;
        if (args.Contains("--ci-ux-offline-fixture", StringComparer.Ordinal))
        {
            var index = Array.IndexOf(args, "--ci-ux-offline-fixture");
            var temp = Environment.GetEnvironmentVariable("RUNNER_TEMP");
            if (!OperatingSystem.IsWindows() ||
                Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
                string.IsNullOrWhiteSpace(temp) || index + 1 >= args.Length)
                throw new InvalidOperationException("UX_RECIPE_CI_ONLY");
            var fixture = Path.GetFullPath(args[index + 1]);
            var isolatedRoot = Path.GetFullPath(temp) + Path.DirectorySeparatorChar;
            if (!fixture.StartsWith(isolatedRoot, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(Path.Combine(fixture, "ROMS", "gba", "A.fixture")) ||
                !File.Exists(Path.Combine(fixture, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml")))
                throw new InvalidOperationException("UX_RECIPE_SYNTHETIC_FIXTURE_REQUIRED");
            window.Opened += (_, _) =>
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    try
                    {
                        if (!window.IsVisible ||
                            !window.SmokeExerciseOfflineImportedFixture(fixture))
                        {
                            Environment.ExitCode = 29;
                            Console.Error.WriteLine("UX_RECIPE_OFFLINE_ESDE_GUI_FAIL");
                        }
                        else
                            Console.WriteLine("UX_RECIPE_OFFLINE_ESDE_GUI_PASS synthetic=1 " +
                                "local_media=1 french_diagnostics=1 malformed_xml_preserves_catalogue=1");
                    }
                    catch (Exception error)
                    {
                        Environment.ExitCode = 29;
                        Console.Error.WriteLine("UX_RECIPE_OFFLINE_ESDE_GUI_FAIL " +
                            error.GetType().Name + ": " + error.Message);
                    }
                    finally { window.Close(); }
                });
        }
        if (args.Contains("--ci-visual-capture", StringComparer.Ordinal))
            VisualCapture.Configure(window);
        if (args.Contains("--ci-library-ux02-capture", StringComparer.Ordinal))
            LibraryUxCapture.Configure(window);
#if NEXT03_CI
        if (args.Contains("--ci-next03-real-gui", StringComparer.Ordinal))
        {
            var at = Array.IndexOf(args, "--ci-next03-real-gui");
            if (at + 1 >= args.Length) throw new InvalidOperationException("NEXT03_CI_ROOT_REQUIRED");
            Next03RealGuiHarness.Configure(window, args[at + 1]);
        }
#endif
#if NEXT04_CI
        if (args.Contains("--ci-next04-esde-capture", StringComparer.Ordinal))
        {
            var index = Array.IndexOf(args, "--ci-next04-esde-capture");
            if (index + 1 >= args.Length)
                throw new InvalidOperationException("NEXT04_CI_ROOT_REQUIRED");
            Next04Capture.Configure(window, args[index + 1]);
        }
#endif
        if (args.Contains("--smoke-window", StringComparer.Ordinal))
        {
            // Real native Avalonia window must open. No artificial test-only success path.
            window.Opened += (_, _) =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    // --smoke-window explicitly opts into the legacy synthetic journey.
                    // Normal startup intentionally remains a NoCatalog welcome screen.
                    if (!window.IsVisible || window.Content is null ||
                        !window.SmokeExerciseFourViews() ||
                        window.Title != "ROMothèque Manager — Démonstration")
                    {
                        Environment.ExitCode = 17;
                        Console.Error.WriteLine("POC_WINDOW_SMOKE_FAIL");
                    }
                    else Console.WriteLine("POC_WINDOW_OPENED_PASS views=Dashboard,Collection,GameDetail,Libraries fixtures=6 synthetic=1");
                    window.Close();
                });
            };
        }
        lifetime.Start(args);
    }
}
