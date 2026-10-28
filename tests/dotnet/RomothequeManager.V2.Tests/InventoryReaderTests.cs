using System.Security.Cryptography;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;

namespace RomothequeManager.V2.Tests;

// NEXT-02: invented files under a unique temp root; no real library, ROM data or network.
[TestClass]
public sealed class InventoryReaderTests
{
    private string _root = "";

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-next02-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private string Put(string relative, string content = "invented synthetic fixture")
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
        return file;
    }

    private InventoryScope Scope(int maxFiles = 100000, int depth = 8, TimeSpan? timeLimit = null) =>
        new(_root, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["gba"] = "gba", ["gb"] = "gb", ["psx"] = "psx" },
        MaxDepth: depth, MaxFiles: maxFiles, TimeLimit: timeLimit);

    [TestMethod]
    public void T01_NoXmlRequiredAndFileIsNotConfirmedGame()
    {
        Put("ROMS/gb/one.gb");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus);
        Assert.AreEqual(1, result.ObservedFileCount);
        Assert.AreEqual(1, result.TotalFileCount);
        Assert.IsNull(result.ConfirmedGameCount);
        Assert.AreEqual("FILESYSTEM_OBSERVATION", result.Observations.Single().SourceKind);
        Assert.AreEqual(InventoryKind.PotentialRom, result.Observations.Single().Kind);
    }

    [TestMethod]
    public void T02_AliasesAreExplicitAndUnknownPlatformIsHonest()
    {
        Put("ROMS/gba/known.GBA");
        Put("ROMS/mystery/unknown.gba");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual("gba", result.Observations.Single(x => x.RelativePath.Contains("known")).PlatformKey);
        Assert.AreEqual("UNKNOWN", result.Observations.Single(x => x.RelativePath.Contains("unknown")).PlatformKey);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "UNKNOWN_PLATFORM"));
    }

    [TestMethod]
    public void T03_ExtensionsAreConservativeCaseInsensitiveAndNoArchiveOpened()
    {
        Put("ROMS/gba/game.GBA");
        Put("ROMS/gba/image.ISO");
        Put("ROMS/gba/data.bin");
        Put("ROMS/gba/pack.zip");
        Put("ROMS/gba/note.txt");
        Put("ROMS/gba/cover.png");
        Put("ROMS/gba/mystery.xyz");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryKind.PotentialRom, result.Observations.Single(x => x.Extension == ".gba").Kind);
        Assert.AreEqual(3, result.Observations.Count(x => x.Kind == InventoryKind.ArchiveCandidate));
        Assert.AreEqual(InventoryKind.Sidecar, result.Observations.Single(x => x.Extension == ".txt").Kind);
        Assert.AreEqual(InventoryKind.Media, result.Observations.Single(x => x.Extension == ".png").Kind);
        Assert.AreEqual(InventoryKind.Unknown, result.Observations.Single(x => x.Extension == ".xyz").Kind);
        Assert.AreEqual(3, result.Diagnostics.Count(x => x.Code == "AMBIGUOUS_EXTENSION"));
    }

    [TestMethod]
    public void T04_HomonymsAcrossPlatformsRemainSeparateFiles()
    {
        Put("ROMS/gb/same.gb");
        Put("ROMS/gba/same.gb");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(2, result.Observations.Count);
        Assert.AreEqual(2, result.Observations.Select(o => o.LocationKey).Distinct().Count());
        Assert.AreEqual(0, result.PotentialDuplicates.Count);
        Assert.IsNull(result.ConfirmedGameCount);
    }

    [TestMethod]
    public void T05_CandidateDuplicatesAreReversibleAndNotBinaryProof()
    {
        Put("ROMS/gba/editionA/Game.gba", "same size invented");
        Put("ROMS/gba/editionB/Game.gba", "different bytes!!!");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(2, result.Observations.Count);
        Assert.AreEqual(1, result.PotentialDuplicates.Count);
        Assert.AreEqual(2, result.PotentialDuplicates.Single().ObservationIds.Count);
        Assert.IsTrue(result.PotentialDuplicates.Single().Reason.Contains("NOT a binary match"));
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "POTENTIAL_DUPLICATE"));
    }

    [TestMethod]
    public void T06_PlaylistAndDiscsAreObservedFilesNotThreeGames()
    {
        Put("ROMS/psx/disc1.chd");
        Put("ROMS/psx/disc2.chd");
        Put("ROMS/psx/set.m3u", "disc1.chd\ndisc2.chd\nnot-present.chd\n");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(3, result.ObservedFileCount);
        Assert.IsNull(result.ConfirmedGameCount);
        Assert.AreEqual(1, result.Playlists.Count);
        Assert.AreEqual(3, result.Playlists.Single().Members.Count);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "PLAYLIST_MEMBER_MISSING"));
    }

    [TestMethod]
    public void T07_EmptyRootIsCompleteButMissingRootFails()
    {
        var empty = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, empty.ScanStatus);
        Assert.AreEqual(0, empty.TotalFileCount);
        Directory.Delete(_root, true);
        var missing = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryScanStatus.Failed, missing.ScanStatus);
        Assert.IsNull(missing.TotalFileCount);
        Assert.AreEqual(InventoryAvailability.Offline, missing.Availability);
    }

    [TestMethod]
    public void T08_PartialDoesNotReplaceLastCompleteSnapshot()
    {
        Put("ROMS/gba/first.gba");
        var session = new InventorySession(Scope(maxFiles: 1));
        var accepted = session.Scan();
        Put("ROMS/gba/second.gba");
        var incomplete = session.Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, accepted.ScanStatus);
        Assert.AreEqual(InventoryScanStatus.Partial, incomplete.ScanStatus);
        Assert.IsNull(incomplete.TotalFileCount);
        Assert.AreSame(accepted, session.Current);
        Assert.AreSame(incomplete, session.LastAttempt);
        Assert.IsTrue(incomplete.Diagnostics.Any(d => d.Code == "LIMIT_REACHED"));
    }

    [TestMethod]
    public void T09_SymlinkIsNotFollowedWhenHostSupportsIt()
    {
        Put("ROMS/gba/first.gba");
        var target = Put("outside/invented.txt");
        var link = Path.Combine(_root, "ROMS", "gba", "link.gba");
        try { File.CreateSymbolicLink(link, target); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return; // Host disallows symlinks; other safety tests remain mandatory.
        }
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryScanStatus.Partial, result.ScanStatus);
        Assert.AreEqual(1, result.ObservedFileCount);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "LINK_SKIPPED"));
        File.Delete(link);
    }

    [TestMethod]
    public void T10_RelativeAndNetworkRootsAreRejected()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new InventorySession(new InventoryScope("relative-path")));
        Assert.ThrowsExactly<ArgumentException>(() => new InventorySession(new InventoryScope(@"\\server\share")));
        Assert.ThrowsExactly<ArgumentException>(() => new InventorySession(new InventoryScope("https://example.invalid")));
    }

    [TestMethod]
    public void T11_IdempotentMetadataAndVerifiedRelocation()
    {
        Put("ROMS/gb/one.gb");
        var session = new InventorySession(Scope());
        var first = session.Scan();
        var second = session.Scan();
        Assert.AreEqual(first.LibrarySessionId, second.LibrarySessionId);
        Assert.AreEqual(first.ContentFingerprint, second.ContentFingerprint);
        Assert.AreEqual(first.Observations.Single().LocationKey, second.Observations.Single().LocationKey);
        Assert.AreNotEqual(first.Observations.Single().ObservationId,
            second.Observations.Single().ObservationId);

        var moved = Path.Combine(Path.GetTempPath(), "rm-next02-moved-" + Guid.NewGuid().ToString("N"));
        Directory.Move(_root, moved);
        try
        {
            var after = session.RelocateVerified(moved);
            Assert.AreEqual(first.LibrarySessionId, after.LibrarySessionId);
            Assert.AreEqual(first.ContentFingerprint, after.ContentFingerprint);
        }
        finally { Directory.Move(moved, _root); }
    }

    [TestMethod]
    public void T12_CancelledScanIsPartialWithUnknownTotal()
    {
        Put("ROMS/gba/first.gba");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        var result = new InventorySession(Scope()).Scan(cancel.Token);
        Assert.AreEqual(InventoryScanStatus.Partial, result.ScanStatus);
        Assert.IsNull(result.TotalFileCount);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "CANCELLED"));
    }

    [TestMethod]
    public void T13_2000InventedFilesHaveCompleteObservedCount()
    {
        for (var i = 0; i < 2000; i++)
            Put("ROMS/gba/batch/game-" + i.ToString("D4") + ".gba");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus);
        Assert.AreEqual(2000, result.ObservedFileCount);
        Assert.AreEqual(2000, result.TotalFileCount);
    }

    [TestMethod]
    public void T14_UnsafePlaylistReferencesAreRefused()
    {
        Put("ROMS/psx/a.m3u", "../../secret.chd\nhttps://example.invalid/rom.bin\nC:/secret.bin\n");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(0, result.Playlists.Single().Members.Count);
        Assert.AreEqual(3, result.Diagnostics.Count(x => x.Code == "PLAYLIST_PATH_REFUSED"));
    }

    [TestMethod]
    public void T15_CompleteSnapshotSurvivesLostDrive()
    {
        Put("ROMS/gba/one.gba");
        var session = new InventorySession(Scope());
        var complete = session.Scan();
        Directory.Delete(_root, true);
        var attempt = session.Scan();
        Assert.AreEqual(InventoryScanStatus.Failed, attempt.ScanStatus);
        Assert.AreSame(complete, session.Current);
        Assert.AreSame(attempt, session.LastAttempt);
    }

    [TestMethod]
    public void T16_ReadOnlyInventoryDoesNotMutateSyntheticSource()
    {
        var path = Put("ROMS/gb/fictional.gb", "invented fixture only");
        var before = SHA256.HashData(File.ReadAllBytes(path));
        var result = new InventorySession(Scope()).Scan();
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(path)));
        Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus);
        Assert.IsFalse(File.Exists(Path.Combine(_root, "inventory.db")));
    }

    [TestMethod]
    public void T17_InvalidRelocationDoesNotReplaceCurrent()
    {
        Put("ROMS/gb/one.gb");
        var session = new InventorySession(Scope());
        var complete = session.Scan();
        var different = Path.Combine(Path.GetTempPath(), "rm-next02-other-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(different, "ROMS"));
        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() => session.RelocateVerified(different));
            Assert.AreSame(complete, session.Current);
        }
        finally { Directory.Delete(different, true); }
    }

    [TestMethod]
    public void T18_NoGameIdentityIsInventedByScanning()
    {
        Put("ROMS/gba/edition1/Game.gba");
        Put("ROMS/gba/edition2/Game.gba");
        Put("ROMS/gba/gamelist.xml", "<gameList>synthetic ignored</gameList>");
        var result = new InventorySession(Scope()).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus);
        Assert.AreEqual(3, result.Observations.Count);
        Assert.IsNull(result.ConfirmedGameCount);
        Assert.IsTrue(result.Observations.All(o => o.SourceKind == "FILESYSTEM_OBSERVATION"));
    }
}
