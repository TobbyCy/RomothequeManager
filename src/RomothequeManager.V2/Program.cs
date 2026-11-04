using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using RomothequeManager.V2.Inventory.Application;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2;

internal sealed class App : Application { }

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // Activated only by the synthetic, confined ZIP proof. No normal-app logging.
        BetaIdealAvaloniaTrace.ActivateForAuthorizedFixture(args);
        BetaIdealAvaloniaTrace.Mark("MAIN_ENTER");
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
        BetaIdealAvaloniaTrace.Mark("SETUP_BEGIN");
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .AfterSetup(builder => builder.Instance?.Styles.Add(new FluentTheme()))
            .SetupWithLifetime(lifetime);
        BetaIdealAvaloniaTrace.Mark("SETUP_COMPLETE");
        BetaIdealAvaloniaTrace.Mark("MAINWINDOW_CTOR_BEGIN");
        // Backend composition is available on the DEV 1 dependent branch.
        // No grants, ROMS enumeration or SQLite source validation at startup.
        var window = new MainWindow(() =>
        {
            var backend = new IdealLibraryBackendService();
            return ObservedLibraryWindow.FromStandardServices(backend,
                requestRomsGrant: version =>
                    backend.AuthorizeRomsRead(version, explicitUserAction: true),
                loadSavedLibraries: backend.LoadSavedLibraries,
                restoreSaved: backend.RestoreSaved);
        });
        BetaIdealAvaloniaTrace.Mark("MAINWINDOW_CTOR_END");
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
            {
                BetaIdealAvaloniaTrace.Mark("WINDOW_OPENED");
                BetaIdealAvaloniaTrace.Mark("DISPATCHER_POST");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    BetaIdealAvaloniaTrace.Mark("DISPATCHER_EXECUTE");
                    try
                    {
                        if (!window.IsVisible ||
                            !window.SmokeExerciseOfflineImportedFixture(fixture))
                        {
                            Environment.ExitCode = 29;
                            BetaIdealAvaloniaTrace.Mark("SMOKE_FAIL");
                            Console.Error.WriteLine("UX_RECIPE_OFFLINE_ESDE_GUI_FAIL");
                        }
                        else
                        {
                            BetaIdealAvaloniaTrace.Mark("SMOKE_PASS");
                            Console.WriteLine("UX_RECIPE_OFFLINE_ESDE_GUI_PASS synthetic=1 " +
                                "local_media=1 french_diagnostics=1 malformed_xml_preserves_catalogue=1");
                        }
                    }
                    catch (Exception error)
                    {
                        Environment.ExitCode = 29;
                        BetaIdealAvaloniaTrace.Mark("SMOKE_FAIL");
                        // Original smoke failure semantics; the worker never displays raw output.
                        Console.Error.WriteLine("UX_RECIPE_OFFLINE_ESDE_GUI_FAIL " +
                            error.GetType().Name + ": " + error.Message);
                    }
                    finally { window.Close(); }
                });
            };
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
#if NEXT05_CI
        // Separate child process: SQLite-only restoration, no Avalonia, ROMS or MEDIAS access.
        if (args.Length == 5 && args[0] == "--ci-next05-sqlite-reopen")
        {
            try
            {
                Environment.ExitCode = Next05GuiHarness.VerifyReopened(
                    args[1], args[2], args[3], args[4]);
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 46;
                Console.Error.WriteLine("SQLITE_UI_CHILD_FAILED " + ex.GetType().Name);
            }
            return;
        }
#endif
#if NEXT05_CI
        if (args.Contains("--ci-next05-mib-gui", StringComparer.Ordinal))
            Next05MiBGuiHarness.Configure(window);
        // #141: temporary native evidence entrypoint; absent from normal EXE.
        if (args.Contains("--ci-next05-legacy-media", StringComparer.Ordinal))
            Next05LegacyMediaHarness.Configure(window);
        // #145 Lot B-only targeted GUI proof, unavailable from standard builds.
        if (args.Contains("--ci-next05-lotb-media", StringComparer.Ordinal))
            Next05LegacyMediaHarness.ConfigureLotB(window);
        // Native RS-A fixture UI recipe. The production build has no such entrypoint.
        if (args.Contains("--ci-next05-rsa-gui", StringComparer.Ordinal))
            Next05RsAGuiHarness.Configure(window);
