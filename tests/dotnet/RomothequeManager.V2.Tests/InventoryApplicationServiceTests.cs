using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using RomothequeManager.V2.Inventory;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory.Application;

// NEXT-03 MSTest using only the existing NEXT-02 reader. All roots are generated
// under an owned random temporary directory and cleaned only when owned by this run.
[TestClass]
public sealed class InventoryApplicationServiceTests
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
                service.State.Phase == InventoryRunPhase.Rejected &&
                service.State.SafeDiagnosticCode == "CONSENT_OR_SELECTION_REQUIRED" &&
                service.State.LastAttempt is null, variant + "_CONSENT_BYPASS");

        var result = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        var scan = result.LastAttempt ?? throw new InvalidOperationException(variant + "_NO_LAST_ATTEMPT");
        Require(result.Phase == InventoryRunPhase.Completed &&
                scan.ScanStatus == InventoryScanStatus.Complete, variant + "_NOT_COMPLETE");
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
        Require(!service.RequestCancellation() &&
                service.State.LastAttempt?.ScanStatus == InventoryScanStatus.Complete,
            "LATE_CANCELLATION_DOWNGRADED_COMPLETE");
        firstCompleted = true;
        var cancelled = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        Require(cancelTriggered, "PLAYLIST_CANCELLATION_NOT_EXERCISED");
        Require(cancelled.LastAttempt?.ScanStatus == InventoryScanStatus.Partial &&
                cancelled.LastAttempt.TotalFileCount is null &&
                cancelled.LastAttempt.Diagnostics.Any(x => x.Code == "CANCELLED"),
            "CANCELLED_ATTEMPT_NOT_PARTIAL");
        Require(cancelled.Current?.ScanId == complete.Current!.ScanId,
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
                    second.SafeDiagnosticCode == "SCAN_ALREADY_RUNNING" &&
                    service.State.Phase == InventoryRunPhase.Running,
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

    private static async Task TestReentrantNotificationOrder(
        string root, IReadOnlyDictionary<string, string> aliases,
        HashSet<string> permittedRoots)
    {
        await using var service = new InventoryApplicationService(
            scope => permittedRoots.Contains(scope.LibraryRoot));
        var phases = new List<InventoryRunPhase>();
        bool cancelledByObserver = false;
        // Reentrant observers must not cause a later listener to see
        // CancellationRequested before the Running notification.
        service.StateChanged += (_, state) =>
        {
            if (!cancelledByObserver && state.Phase == InventoryRunPhase.Running)
            {
                cancelledByObserver = true;
                Require(service.RequestCancellation(), "OBSERVER_CANCEL_REFUSED");
            }
        };
        service.StateChanged += (_, state) => phases.Add(state.Phase);
        var selected = service.PrepareSelection(new InventoryScope(root, aliases));
        Require(selected.Accepted, "OBSERVER_SELECTION_REFUSED");
        var completed = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        Require(cancelledByObserver &&
                phases.SequenceEqual(new[]
                {
                    InventoryRunPhase.Ready,
                    InventoryRunPhase.Running,
                    InventoryRunPhase.CancellationRequested,
                    InventoryRunPhase.Completed
                }) &&
                completed.LastAttempt?.ScanStatus == InventoryScanStatus.Partial &&
                completed.LastAttempt.Diagnostics.Any(x => x.Code == "CANCELLED"),
            "OBSERVER_NOTIFICATION_ORDER_WRONG");
        Console.WriteLine("NEXT03_REENTRANT_EVENT_ORDER_PASS");
    }

    private static async Task TestDefaultFailClosed(
        string root, IReadOnlyDictionary<string, string> aliases)
    {
        await using var service = new InventoryApplicationService();
        var selection = service.PrepareSelection(new InventoryScope(root, aliases));
        Require(selection.Accepted, "DEFAULT_PREFLIGHT_FAILED");
        var notifications = new List<InventoryApplicationState>();
        service.StateChanged += (_, state) => notifications.Add(state);
        var result = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selection.SelectionVersion, true));
        Require(notifications.Count == 1 &&
                ReferenceEquals(notifications[0], service.State),
            "DEFAULT_HOLD_EVENT_NOT_PUBLISHED");
        Require(result.Phase == InventoryRunPhase.Rejected &&
                result.SafeDiagnosticCode == "REAL_LIBRARY_SCAN_HOLD" &&
                service.State.Phase == InventoryRunPhase.Rejected &&
                service.State.SafeDiagnosticCode == "REAL_LIBRARY_SCAN_HOLD" &&
                service.State.LastAttempt is null,
            "DEFAULT_REAL_SCAN_HOLD_BYPASSED");
        Console.WriteLine("NEXT03_REAL_SCAN_HOLD_PASS publicConstructor=DENY");
    }

    private static async Task TestUnavailableAfterComplete(
        string root, IReadOnlyDictionary<string, string> aliases,
        HashSet<string> permittedRoots)
    {
        await using var service = new InventoryApplicationService(
            scope => permittedRoots.Contains(scope.LibraryRoot));
        var selected = service.PrepareSelection(new InventoryScope(root, aliases));
        Require(selected.Accepted, "OFFLINE_SELECTION_FAILED");
        var previous = await service.StartConfirmedScanAsync(
            new InventoryRunRequest(selected.SelectionVersion, true));
        var complete = previous.Current ??
            throw new InvalidOperationException("OFFLINE_PREVIOUS_COMPLETE_MISSING");
        var unavailable = root + "-temporarily-unavailable";
        Require(!Directory.Exists(unavailable), "OFFLINE_TEST_ROOT_COLLISION");
        Directory.Move(root, unavailable);
        try
        {
            var result = await service.StartConfirmedScanAsync(
                new InventoryRunRequest(selected.SelectionVersion, true));
            Require(result.Current?.ScanId == complete.ScanId,
                "FAILED_ATTEMPT_OVERWROTE_CURRENT");
            Require(result.LastAttempt?.ScanStatus == InventoryScanStatus.Failed &&
                result.LastAttempt.TotalFileCount is null &&
                result.LastAttempt.Availability == InventoryAvailability.Offline &&
                result.LastAvailability == InventoryAvailability.Offline &&
                result.LastFreshness == InventoryFreshness.Unknown &&
                result.Diagnostics.Any(d => d.Code == "ROOT_UNAVAILABLE"),
                "OFFLINE_FAILED_STATE_NOT_PRESERVED");
            Require(result.ConfirmedGameCount is null, "OFFLINE_GAMES_INVENTED");
            var staleData = complete with { Freshness = InventoryFreshness.Stale };
            var simulatedStale = result with { LastAttempt = staleData };
            Require(simulatedStale.LastFreshness == InventoryFreshness.Stale &&
                simulatedStale.LastAvailability == complete.Availability,
                "STALE_PROVENANCE_NOT_EXPOSED_AS_IS");
            Console.WriteLine("NEXT03_FAILED_OFFLINE_PASS lastCompletePreserved=1");
            Console.WriteLine("NEXT03_STALE_ENUM_PROJECTION_PASS simulatedOnly=1");
        }
        finally { Directory.Move(unavailable, root); }
    }

    [TestMethod]
    public async Task NEXT03_AllThreePhysicalSyntheticLibrariesAndSafetyScenariosPass()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "tools", "demo-library", "generate.cjs")))
            directory = directory.Parent;
        Require(directory is not null, "REPOSITORY_FIXTURE_GENERATOR_MISSING");
        var repository = directory!.FullName;
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
            await TestReentrantNotificationOrder(roots["IDEAL"], aliases, permitted);
            await TestUnavailableAfterComplete(roots["IDEAL"], aliases, permitted);
            AssertUnchanged(originalHashes, Fingerprints(output));
            Console.WriteLine("NEXT03_BACKEND_SMOKE_PASS immutableSyntheticFixture=1");
        }
        finally
        {
            // Only this executable created the GUID-scoped local temporary directory.
            if (Directory.Exists(temp)) Directory.Delete(temp, recursive: true);
        }
    }
}
