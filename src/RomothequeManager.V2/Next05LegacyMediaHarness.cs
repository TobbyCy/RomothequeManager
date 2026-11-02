#if NEXT05_CI
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace RomothequeManager.V2;

// #141: native WINDOWS ONLY, attached Avalonia exercise of the exact production
// CoverFor/ImportedMediaPreview pathways on generated synthetic ES-DE media.
// Absent from the normal EXE. No ROM, BIOS, SSD, grant or preview service.
internal static class Next05LegacyMediaHarness
{
    private static void Demand(bool condition, string code)
    {
        if (!condition) throw new InvalidDataException(code);
    }

    public static void Configure(MainWindow window)
    {
        var runner = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            string.IsNullOrWhiteSpace(runner))
            throw new InvalidOperationException("LM_FIXTURES_ONLY_WINDOWS_RUNNER_REQUIRED");
        var jpg = Path.Combine(runner, "lm-valid-synthetic.jpg");
        Demand(File.Exists(jpg), "LM_JPEG_SYNTHETIC_NOT_PREPARED");
        var root = Path.Combine(runner, "LM141-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var gallery = Path.Combine(runner, "LM141-Windows-PNG");
        Directory.CreateDirectory(gallery);
        var fixture = BuildFixture(root, jpg);
        var memory = new MemorySample();
        var sampler = new System.Threading.Timer(_ => memory.Tick(), null, 0, 20);
        window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                // Truthful distinction between a member path being OPEN_OK and
                // actual decoder acceptance. Verify the fixture itself via PNG
                // chunk/zlib synthesis and native pixel decode in limited scope.
                var opened = new EsDeImportSession(root);
                var snapshot = opened.Import();
                Demand(snapshot.Games.Count == 50, "LM07_ESDE_50_NOT_IMPORTED");
                var bigMedia = snapshot.Games[0].Media.Single();
                Demand(opened.TryOpenMedia(bigMedia, out var bigStream) && bigStream is not null,
                    "LM02_GENUINE_4096_MEMBER_NOT_OPENABLE");
                using (bigStream)
                {
                    var entries = 0;
                    var result = LegacyImportedImageBudget.Decode(bigStream!,
                        LegacyImageSlot.Card, CancellationToken.None, () => entries++);
                    result.Bitmap?.Dispose();
                    Demand(result.Reason == "MEDIA_DIMENSIONS_EXCEEDED" && entries == 0,
                        "LM02_4096_MUST_REFUSE_WITHOUT_DECODER");
                }
                var bigBytes = File.ReadAllBytes(fixture.GiantPng);
                Demand(bigBytes.Length < LegacyImportedImageBudget.MaxCompressedBytes,
                    "LM02_4096_COMPRESSED_FIXTURE_UNEXPECTED");
                Demand(!LegacyImportedImageBudget.Preflight(bigBytes, out var giantReason) &&
                    giantReason == "MEDIA_DIMENSIONS_EXCEEDED",
                    "LM02_PRE_DECODE_DIMENSION_REJECT");
                var overBytes = File.ReadAllBytes(fixture.OversizedPng);
                Demand(overBytes.Length > LegacyImportedImageBudget.MaxCompressedBytes,
                    "LM01_REQUIRES_GENUINE_OVER4MIB_PNG");
                // One native reference decode of a FICTIONAL 2048x1024 PNG
                // verifies that LM01 is not merely a fake header / random file.
                using (var proof = new Bitmap(new MemoryStream(overBytes)))
                    Demand(proof.PixelSize.Width == 2048 && proof.PixelSize.Height == 1024,
                        "LM01_FIXTURE_NOT_ACTUALLY_DECODABLE");
                var oversizeMedia = snapshot.Games[1].Media.Single();
                Demand(opened.TryOpenMedia(oversizeMedia, out var tooLarge) && tooLarge is not null,
                    "LM01_OVERSIZED_MEMBER_NOT_OPENABLE");
                using (tooLarge)
                {
                    var entries = 0;
                    var result = LegacyImportedImageBudget.Decode(tooLarge!,
                        LegacyImageSlot.Card, CancellationToken.None, () => entries++);
                    result.Bitmap?.Dispose();
                    Demand(result.Reason == "MEDIA_TOO_LARGE_COMPRESSED" && entries == 0,
                        "LM01_TOO_LARGE_MUST_REFUSE_BEFORE_DECODE");
                }
                Demand(!opened.TryOpenMedia(new EsDeMedia("image",
                    "../../outside-fiction.png", true), out var unauthorized) &&
                    unauthorized is null, "LM06_NONMEMBER_MUST_NOT_OPEN");
                Console.WriteLine("LM01_LM02_LM06_NATIVE_WINDOWS_PASS member_open=true oversized=4MiB+ decoder_entered=0 dimensions_4096_rejected=true nonmember_denied=true");

                // Direct successful decoder proof on BOTH PNG and JPEG from the
                // same guarded implementation, no Avalonia test/mock source.
                var sample = File.ReadAllBytes(fixture.ValidPng);
                using (var source = new MemoryStream(sample))
                {
                    var entries = 0;
                    var result = LegacyImportedImageBudget.Decode(source,
                        LegacyImageSlot.Card, CancellationToken.None, () => entries++);
                    Demand(result.Bitmap is not null && entries == 1 &&
                        result.Reason == "DECODE_OK" &&
                        result.Bitmap.PixelSize.Width <= 192 &&
                        result.Bitmap.PixelSize.Height <= 256,
                        "LM03_PNG_NATIVE_DECODE_REQUIRED");
                    result.Bitmap?.Dispose();
                }
                using (var source = File.OpenRead(jpg))
                {
                    var entries = 0;
                    var result = LegacyImportedImageBudget.Decode(source,
                        LegacyImageSlot.Gallery, CancellationToken.None, () => entries++);
                    Demand(result.Bitmap is not null && entries == 1 &&
                        result.Reason == "DECODE_OK", "LM03_JPEG_NATIVE_DECODE_REQUIRED");
                    result.Bitmap?.Dispose();
                }
                Console.WriteLine("LM03_PNG_JPEG_GUARDED_DECODE_PASS native_actual_bitmap=true");

                window.Width = 1120; window.Height = 720;
                var clock = Stopwatch.StartNew();
                Demand(window.CiLegacyImport(root, 50), "LM07_50_ESDE_IMPORT_FAILED");
                clock.Stop();
                Console.WriteLine("LM04_FIRST_RENDER_MS " + clock.ElapsedMilliseconds);
                Capture(window, gallery, "LM04-Placeholders-1120x720", 1120, 720);
                Capture(window, gallery, "LM04-Placeholders-760x480", 760, 480);

                var before = Process.GetCurrentProcess().PrivateMemorySize64;
                await WaitForQueueAsync(window, 30000);
                var after = Process.GetCurrentProcess().PrivateMemorySize64;
                var totals = window.LegacyMediaCounters();
                Demand(totals.RetainedBytes <= LegacyImportedImageBudget.MaxViewBytes &&
                    totals.PeakActive <= 1 &&
                    window.CiLegacyLiveBitmapCount >= 47 &&
                    window.CiLegacyLiveBitmapCount <= 50,
                    "LM04_50_CARDS_BUDGET_OR_SERIAL_WORKER_FAILED");
                Demand(window.CiLegacyTexts().Contains("LM Game 049", StringComparison.Ordinal),
                    "LM07_ESDE_METADATA_LOST");
                Console.WriteLine("LM04_NATIVE_50_CARDS_PASS count=50 attached=" +
                    window.CiLegacyLiveBitmapCount + " logicalRetainedBytes=" +
                    totals.RetainedBytes + " peakActiveDecodes=" + totals.PeakActive +
                    " privateBefore=" + before + " privateAfter=" + after +
                    " gcBytes=" + GC.GetTotalMemory(false) +
                    " nativeDecoderEntered=" + totals.NativeDecoderEntered +
                    " privateSampledPeak=" + memory.PeakPrivateBytes +
                    " managedSampledPeak=" + memory.PeakManagedBytes +
                    " sampledIntervalMs=20 PRIVATE_PROCESS_MAX_NOT_CLAIMED");
                Capture(window, gallery, "LM04-50Cards-1120x720", 1120, 720);
                Capture(window, gallery, "LM04-50Cards-760x480", 760, 480);

                Demand(window.CiLegacyOpenFirst(), "LM03_DETAIL_NOT_ACCESSIBLE");
                await WaitForQueueAsync(window, 30000);
                Demand(window.CiLegacyTexts().Contains("Médias référencés",
                    StringComparison.Ordinal), "LM03_DETAIL_METADATA_MISSING");
                Capture(window, gallery, "LM03-Detail-1120x720", 1120, 720);
                Capture(window, gallery, "LM03-Detail-760x480", 760, 480);

                // Prove the actual CoverFor + ImportedMediaPreview controls:
                // a corrupt primary cover must fall back to an independently
                // bounded thumbnail; both images are actually decoded/adopted.
                Demand(window.CiLegacyShowCollection() && window.CiLegacyOpenIndex(2),
                    "LM03_FALLBACK_DETAIL_NOT_ACCESSIBLE");
                var priorDecodes = window.LegacyMediaCounters().NativeDecoderEntered;
                await WaitForQueueAsync(window, 30000);
                Demand(window.CiLegacyLiveBitmapCount >= 2 &&
                    window.LegacyMediaCounters().NativeDecoderEntered > priorDecodes &&
                    window.LegacyMediaCounters().RetainedBytes <=
                        LegacyImportedImageBudget.MaxViewBytes,
                    "LM03_PRIMARY_CORRUPT_FALLBACK_AND_GALLERY_NOT_DECODED");
                Capture(window, gallery, "LM03-ThumbnailFallback-1120x720", 1120, 720);
                Capture(window, gallery, "LM03-ThumbnailFallback-760x480", 760, 480);

                Demand(window.CiLegacyShowCollection() && window.CiLegacyOpenIndex(3),
                    "LM03_JPEG_DETAIL_NOT_ACCESSIBLE");
                priorDecodes = window.LegacyMediaCounters().NativeDecoderEntered;
                await WaitForQueueAsync(window, 30000);
                Demand(window.CiLegacyLiveBitmapCount >= 2 &&
                    window.LegacyMediaCounters().NativeDecoderEntered >= priorDecodes + 2,
                    "LM03_JPEG_COVER_AND_GALLERY_NOT_DECODED");
                Capture(window, gallery, "LM03-JpegDetail-1120x720", 1120, 720);
                Capture(window, gallery, "LM03-JpegDetail-760x480", 760, 480);
                Console.WriteLine("LM03_ACTUAL_COVERFOR_AND_GALLERY_PASS fallback=true jpeg=true bitmap_adopted=true");

                var navBefore = Process.GetCurrentProcess().PrivateMemorySize64;
                for (var i = 0; i < 20; i++)
                {
                    Demand(window.CiLegacyShowDashboard(), "LM05_DASHBOARD_NAV_FAILED");
                    Demand(window.CiLegacyShowCollection(), "LM05_COLLECTION_NAV_FAILED");
                    Demand(window.CiLegacyOpenFirst(), "LM05_DETAIL_NAV_FAILED");
                }
                Demand(window.CiLegacyShowCollection(), "LM05_LAST_COLLECTION_NAV_FAILED");
                await WaitForQueueAsync(window, 30000);
                var navAfter = Process.GetCurrentProcess().PrivateMemorySize64;
                Demand(window.LegacyMediaCounters().RetainedBytes <=
                    LegacyImportedImageBudget.MaxViewBytes &&
                    window.LegacyMediaCounters().PeakActive <= 1,
                    "LM05_GENERATION_BUDGET_OR_SERIAL_FAILURE");
                Console.WriteLine("LM05_NATIVE_20_RAPID_NAVIGATIONS_PASS " +
                    "privateBefore=" + navBefore + " privateAfter=" + navAfter +
                    " retained=" + window.LegacyMediaCounters().RetainedBytes +
                    " generation=" + window.LegacyMediaCounters().Generation +
                    " NO_ZERO_NATIVE_MEMORY_LEAK_CLAIM");
                Capture(window, gallery, "LM05-FastNavigation-1120x720", 1120, 720);
                Capture(window, gallery, "LM05-FastNavigation-760x480", 760, 480);

                Demand(window.CiLegacySearch("Game 01"), "LM07_SEARCH_FAILED");
                Demand(window.CiLegacyTexts().Contains("Game 01", StringComparison.Ordinal),
                    "LM07_SEARCH_METADATA_LOST");
                Demand(window.CiLegacySearch(""), "LM07_CLEAR_SEARCH_FAILED");
                Demand(window.CiLegacyReimport(), "LM07_REIMPORT_FAILED");
                await WaitForQueueAsync(window, 30000);
                Capture(window, gallery, "LM07-Reimport-1120x720", 1120, 720);
                Capture(window, gallery, "LM07-Reimport-760x480", 760, 480);

                window.Close();
                Demand(window.LegacyMediaCounters().RetainedBytes == 0 &&
                    window.CiLegacyLiveBitmapCount == 0,
                    "LM05_BITMAPS_NOT_DISPOSED_AFTER_WINDOW_CLOSE");
                Console.WriteLine("LM05_DISPOSE_PASS retainedBytes=0 attached=0" +
                    " sampledPrivatePeak=" + memory.PeakPrivateBytes +
                    " sampledManagedPeak=" + memory.PeakManagedBytes +
                    " SAMPLE_NOT_NATIVE_HARD_CAP");
                Console.WriteLine("LM_01_02_03_04_05_06_07_WINDOWS_FIXTURES_PASS source=CI_ONLY");
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 74;
                Console.Error.WriteLine("LM141_WINDOWS_FIXTURE_FAIL " +
                    ex.GetType().Name + " " + ex.Message);
                window.Close();
            }
            finally
            {
                // Owned for the entire async native fixture session, not only
                // the synchronous Configure call that registered this callback.
                sampler.Dispose();
            }
        }, DispatcherPriority.Background);
    }

    // #145 Lot B: TARGETED native evidence for the changed View/VM/service boundary.
    // Reuses fixture generation and screenshot functions without replaying LM01..07.
    /// <summary>Exercises only the new lot-B media pipeline on a Windows CI-only Avalonia
    /// window, checking 50 cards, fallback, generation invalidation and 1120/760 captures.</summary>
    internal static void ConfigureLotB(MainWindow window)
    {
        var runner = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (!OperatingSystem.IsWindows() ||
            Environment.GetEnvironmentVariable("GITHUB_ACTIONS") != "true" ||
            string.IsNullOrWhiteSpace(runner))
            throw new InvalidOperationException("B05_FIXTURES_ONLY_WINDOWS_RUNNER_REQUIRED");
        var jpeg = Path.Combine(runner, "lm-valid-synthetic.jpg");
        if (!File.Exists(jpeg)) throw new InvalidDataException("B05_JPEG_FIXTURE_MISSING");
        var root = Path.Combine(runner, "LOTB145-" + Guid.NewGuid().ToString("N"));
        var gallery = Path.Combine(runner, "LOTB145-Windows-PNG");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(gallery);
        BuildFixture(root, jpeg); // all data generated in RUNNER_TEMP, no real user media.

        window.Opened += (_, _) => Dispatcher.UIThread.Post(async () =>
        {
            try
            {
                Demand(window.IsVisible && window.CiLegacyImport(root, 50),
                    "B05_ACCEPTED_FIXTURE_IMPORT_REQUIRED");
                await WaitForQueueAsync(window, 30000);
                var cards = window.LegacyMediaCounters();
                Demand(cards.PeakActive <= 1 && cards.Pending == 0 &&
                    cards.RetainedBytes <= LegacyImportedImageBudget.MaxViewBytes &&
                    window.CiLegacyLiveBitmapCount >= 47 &&
                    window.CiLegacyLiveBitmapCount <= 50,
                    "B05_50_CARD_SINGLE_FLIGHT_AND_RETENTION");
                Console.WriteLine("LOTB_B05_50_CARDS_PASS peak=" + cards.PeakActive +
                    " nativeEntries=" + cards.NativeDecoderEntered +
                    " retained=" + cards.RetainedBytes);

                window.Width = 1120; window.Height = 720;
                await WaitForQueueAsync(window, 30000);
                Capture(window, gallery, "B05-Collection-1120x720", 1120, 720);
                // The original B-05 capture changed Window.Width and immediately
                // measured/arranged Content. The Win32 Bounds/SizeChanged/layout
                // could still describe the preceding 1120px viewport; a centered
                // wide _page was consequently cropped on its left side.
                window.Width = 760; window.Height = 480;
                var prelayout = window.CiLegacyCollectionNarrowGeometry();
                Console.WriteLine("B05_BEFORE_RESIZE_LAYOUT fits=" + prelayout.Fits +
                    " geometry=" + prelayout.Geometry);
                await SettleNativeCollection760Async(window);
                await WaitForQueueAsync(window, 30000);
                var stable = window.CiLegacyCollectionNarrowGeometry();
                Demand(stable.Fits, "B05_760_LAYOUT_CLIPPED_AFTER_SETTLE:" + stable.Geometry);
                Console.WriteLine("B05_AFTER_RESIZE_LAYOUT fits=true geometry=" + stable.Geometry);
                Capture(window, gallery, "B05-Collection-760x480", 760, 480);
                Demand(window.CiLegacyCollectionNarrowGeometry().Fits,
                    "B05_760_CAPTURE_ARRANGE_INTRODUCED_CLIPPING");
                Demand(window.CiLegacyCollectionKeyboardFocus(),
                    "B05_760_FILTER_SEARCH_CARD_FOCUS_FAILED");
                Console.WriteLine("B05_760_NATIVE_FOCUS_PASS selector_search_card=true");
                // The card action is a real native button, and the detail→collection
                // route remains accessible at 760px without reimport or grants.
                Demand(window.CiLegacyOpenFirst() && window.CiLegacyShowCollection(),
                    "B05_760_CARD_NAVIGATION_FAILED");
                await SettleNativeCollection760Async(window);
                Demand(window.CiLegacyCollectionNarrowGeometry().Fits,
                    "B05_760_RETURN_NAVIGATION_CLIPPED");
                Console.WriteLine("B05_760_NATIVE_NAV_PASS detail_collection=true");

                Demand(window.CiLegacyOpenIndex(2), "B03_FALLBACK_GAME_NOT_OPENED");
                await WaitForQueueAsync(window, 30000);
                Demand(window.CiLegacyLiveBitmapCount >= 2 &&
                    window.LegacyMediaCounters().PeakActive <= 1,
                    "B03_DECLARED_THUMBNAIL_NOT_DISPLAYED");
                window.Width = 1120; window.Height = 720;
                await WaitForQueueAsync(window, 30000);
                Capture(window, gallery, "B03-Thumbnail-1120x720", 1120, 720);
                window.Width = 760; window.Height = 480;
                await WaitForQueueAsync(window, 30000);
                Capture(window, gallery, "B03-Thumbnail-760x480", 760, 480);
                Console.WriteLine("LOTB_B03_FALLBACK_WINDOWS_PASS declared_thumbnail=true");

                Demand(window.CiLegacyShowCollection(), "B04_COLLECTION_RETURN_FAILED");
                for (var i = 0; i < 8; i++)
                {
                    Demand(window.CiLegacyShowDashboard(), "B04_DASHBOARD_NAV_FAILED");
                    Demand(window.CiLegacyShowCollection(), "B04_COLLECTION_NAV_FAILED");
                }
                Demand(window.CiLegacySearch("LM Game 01"), "B04_FILTER_FAILED");
                Demand(window.CiLegacySearch(""), "B04_CLEAR_FILTER_FAILED");
                Demand(window.CiLegacyReimport(), "B01_REFRESH_REQUIRED");
                await WaitForQueueAsync(window, 30000);
                var settled = window.LegacyMediaCounters();
                Demand(settled.RetainedBytes <= LegacyImportedImageBudget.MaxViewBytes &&
                    settled.PeakActive <= 1 && settled.Pending == 0,
                    "B04_NAVIGATION_GENERATION_BUDGET_FAILED");
                window.Width = 1120; window.Height = 720;
                await WaitForQueueAsync(window, 30000);
                Capture(window, gallery, "B04-Refresh-1120x720", 1120, 720);
                window.Width = 760; window.Height = 480;
                await WaitForQueueAsync(window, 30000);
                Capture(window, gallery, "B04-Refresh-760x480", 760, 480);

                window.Close();
                Demand(window.CiLegacyLiveBitmapCount == 0 &&
                    window.LegacyMediaCounters().RetainedBytes == 0,
                    "B04_VIEW_DISPOSE_MUST_RELEASE_BITMAPS");
                Console.WriteLine("LOTB_B04_CANCEL_STALE_DISPOSE_PASS retained=0 attached=0");
                Console.WriteLine("LOTB_B03_B04_B05_WINDOWS_FIXTURES_PASS source=CI_ONLY");
            }
            catch (Exception error)
            {
                Environment.ExitCode = 76;
                Console.Error.WriteLine("LOTB_NATIVE_FIXTURE_FAIL " +
                    error.GetType().Name + " " + error.Message);
                window.Close();
            }
        }, DispatcherPriority.Background);
    }

    // B-05 only: wait for the REAL Windows resize, SizeChanged callback and
    // arranged visual-tree coordinates, not merely Pending==0 (which measures
    // decode work, not Avalonia layout readiness). Never force production
    // UpdateContentWidth or fake a synthetic viewport to make the oracle pass.
    private static async Task SettleNativeCollection760Async(MainWindow window)
    {
        var watch = Stopwatch.StartNew();
        string last = "LAYOUT_NOT_SAMPLED";
        while (watch.ElapsedMilliseconds < 6000)
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Loaded);
            await Task.Delay(50);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Render);
            var geometry = window.CiLegacyCollectionNarrowGeometry();
            last = geometry.Geometry;
            if (geometry.Fits && window.LegacyMediaCounters().Pending == 0)
                return;
        }
        throw new InvalidDataException("B05_760_LAYOUT_NOT_SETTLED:" + last);
    }

    // 20ms polling produces an OBSERVED sampled peak, never a proven OS hard
    // maximum. GC and Skia can retain native allocations across navigation.
    private sealed class MemorySample
    {
        private long _peakPrivate;
        private long _peakManaged;
        internal long PeakPrivateBytes => Volatile.Read(ref _peakPrivate);
        internal long PeakManagedBytes => Volatile.Read(ref _peakManaged);
        internal void Tick()
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            RecordMaximum(ref _peakPrivate, process.PrivateMemorySize64);
            RecordMaximum(ref _peakManaged, GC.GetTotalMemory(false));
        }
        private static void RecordMaximum(ref long destination, long value)
        {
            long previous;
            do
            {
                previous = Volatile.Read(ref destination);
                if (value <= previous) return;
            } while (Interlocked.CompareExchange(ref destination, value, previous) != previous);
        }
    }

    private static async Task WaitForQueueAsync(MainWindow window, int timeoutMs)
    {
        var watch = Stopwatch.StartNew();
        while (window.LegacyMediaCounters().Pending > 0 && watch.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(45);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        Demand(window.LegacyMediaCounters().Pending == 0, "LM04_DECODE_QUEUE_TIMEOUT");
    }

    private static void Capture(MainWindow window, string output, string state, int width, int height)
    {
        window.Width = width; window.Height = height;
        if (window.Content is not Control surface || !window.IsVisible)
            throw new InvalidDataException("LM07_REAL_ATTACHED_AVALONIA_REQUIRED");
        surface.Measure(new Size(width - 2, height - 2));
        surface.Arrange(new Rect(0, 0, width - 2, height - 2));
        using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        bitmap.Render(surface);
        var path = Path.Combine(output, state + ".png");
        bitmap.Save(path, PngBitmapEncoderOptions.Default);
        Demand(new FileInfo(path).Length > 4000, "LM07_PNG_EMPTY_" + state);
        Console.WriteLine("LM07_NATIVE_CAPTURE_PASS " + Path.GetFileName(path));
    }

    private sealed record FixturePaths(string GiantPng, string OversizedPng, string ValidPng);

    private static FixturePaths BuildFixture(string root, string jpeg)
    {
        var roms = Path.Combine(root, "ROMS", "gba");
        var covers = Path.Combine(root, "MEDIAS", "gba", "covers");
        var metadata = Path.Combine(root, "MEDIAS", "gba", "metadata");
        Directory.CreateDirectory(roms);
        Directory.CreateDirectory(covers);
        Directory.CreateDirectory(metadata);
        var xml = new StringBuilder("<gameList>");
        var giant = Path.Combine(covers, "cover000.png");
        var over = Path.Combine(covers, "cover001.png");
        var valid = Path.Combine(covers, "cover004.png");
        for (var i = 0; i < 50; i++)
        {
            var index = i.ToString("D3");
            File.WriteAllText(Path.Combine(roms, "Game" + index + ".gba"),
                "FICTIONAL NONEXECUTABLE TEST RECORD " + index);
            var file = i == 3 ? "cover003.jpg" : "cover" + index + ".png";
            var coverPath = Path.Combine(covers, file);
            if (i == 0) File.WriteAllBytes(coverPath, FakePng(4096, 4096, i));
            else if (i == 1) File.WriteAllBytes(coverPath,
                FakePng(2048, 1024, i, noisy: true));
            else if (i == 2) File.WriteAllText(coverPath, "INTENTIONALLY_INVALID_PNG");
            else if (i == 3) File.Copy(jpeg, coverPath, overwrite: true);
            else File.WriteAllBytes(coverPath, FakePng(192, 256, i));
            if (i == 2)
                File.WriteAllBytes(Path.Combine(covers, "thumb002.png"), FakePng(120, 144, 81));
            xml.Append("<game><path>./Game").Append(index)
                .Append(".gba</path><name>LM Game ").Append(index)
                .Append("</name><genre>Fixture</genre><image>../covers/")
                .Append(file).Append("</image>");
            if (i == 2) xml.Append("<thumbnail>../covers/thumb002.png</thumbnail>");
            xml.Append("</game>");
        }
        xml.Append("</gameList>");
        File.WriteAllText(Path.Combine(metadata, "es-de-gamelist.xml"), xml.ToString(),
            new UTF8Encoding(false));
        return new FixturePaths(giant, over, valid);
    }

    private static byte[] FakePng(int width, int height, int seed, bool noisy = false)
    {
        using var deflate = new MemoryStream();
        using (var z = new ZLibStream(deflate, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[checked(width * 4 + 1)];
            var random = new Random(seed + 1401);
            for (var y = 0; y < height; y++)
            {
                row[0] = 0;
                if (noisy) random.NextBytes(row.AsSpan(1));
                else
                {
                    for (var x = 0; x < width; x++)
                    {
                        row[x * 4 + 1] = (byte)(seed * 4 + y % 5);
                        row[x * 4 + 2] = (byte)(seed * 2);
                        row[x * 4 + 3] = (byte)(y % 255);
                        row[x * 4 + 4] = 255;
                    }
                }
                z.Write(row);
            }
        }
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8; header[9] = 6;
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", deflate.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream output, string tag, byte[] data)
    {
        var size = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(size, data.Length);
        output.Write(size);
        var type = Encoding.ASCII.GetBytes(tag);
        output.Write(type); output.Write(data);
        uint crc = 0xFFFFFFFFu;
        foreach (var b in type) crc = Crc(crc, b);
        foreach (var b in data) crc = Crc(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(size, ~crc);
        output.Write(size);
    }

    private static uint Crc(uint crc, byte b)
    {
        crc ^= b;
        for (var i = 0; i < 8; i++)
            crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320u;
        return crc;
    }
}
#endif