#endif
#if NEXT05_CI
        if (args.Contains("--ci-next05-observed-gui", StringComparer.Ordinal))
        {
            var index = Array.IndexOf(args, "--ci-next05-observed-gui");
            if (index + 1 >= args.Length)
                throw new InvalidOperationException("NEXT05_CI_ROOT_REQUIRED");
            Next05GuiHarness.Configure(window, args[index + 1]);
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
                BetaIdealAvaloniaTrace.Mark("WINDOW_OPENED");
                BetaIdealAvaloniaTrace.Mark("DISPATCHER_POST");
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    BetaIdealAvaloniaTrace.Mark("DISPATCHER_EXECUTE");
                    // --smoke-window explicitly opts into the legacy synthetic journey.
                    // Normal startup intentionally remains a NoCatalog welcome screen.
                    if (!window.IsVisible || window.Content is null ||
                        !window.SmokeExerciseFourViews() ||
                        window.Title != "ROMothèque Manager — Démonstration")
                    {
                        Environment.ExitCode = 17;
                        BetaIdealAvaloniaTrace.Mark("SMOKE_FAIL");
                        Console.Error.WriteLine("POC_WINDOW_SMOKE_FAIL");
                    }
                    else
                    {
                        BetaIdealAvaloniaTrace.Mark("SMOKE_PASS");
                        Console.WriteLine("POC_WINDOW_OPENED_PASS views=Dashboard,Collection,GameDetail,Libraries fixtures=6 synthetic=1");
                    }
                    window.Close();
                });
            };
        }
        BetaIdealAvaloniaTrace.Mark("LIFETIME_START_BEGIN");
        lifetime.Start(args);
        BetaIdealAvaloniaTrace.Mark("LIFETIME_RETURN");
    }
}


/// <summary>CI-only bounded startup trace, disabled for all ordinary application launches.</summary>
// #154: a strictly CI-fixture-only breadcrumb probe; no product logging or user paths.
internal static class BetaIdealAvaloniaTrace
{
    private static bool _enabled;

    /// <summary>Enable constant-code breadcrumbs only inside a canonical disposable Windows CI fixture.</summary>
    internal static void ActivateForAuthorizedFixture(string[] args)
    {
        _enabled = false;
        if (!args.Contains("--smoke-window", StringComparer.Ordinal) &&
            !args.Contains("--ci-ux-offline-fixture", StringComparer.Ordinal))
            return;
        if (Environment.GetEnvironmentVariable("ROMOTHEQUE_BETA_TRACE") != "1")
            return;
        // Never activate for an ordinary application, even if a caller supplied a flag.
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true")
            throw new InvalidOperationException("BETA_TRACE_SCOPE_DENIED");

        try
        {
            var rawRoot = Environment.GetEnvironmentVariable("RUNNER_TEMP");
            var rawBound = Environment.GetEnvironmentVariable("ROMOTHEQUE_BETA_TRACE_ROOT");
            if (string.IsNullOrWhiteSpace(rawRoot) ||
                string.IsNullOrWhiteSpace(rawBound))
                throw new InvalidOperationException();
            var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawRoot));
            var bound = Path.TrimEndingDirectorySeparator(Path.GetFullPath(rawBound));
            var common = Path.TrimEndingDirectorySeparator(Path.GetFullPath(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments)));
            var name = Path.GetFileName(root);
            var signature = "rm-beta-ci-";
            if (!string.Equals(root, bound, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(Path.GetDirectoryName(root), common,
                    StringComparison.OrdinalIgnoreCase) ||
                name.Length != signature.Length + 10 ||
                !name.StartsWith(signature, StringComparison.Ordinal) ||
                !name.AsSpan(signature.Length).ToString().All(c =>
                    c is >= '0' and <= '9' or >= 'a' and <= 'f') ||
                !Directory.Exists(root))
                throw new InvalidOperationException();
            var unpacked = Path.Combine(root, "unpacked");
            if (!string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory)),
                    Path.GetFullPath(unpacked), StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(unpacked) ||
                (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 ||
                (File.GetAttributes(unpacked) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException();
            // A marker created alongside disposable .fixture and metadata paths by the
            // Windows CI harness; no ROM, image, user profile or XML contents are read.
            var marker = Path.Combine(root, "BETA-TRACE-FIXTURE.marker");
            var rom = Path.Combine(root, "fictional-ideal", "ROMS", "gba", "A.fixture");
            var xml = Path.Combine(root, "fictional-ideal", "MEDIAS", "gba",
                "metadata", "es-de-gamelist.xml");
            if (!File.Exists(marker) || new FileInfo(marker).Length > 64 ||
                File.ReadAllText(marker) != "BETA_SYNTHETIC_FIXTURE_ONLY" ||
                !File.Exists(rom) || !File.Exists(xml) ||
                (File.GetAttributes(marker) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException();
            _enabled = true;
        }
        catch
        {
            // Fail closed. Never reveal a filesystem path, account or token.
            throw new InvalidOperationException("BETA_TRACE_SCOPE_DENIED");
        }
    }

    /// <summary>Flush one whitelisted stage code without emitting user, filesystem or application data.</summary>
    internal static void Mark(string stage)
    {
        if (!_enabled) return;
        // Restrict the sole output channel to fixed codes, never arbitrary text.
        if (stage is not ("MAIN_ENTER" or "SETUP_BEGIN" or "SETUP_COMPLETE" or
            "MAINWINDOW_CTOR_BEGIN" or "MAINWINDOW_CTOR_END" or
            "LIFETIME_START_BEGIN" or "WINDOW_OPENED" or "DISPATCHER_POST" or
            "DISPATCHER_EXECUTE" or "SMOKE_PASS" or "SMOKE_FAIL" or
            "LIFETIME_RETURN"))
            throw new InvalidOperationException("BETA_TRACE_CODE_DENIED");
        Console.Out.WriteLine("BETA_AVALONIA_TRACE " + stage);
        Console.Out.Flush();
    }
}
