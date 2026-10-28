using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

// NEXT-03 backend-only proof. Never reads a user library; all roots are generated
// under an owned random temporary directory and cleaned only when owned by this run.
internal static class Program
{
    private static void Require(bool ok, string failure)
    {
        if (!ok) throw new InvalidOperationException(failure);
    }

    private static string RunNode(string repository, params string[] parameters)
    {
        var process = new ProcessStartInfo("node")
        {
            WorkingDirectory = repository,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        process.ArgumentList.Add(Path.Combine(repository, "tools", "demo-library", "generate.cjs"));
        foreach (var parameter in parameters) process.ArgumentList.Add(parameter);
        using var launched = Process.Start(process);
        Require(launched is not null, "NODE_NOT_STARTED");
        var standard = launched!.StandardOutput.ReadToEndAsync();
        var error = launched.StandardError.ReadToEndAsync();
        Require(launched.WaitForExit(120_000), "NODE_TIMEOUT");
        var result = standard.GetAwaiter().GetResult() + error.GetAwaiter().GetResult();
        Require(launched.ExitCode == 0, "NODE_FIXTURE_FAILED: " + result);
        return result;
    }

    private static Dictionary<string, string> Aliases(string repository)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            repository, "contracts", "demo-library-01", "catalogue.json")));
        return document.RootElement.GetProperty("platformSlugs").EnumerateArray()
            .Select(value => value.GetString()!)
            .ToDictionary(key => key, key => key, StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, string> Fingerprints(string root)
    {
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(file => Path.GetRelativePath(root, file),
                file => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))),
                StringComparer.Ordinal);
    }

    private static void AssertUnchanged(
        IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
    {
        Require(before.Count == after.Count, "SOURCE_FILE_COUNT_CHANGED");
        foreach (var (name, hash) in before)
            Require(after.TryGetValue(name, out var now) && now == hash,
                "SOURCE_MUTATED:" + name);
    }

    private static async Task TestVariant(
        string root, string variant, int expectedCount, int expectedDuplicates,
        IReadOnlyDictionary<string, string> aliases,
        HashSet<string> permittedRoots)
    {
        await using var service = new InventoryApplicationService(
            scope => permittedRoots.Contains(scope.LibraryRoot));
        var selected = service.PrepareSelection(new InventoryScope(root, aliases));
        Require(selected.Accepted, variant + "_SELECTION_REFUSED");
        Require(service.State.Phase == InventoryRunPhase.Ready &&
                service.State.Current is null && service.State.LastAttempt is null,
            variant + "_IMPLICIT_SCAN");

        var unconfirmed = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, false));
        Require(unconfirmed.Phase == InventoryRunPhase.Rejected &&
                service.State.LastAttempt is null, variant + "_CONSENT_BYPASS");

        var result = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        var scan = result.LastAttempt;
        Require(result.Phase == InventoryRunPhase.Completed &&
                scan?.ScanStatus == InventoryScanStatus.Complete, variant + "_NOT_COMPLETE");
        Require(scan.ObservedFileCount == expectedCount &&
                scan.TotalFileCount == expectedCount &&
                scan.Observations.Count == expectedCount,
            variant + "_OBSERVATION_COUNT_MISMATCH");
        Require(scan.CoverageProof.Any(p => p.PartitionKey == "ROMS" &&
                p.Status == InventoryScanStatus.Complete), variant + "_COVERAGE_UNKNOWN");
        Require(scan.ConfirmedGameCount is null, variant + "_INVENTED_GAMES");
        Require(scan.PotentialDuplicates.Count == expectedDuplicates,
            variant + "_DUPLICATE_GROUP_MISMATCH");
        Require(scan.Observations.Select(x => x.PlatformKey).Distinct().Count() == 13,
            variant + "_PLATFORMS_MISMATCH");
        Require(result.Current?.ScanId == scan.ScanId,
            variant + "_CURRENT_NOT_UPDATED");
        if (variant == "MYSTERY")
        {
            Require(!File.Exists(Path.Combine(root, "gamelist.xml")),
                "MYSTERY_UNEXPECTED_XML");
            var mediaRoot = Path.Combine(root, "MEDIAS");
            Require(!Directory.Exists(mediaRoot) ||
                    !Directory.EnumerateFiles(mediaRoot, "*.png",
                        SearchOption.AllDirectories).Any(), "MYSTERY_MEDIA_PRESENT");
        }
        Require(service.ClearSelection(), variant + "_CLEAR_FAILED");
        Require(service.State.LastAttempt is null && service.State.Current is null,
            variant + "_CROSS_LIBRARY_STATE_LEAK");
        Console.WriteLine("NEXT03_" + variant + "_PASS observed=" + expectedCount +
            " duplicates=" + expectedDuplicates + " gameCount=UNKNOWN");
    }

    private static async Task TestCancellation(
        string root, IReadOnlyDictionary<string, string> aliases,
        HashSet<string> permittedRoots)
    {
        await using var service = new InventoryApplicationService(
            scope => permittedRoots.Contains(scope.LibraryRoot));
        var firstCompleted = false;
        var cancelTriggered = false;
        var scope = new InventoryScope(root, aliases)
        {
            PlaylistChunkObserved = _ =>
            {
                if (firstCompleted && !cancelTriggered)
                {
                    cancelTriggered = true;
                    Require(service.RequestCancellation(), "CANCEL_REQUEST_REFUSED");
                }
            }
        };
        var selection = service.PrepareSelection(scope);
        Require(selection.Accepted, "CANCEL_SELECTION_FAILED");
        var complete = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        Require(complete.Current?.ScanStatus == InventoryScanStatus.Complete,
            "CANCEL_BASELINE_NOT_COMPLETE");
        firstCompleted = true;
        var cancelled = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        Require(cancelTriggered, "PLAYLIST_CANCELLATION_NOT_EXERCISED");
        Require(cancelled.LastAttempt?.ScanStatus == InventoryScanStatus.Partial &&
                cancelled.LastAttempt.TotalFileCount is null &&
                cancelled.LastAttempt.Diagnostics.Any(x => x.Code == "CANCELLED"),
            "CANCELLED_ATTEMPT_NOT_PARTIAL");
        Require(cancelled.Current?.ScanId == complete.Current.ScanId,
            "CANCEL_OVERWROTE_LAST_COMPLETE");
        Console.WriteLine("NEXT03_CANCELLED_PLAYLIST_PASS lastCompletePreserved=1");
    }

    private static async Task TestConcurrent(
        string root, IReadOnlyDictionary<string, string> aliases,
        HashSet<string> permittedRoots)
    {
        using var entered = new ManualResetEventSlim(false);
        using var release = new ManualResetEventSlim(false);
        await using var service = new InventoryApplicationService(
            scope => permittedRoots.Contains(scope.LibraryRoot));
        var scope = new InventoryScope(root, aliases)
        {
            PlaylistChunkObserved = _ =>
            {
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(15)))
                    throw new InvalidOperationException("TEST_PLAYLIST_GATE_TIMEOUT");
            }
        };
        var selection = service.PrepareSelection(scope);
        Require(selection.Accepted, "CONCURRENCY_SELECTION_FAILED");
        var running = service.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        try
        {
            Require(await Task.Run(() => entered.Wait(TimeSpan.FromSeconds(15))),
                "CONCURRENCY_TEST_DID_NOT_ENTER_PLAYLIST");
            var second = await service.StartConfirmedScanAsync(
                new InventoryRunRequest(selection.SelectionVersion, true));
            Require(second.Phase == InventoryRunPhase.Rejected &&
                    second.SafeDiagnosticCode == "SCAN_ALREADY_RUNNING",
                "CONCURRENT_SCAN_NOT_REJECTED");
            Require(!service.ClearSelection(), "RUNNING_SCOPE_CLEARED");
            Require(!service.PrepareSelection(new InventoryScope(root, aliases)).Accepted,
                "CONCURRENT_SCOPE_REPLACED");
            Require(service.RequestCancellation(), "RUNNING_CANCEL_REFUSED");
            Require(service.State.Phase == InventoryRunPhase.CancellationRequested,
                "CANCEL_PREMATURELY_COMPLETED");
        }
        finally { release.Set(); }
        var done = await running;
        Require(done.Phase == InventoryRunPhase.Completed &&
                done.LastAttempt?.ScanStatus == InventoryScanStatus.Partial,
            "CONCURRENT_CANCEL_NOT_PARTIAL");
        Console.WriteLine("NEXT03_SINGLE_FLIGHT_PASS noSecondScan=1");
    }

    private static async Task TestDefaultFailClosed(
        string root, IReadOnlyDictionary<string, string> aliases)
    {
        await using var service = new InventoryApplicationService();
        var selection = service.PrepareSelection(new InventoryScope(root, aliases));
        Require(selection.Accepted, "DEFAULT_PREFLIGHT_FAILED");
        var result = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        Require(result.Phase == InventoryRunPhase.Rejected &&
                result.SafeDiagnosticCode == "REAL_LIBRARY_SCAN_HOLD" &&
                service.State.LastAttempt is null,
            "DEFAULT_REAL_SCAN_HOLD_BYPASSED");
        Console.WriteLine("NEXT03_REAL_SCAN_HOLD_PASS publicConstructor=DENY");
    }

    private static async Task<int> Main(string[] arguments)
    {
        if (arguments.Length != 2 || arguments[0] != "--repo")
            throw new ArgumentException("Usage: --repo <repository-clone>");
        var repository = Path.GetFullPath(arguments[1]);
        var temp = Path.Combine(Path.GetTempPath(), "NEXT03-SYNTHETIC-" +
            Guid.NewGuid().ToString("N"));
        var output = Path.Combine(temp, "demo-output");
        Directory.CreateDirectory(temp);
        try
        {
            RunNode(repository, "--output", output);
            RunNode(repository, "--verify", output);
            var originalHashes = Fingerprints(output);
            var aliases = Aliases(repository);
            var roots = new[] { "IDEAL", "CHAOS", "MYSTERY" }
                .ToDictionary(name => name, name => Path.Combine(output, name));
            var permitted = new HashSet<string>(roots.Values,
                StringComparer.OrdinalIgnoreCase);
            await TestDefaultFailClosed(roots["IDEAL"], aliases);
            await TestVariant(roots["IDEAL"], "IDEAL", 32, 0, aliases, permitted);
            await TestVariant(roots["CHAOS"], "CHAOS", 33, 3, aliases, permitted);
            await TestVariant(roots["MYSTERY"], "MYSTERY", 32, 0, aliases, permitted);
            await TestCancellation(roots["IDEAL"], aliases, permitted);
            await TestConcurrent(roots["IDEAL"], aliases, permitted);
            AssertUnchanged(originalHashes, Fingerprints(output));
            Console.WriteLine("NEXT03_BACKEND_SMOKE_PASS immutableSyntheticFixture=1");
            return 0;
        }
        finally
        {
            // Only this executable created the GUID-scoped local temporary directory.
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
}
