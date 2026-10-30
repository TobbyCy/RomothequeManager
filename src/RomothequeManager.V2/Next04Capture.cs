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

        var longRoot = Path.Combine(Path.GetDirectoryName(fixtureRoot)!, "LONG");
        if (!Directory.Exists(Path.Combine(longRoot, "ROMS", "gba")) ||
            !File.Exists(Path.Combine(longRoot, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml")))
            throw new InvalidDataException("NEXT04_LONG_SYNTHETIC_FIXTURE_REQUIRED");

        var output = Path.Combine(runner, "next04-ux-captures");
        Directory.CreateDirectory(output);
        // DPI render simulation uses Avalonia logical DIP layout and a scaled bitmap.
        // It is not a substitute for physical monitor/Windows DPI human testing.
        void Capture(string name, int width, int height, double scale = 1.0)
        {
            window.Width = width;
            window.Height = height;
            if (window.Content is not Control visual || !window.IsVisible)
                throw new InvalidDataException("NEXT04_GUI_NOT_ATTACHED");
            visual.Measure(new Size(width - 2, height - 2));
            visual.Arrange(new Rect(0, 0, width - 2, height - 2));
            using var bitmap = new RenderTargetBitmap(
                new PixelSize((int)Math.Round(width * scale), (int)Math.Round(height * scale)),
                new Vector(96 * scale, 96 * scale));
            bitmap.Render(visual);
            var target = Path.Combine(output, name + ".png");
            bitmap.Save(target, PngBitmapEncoderOptions.Default);
            if (new FileInfo(target).Length < 4000)
                throw new InvalidDataException("NEXT04_GUI_CAPTURE_EMPTY:" + name);
            Console.WriteLine("NEXT04_GUI_CAPTURE_PASS name=" + name);
        }

        window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                if (!window.CiNext04BrandingLayout())
                    throw new InvalidDataException("NEXT04_BRANDING_NATIVE_SIZE_OR_ICON_FAILED");
                Console.WriteLine("NEXT04_BRANDING_PASS logo=1960x576 display=228x67 icon=present");
                if (!window.CiNext04Import(Path.Combine(fixtureRoot, "IDEAL"), 30, 13))
                    throw new InvalidDataException("NEXT04_IDEAL_IMPORT_FAILED");
                Capture("IDEAL-Dashboard-1120x720", 1120, 720);
                Capture("IDEAL-Dashboard-Ultrawide-2200x900", 2200, 900);
                Capture("IDEAL-Dashboard-DPI125-1400x900", 1120, 720, 1.25);
                Capture("IDEAL-Dashboard-DPI200-2240x1440", 1120, 720, 2.0);
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
                if (!window.CiNext04PrepareDeep(longRoot))
                    throw new InvalidDataException("NEXT04_DEEP_COLLECTION_PREPARATION_FAILED");
                Capture("LONG-Collection-Page2-1120x720", 1120, 720);
                // Let Avalonia finish its measure/layout pass before reading ScrollViewer.Extent.
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                if (!window.CiNext04DeepOpen())
                    throw new InvalidDataException("NEXT04_DEEP_NONZERO_SCROLL_OR_METADATA_FAILED");
                Capture("LONG-Detail-Metadata-1120x720", 1120, 720);
                if (!await window.CiNext04DeepReimportWhileOnDetail())
                    throw new InvalidDataException("NEXT04_DETAIL_REIMPORT_CONTEXT_LOST");
                if (!window.CiNext04DeepReturn())
                    throw new InvalidDataException("NEXT04_DEEP_RETURN_FILTER_OR_PAGE_LOST");
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                if (!window.CiNext04DeepOffsetRestored())
                    throw new InvalidDataException("NEXT04_DEEP_VERTICAL_OFFSET_NOT_RESTORED");
                Capture("LONG-Collection-Returned-1120x720", 1120, 720);
                if (!await window.CiNext04DeepReimportWhileOnCollection())
                    throw new InvalidDataException("NEXT04_COLLECTION_REIMPORT_CONTEXT_LOST");
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                if (!window.CiNext04DeepOffsetRestored())
                    throw new InvalidDataException("NEXT04_REIMPORT_SCROLL_NOT_RESTORED");
                if (!await window.CiNext04DeepMalformedXmlPreservesLastGood(
                        longRoot))
                    throw new InvalidDataException("NEXT04_FAILED_REIMPORT_LOST_LAST_VALID_CONTEXT");
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                if (!window.CiNext04DeepOffsetRestored())
                    throw new InvalidDataException("NEXT04_FAILED_REIMPORT_SCROLL_LOST");
                if (!window.CiNext04MissingMetadataAndInventoryLink())
                    throw new InvalidDataException("NEXT04_NULL_METADATA_OR_INVENTORY_LINK_FAILED");
                Capture("LONG-Inventory-Preview-Accessible-1120x720", 1120, 720);
                if (!window.CiNext04DiagnosticsPagination())
                    throw new InvalidDataException("NEXT04_DIAGNOSTICS_LAST_PAGE_INACCESSIBLE");
                Console.WriteLine("NEXT04_DIAGNOSTICS_PAGING_PASS total=27 page_size=20 last=accessible");
                Console.WriteLine("NEXT04_GUI_RENDER_IMPORT_INTERNAL_PASS picker_confirm_cancel=NOT_EXECUTED");
                Console.WriteLine("NEXT04_DEEP_GUI_PASS page=2 search=kept system=gba scroll_nonzero=restored detail_reimport=kept collection_reimport=kept invalid_xml=last_good metadata_optional=visible inventory_simulation=accessible");
                Console.WriteLine("NEXT04_REAL_ESDE_GUI_PASS captures=13 ideal=30 systems=13 chaos=30 mystery=REJECTED lastGood=KEPT dpi_render=125_200 simulated_only=1");
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
