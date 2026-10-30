using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Application;

namespace RomothequeManager.V2.Tests;

// N05-A..J: physical filesystem scans ONLY of tiny inert files in test-owned temp roots.
// Never reads any user's ROMs, images, XML or drives; no network, SQLite or ES-DE importer.
[TestClass]
public sealed class Next05ObservedFilesTests
{
    private string _root = "";

    [TestInitialize]
    public void CreateFixtureRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-next05-fixture-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS"));
    }

    [TestCleanup]
    public void DeleteOnlyOurFixtureRoot()
    {
        if (Path.GetFileName(_root).StartsWith("rm-next05-fixture-", StringComparison.Ordinal) &&
            Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static string PutIn(string root, string relative, string text = "INERT_FICTITIOUS_FIXTURE_ONLY")
    {
        var target = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.WriteAllText(target, text);
        return target;
    }

    private string Put(string relative, string text = "INERT_FICTITIOUS_FIXTURE_ONLY") =>
        PutIn(_root, relative, text);

    private InventoryScope Scope(
        int maxFiles = 100000, int maxDepth = 8,
        IReadOnlyDictionary<string, string>? aliases = null) =>
        new(_root, aliases, MaxDepth: maxDepth, MaxFiles: maxFiles);

    private static readonly IReadOnlyDictionary<string, string> ExplicitAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gb"] = "gb", ["gba"] = "gba", ["psx"] = "psx"
        };

    private static InventoryApplicationService FixtureOnlyService(string root) =>
        new(scope => string.Equals(scope.LibraryRoot, root,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal));

    [TestMethod]
    public async Task N05_A_MysteryWithoutAnyXmlProducesConsultableObservations()
    {
        Put("ROMS/unconfigured/Unknown Cartridge.gba");
        Put("MEDIAS/unconfigured/covers/Unlinked.png");
        Put("ROMOTHEQUE_MANAGER/data/catalog.sqlite", "NOT_A_DATABASE");
        await using var service = FixtureOnlyService(_root);
        var selected = service.PrepareSelection(Scope());
        Assert.IsTrue(selected.Accepted);
        Assert.IsNull(service.State.Current, "Picker must not trigger a scan.");
        var state = await service.StartConfirmedScanAsync(new(selected.SelectionVersion, true));
        var captured = state.Current!;
        Assert.AreEqual(InventoryScanStatus.Complete, captured.ScanStatus);
        Assert.AreEqual(1, captured.ObservedFileCount, "Scanner scope is ROMS only.");
        Assert.IsNull(captured.ConfirmedGameCount);
        Assert.AreEqual("FILESYSTEM_OBSERVATION", captured.Observations.Single().SourceKind);
        Assert.AreEqual("UNKNOWN", captured.Observations.Single().PlatformKey);
        var page = InventoryObservationProjection.GetPage(captured);
        Assert.AreSame(captured, page.Snapshot);
        Assert.AreEqual(1, page.MatchingObservationCount);
        Assert.AreEqual("Unknown Cartridge.gba", page.Rows.Single().ProvisionalLabel);
        Assert.AreSame(captured.Observations.Single(), page.Rows.Single().Observation);
        Assert.IsFalse(Directory.EnumerateFiles(_root, "*.xml", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    public async Task N05_B_IdealChaosMysteryAllUseRealReaderWithoutXml()
    {
        foreach (var variant in new[] { "IDEAL", "CHAOS", "MYSTERY" })
        {
            var root = Path.Combine(_root, variant);
            PutIn(root, "ROMS/gba/Fictitious.gba");
            PutIn(root, "ROMS/psx/Disc A.chd");
            if (variant == "CHAOS") PutIn(root, "ROMS/misc/unknown.opaque");
            if (variant == "MYSTERY") PutIn(root, "ROMS/misc/Unknown Region (Synthetic).zip");
            await using var service = FixtureOnlyService(root);
            var selected = service.PrepareSelection(new InventoryScope(root, ExplicitAliases));
            Assert.IsTrue(selected.Accepted);
            var rejected = await service.StartConfirmedScanAsync(new(selected.SelectionVersion, false));
            Assert.AreEqual(InventoryRunPhase.Rejected, rejected.Phase);
            Assert.IsNull(rejected.LastAttempt);
            var state = await service.StartConfirmedScanAsync(new(selected.SelectionVersion, true));
            var snapshot = state.Current!;
            Assert.AreEqual(variant == "IDEAL" ? 2 : 3, snapshot.ObservedFileCount);
            Assert.IsNull(snapshot.ConfirmedGameCount);
            Assert.IsTrue(snapshot.Observations.All(x => x.SourceKind == "FILESYSTEM_OBSERVATION"));
            Assert.AreEqual(snapshot.ObservedFileCount,
                InventoryObservationProjection.GetPage(snapshot).MatchingObservationCount);
            Assert.IsFalse(Directory.EnumerateFiles(root, "*.xml", SearchOption.AllDirectories).Any());
            Assert.IsFalse(Directory.Exists(Path.Combine(root, "ES-DE")));
            Console.WriteLine("NEXT05_" + variant + "_SYNTHETIC_PASS observations=" +
                snapshot.ObservedFileCount + " gameCount=UNKNOWN");
        }
    }

    [TestMethod]
    public void N05_C_ArchivesSidecarsAndUnknownStayFilesAndAreNeverExtracted()
    {
        var archive = Put("ROMS/gba/Archive candidate.zip", "FAKE_ZIP_BYTES_NOT_AN_ARCHIVE");
        Put("ROMS/gba/Unknown.7z");
        Put("ROMS/gba/Disc.iso");
        Put("ROMS/gba/Track.bin");
        Put("ROMS/gba/notes.nfo");
        Put("ROMS/gba/background.jpeg");
        Put("ROMS/gba/opaque.mystery");
        var before = SHA256.HashData(File.ReadAllBytes(archive));
        var snapshot = new InventorySession(Scope(aliases: ExplicitAliases)).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, snapshot.ScanStatus);
        Assert.AreEqual(7, snapshot.ObservedFileCount);
        Assert.AreEqual(4, snapshot.Observations.Count(x => x.Kind == InventoryKind.ArchiveCandidate));
        Assert.AreEqual(1, snapshot.Observations.Count(x => x.Kind == InventoryKind.Unknown));
        Assert.AreEqual(1, snapshot.Observations.Count(x => x.Kind == InventoryKind.Media));
        Assert.AreEqual(1, snapshot.Observations.Count(x => x.Kind == InventoryKind.Sidecar));
        Assert.IsNull(snapshot.ConfirmedGameCount);
        Assert.IsFalse(Directory.Exists(Path.Combine(_root, "ROMS", "gba", "Archive candidate")));
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(archive)));
    }

    [TestMethod]
    public void N05_D_PlaylistDiscAndCueDoNotClaimThreeIdentifiedGames()
    {
        Put("ROMS/psx/Disc One.chd");
        Put("ROMS/psx/Disc Two.chd");
        Put("ROMS/psx/Disc Metadata.cue", "FILE \"Invented.bin\" BINARY");
        Put("ROMS/psx/Disc Set.m3u", "Disc One.chd\nDisc Two.chd\n");
        var snapshot = new InventorySession(Scope(aliases: ExplicitAliases)).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, snapshot.ScanStatus);
        Assert.AreEqual(4, snapshot.ObservedFileCount);
        Assert.IsNull(snapshot.ConfirmedGameCount);
        Assert.AreEqual(1, snapshot.Playlists.Count, "A cue is not parsed or treated as playlist here.");
        Assert.AreEqual(2, snapshot.Playlists.Single().Members.Count);
        Assert.IsTrue(snapshot.Playlists.Single().Members.All(x => x.Exists));
        Assert.AreEqual(1, snapshot.Observations.Count(x => x.Kind == InventoryKind.Playlist));
        Assert.AreEqual(1, snapshot.Observations.Count(x => x.Extension == ".cue"));
    }

    [TestMethod]
    public void N05_E_UnicodeVariantsFilteringSortingPagingAndTransientIds()
    {
        Put("ROMS/gb/Édition région/Cartouche (EU).gb");
        Put("ROMS/gb/Édition région/Cartouche (JP).gb");
        Put("ROMS/gba/Édition région/Cartouche (EU).gba");
        Put("ROMS/unknown/opaque.xyz");
        var session = new InventorySession(Scope(aliases: ExplicitAliases));
        var initial = session.Scan();
        var first = InventoryObservationProjection.GetPage(initial, new(
            PlatformKey: "GB", Extension: ".GB", PageIndex: 0, PageSize: 1));
        var second = InventoryObservationProjection.GetPage(initial, new(
            PlatformKey: "gb", Extension: ".gb", PageIndex: 1, PageSize: 1));
        Assert.AreEqual(2, first.MatchingObservationCount);
        Assert.AreEqual(2, second.MatchingObservationCount);
        Assert.AreEqual("Cartouche (EU).gb", first.Rows.Single().ProvisionalLabel);
        Assert.AreEqual("Cartouche (JP).gb", second.Rows.Single().ProvisionalLabel);
        Assert.AreEqual(1, InventoryObservationProjection.GetPage(initial,
            new(SearchText: "opaque", Kind: InventoryKind.Unknown)).MatchingObservationCount);
        Assert.AreEqual(0, InventoryObservationProjection.GetPage(initial,
            new(SearchText: "NO SUCH FILE")).MatchingObservationCount);
        Assert.AreEqual(0, InventoryObservationProjection.GetPage(initial,
            new(PageIndex: int.MaxValue, PageSize: 200)).Rows.Count);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            InventoryObservationProjection.GetPage(initial, new(PageSize: 0)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            InventoryObservationProjection.GetPage(initial, new(PageIndex: -1)));
        var again = session.Scan();
        Assert.AreEqual(initial.ContentFingerprint, again.ContentFingerprint);
        Assert.AreNotEqual(initial.Observations[0].ObservationId, again.Observations[0].ObservationId);
        Assert.AreEqual(initial.Observations[0].LocationKey, again.Observations[0].LocationKey);
        Assert.IsNull(again.ConfirmedGameCount);
    }

    [TestMethod]
    public async Task N05_F_ProductionConstructorDeniesEvenWithConsentAndRejectsOtherScope()
    {
        Put("ROMS/gba/Inert.gba");
        await using var product = new InventoryApplicationService();
        var selection = product.PrepareSelection(Scope(aliases: ExplicitAliases));
        Assert.IsTrue(selection.Accepted);
        var denied = await product.StartConfirmedScanAsync(new(selection.SelectionVersion, true));
        Assert.AreEqual(InventoryRunPhase.Rejected, denied.Phase);
        Assert.AreEqual("REAL_LIBRARY_SCAN_HOLD", denied.SafeDiagnosticCode);
        Assert.IsNull(denied.LastAttempt);
        Assert.IsNull(denied.Current);
        var other = Path.Combine(_root, "other");
        Put("other/ROMS/gba/Other.gba");
        await using var fixture = FixtureOnlyService(_root);
        var chosen = fixture.PrepareSelection(new InventoryScope(other, ExplicitAliases));
        Assert.IsTrue(chosen.Accepted);
        var otherDenied = await fixture.StartConfirmedScanAsync(new(chosen.SelectionVersion, true));
        Assert.AreEqual("REAL_LIBRARY_SCAN_HOLD", otherDenied.SafeDiagnosticCode);
        Assert.IsNull(otherDenied.LastAttempt);
        Assert.ThrowsExactly<ArgumentException>(() => new InventorySession(new InventoryScope(@"\\server\share")));
    }

    [TestMethod]
    public void N05_G_CancelDuringActualPlaylistReadAndBudgetsPreserveCurrent()
    {
        Put("ROMS/psx/Stable.chd");
        using var cts = new CancellationTokenSource();
        var scope = Scope(aliases: ExplicitAliases) with
        {
            PlaylistChunkObserved = _ => cts.Cancel()
        };
        var session = new InventorySession(scope);
        var complete = session.Scan();
        var playlist = Put("ROMS/psx/Added.m3u", new string('x', 6000));
        var interrupted = session.Scan(cts.Token);
        Assert.AreEqual(InventoryScanStatus.Partial, interrupted.ScanStatus);
        Assert.IsNull(interrupted.TotalFileCount);
        Assert.AreSame(complete, session.Current);
        Assert.AreSame(interrupted, session.LastAttempt);
        Assert.IsTrue(interrupted.Diagnostics.Any(x => x.Code == "CANCELLED"));
        using (var exclusive = File.Open(playlist, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsTrue(exclusive.CanRead);

        var limited = new InventorySession(Scope(maxFiles: 1, aliases: ExplicitAliases)).Scan();
        Assert.AreEqual(InventoryScanStatus.Partial, limited.ScanStatus);
        Assert.IsNull(limited.TotalFileCount);
        Assert.IsTrue(limited.Diagnostics.Any(x => x.Code == "LIMIT_REACHED"));
        Put("ROMS/psx/deeper/nested.chd");
        var depth = new InventorySession(Scope(maxDepth: 1, aliases: ExplicitAliases)).Scan();
        Assert.AreEqual(InventoryScanStatus.Partial, depth.ScanStatus);
        Assert.IsTrue(depth.Diagnostics.Any(x => x.Code == "LIMIT_REACHED"));
        var timeout = new InventorySession(Scope(aliases: ExplicitAliases) with
        {
            TimeLimit = TimeSpan.FromTicks(1)
        }).Scan();
        Assert.AreEqual(InventoryScanStatus.Partial, timeout.ScanStatus);
        Assert.IsNull(timeout.TotalFileCount);
    }

    [TestMethod]
    public void N05_H_OfflineAndStaleAreDistinctAndNeverBecomeDeleted()
    {
        Put("ROMS/gb/WasObserved.gb");
        var session = new InventorySession(Scope(aliases: ExplicitAliases));
        var complete = session.Scan();
        var offline = Path.Combine(Path.GetTempPath(), "rm-next05-displaced-" + Guid.NewGuid().ToString("N"));
        Directory.Move(_root, offline);
        try
        {
            var attempt = session.Scan();
            Assert.AreEqual(InventoryScanStatus.Failed, attempt.ScanStatus);
            Assert.AreEqual(InventoryAvailability.Offline, attempt.Availability);
            Assert.IsNull(attempt.TotalFileCount);
            Assert.AreSame(complete, session.Current);
            Assert.AreSame(attempt, session.LastAttempt);
            var stale = complete with { Freshness = InventoryFreshness.Stale };
            var page = InventoryObservationProjection.GetPage(stale);
            Assert.AreEqual(InventoryFreshness.Stale, page.Snapshot.Freshness);
            Assert.AreEqual(InventoryAvailability.Online, page.Snapshot.Availability);
            Assert.AreEqual(1, page.Rows.Count);
            Assert.IsFalse(page.Snapshot.Diagnostics.Any(x => x.Code == "DELETED"));
        }
        finally
        {
            Directory.Move(offline, _root);
        }
    }

    [TestMethod]
    public void N05_I_ReparseOrUnsafePlaylistIsRejectedAndNoOutsideObservation()
    {
        Put("ROMS/psx/Stable.chd");
        Put("outside/Inert.chd");
        Put("ROMS/psx/Unsafe.m3u",
            "../../outside/Inert.chd\nhttps://example.invalid/inert.chd\n");
        var link = Path.Combine(_root, "ROMS", "psx", "linked.chd");
        bool linkCreated = false;
        try { File.CreateSymbolicLink(link, Path.Combine(_root, "outside", "Inert.chd")); linkCreated = true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Console.WriteLine("N05_I_REPARSE_NOT_EXECUTED host_link_creation_refused");
        }
        try
        {
            var result = new InventorySession(Scope(aliases: ExplicitAliases)).Scan();
            Assert.AreEqual(0, result.Playlists.Single().Members.Count);
            Assert.AreEqual(2, result.Diagnostics.Count(x => x.Code == "PLAYLIST_PATH_REFUSED"));
            Assert.IsFalse(result.Observations.Any(x => x.RelativePath.Contains("outside", StringComparison.OrdinalIgnoreCase)));
            if (linkCreated)
            {
                Assert.AreEqual(InventoryScanStatus.Partial, result.ScanStatus);
                Assert.IsTrue(result.Diagnostics.Any(x => x.Code == "LINK_SKIPPED"));
            }
            else
            {
                Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus);
            }
        }
        finally
        {
            if (linkCreated) File.Delete(link);
        }
    }

    [TestMethod]
    public void N05_J_MoreThan2000RealSyntheticObservationsAreMeasuredAndPaginated()
    {
        for (var i = 0; i < 2200; i++)
            Put("ROMS/gba/Batch-" + (i / 200).ToString("D2") +
                "/Completely Fictional " + i.ToString("D4") + ".gba");
        var specimen = Path.Combine(_root, "ROMS", "gba", "Batch-00", "Completely Fictional 0000.gba");
        var hash = SHA256.HashData(File.ReadAllBytes(specimen));
        var baselineBytes = GC.GetTotalMemory(false);
        var clock = Stopwatch.StartNew();
        var snapshot = new InventorySession(Scope(maxFiles: 2300, aliases: ExplicitAliases)).Scan();
        var page = InventoryObservationProjection.GetPage(snapshot, new(
            PlatformKey: "gba", Extension: ".gba", PageIndex: 10, PageSize: 100));
        clock.Stop();
        var managedDelta = GC.GetTotalMemory(false) - baselineBytes;
        Assert.AreEqual(InventoryScanStatus.Complete, snapshot.ScanStatus);
        Assert.AreEqual(2200, snapshot.ObservedFileCount);
        Assert.AreEqual(2200, snapshot.TotalFileCount);
        Assert.AreEqual(2200, page.MatchingObservationCount);
        Assert.AreEqual(100, page.Rows.Count);
        Assert.IsNull(snapshot.ConfirmedGameCount);
        Assert.IsTrue(snapshot.Observations.All(x => x.SourceKind == "FILESYSTEM_OBSERVATION"));
        CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(specimen)));
        Assert.IsFalse(File.Exists(Path.Combine(_root, "catalog.sqlite")));
        Console.WriteLine("NEXT05_2200_SYNTHETIC_OBSERVATIONS_PASS observed=2200 " +
            "elapsed_ms=" + clock.ElapsedMilliseconds +
            " managed_delta_bytes=" + managedDelta + " gameCount=UNKNOWN");
    }
}
