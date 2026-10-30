using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.Tests;

// N05-M-A..J: synthetic, test-owned temporary files only; no personal media, ROMs or XML.
[TestClass]
public sealed class Next05MediaTests
{
    private string _root = "";
    private string _media = "";

    private static readonly IReadOnlyDictionary<string, string> Aliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["gba"] = "gba", ["gb"] = "gb", ["psx"] = "psx" };
    private static readonly IReadOnlyDictionary<string, MediaKind> Kinds =
        new Dictionary<string, MediaKind>(StringComparer.OrdinalIgnoreCase)
        { ["covers"] = MediaKind.Cover, ["thumbnails"] = MediaKind.Thumbnail,
          ["logos"] = MediaKind.Logo };
    private static readonly IReadOnlyDictionary<string, string> Mappings =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["gba"] = "gba", ["gb"] = "gb", ["psx"] = "psx" };

    [TestInitialize]
    public void Setup()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-next05-media-" + Guid.NewGuid().ToString("N"));
        _media = Path.Combine(_root, "MEDIAS");
        Directory.CreateDirectory(_media);
        Directory.CreateDirectory(Path.Combine(_root, "ROMS"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Path.GetFileName(_root).StartsWith("rm-next05-media-", StringComparison.Ordinal) &&
            Directory.Exists(_root))
            Directory.Delete(_root, true);
    }

    private static string Write(string root, string relative, byte[] bytes)
    {
        var file = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes);
        return file;
    }

    private string Rom(string relative) => Write(_root, "ROMS/" + relative, "INERT_ROM_FIXTURE"u8.ToArray());
    private string Media(string relative, byte[]? bytes = null) =>
        Write(_media, relative, bytes ?? PngHeader(2, 3));

    private MediaRootScope Scope(int maxFiles = 5000, int maxDepth = 8) =>
        new(_media, Aliases, Kinds, MaxFiles: maxFiles, MaxDepth: maxDepth);

    private MediaIndexApplicationService FixtureService() =>
        new(s => string.Equals(s.MediaRoot, _media,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) &&
            _media.StartsWith(Path.GetTempPath(), StringComparison.OrdinalIgnoreCase));

    private InventorySnapshot Roms() =>
        new InventorySession(new InventoryScope(_root, Aliases)).Scan();

    private static VerifiedScopeMapping Mapping(InventorySnapshot roms, MediaIndexSnapshot media) =>
        VerifiedScopeMapping.FromExplicitSelections(roms, media, Mappings, "EXPLICIT_TEST_ROOT_BINDING");

    private static uint Crc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static byte[] PngHeader(uint width, uint height)
    {
        // Valid PNG signature + IHDR with CRC. Deliberately HEADER-ONLY, not decodable pixels.
        var b = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(b, 0);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(b.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16, 4), width);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20, 4), height);
        b[24] = 8; b[25] = 2;
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(29, 4), Crc(b.AsSpan(12, 17)));
        return b;
    }

    private static byte[] JpegHeader(ushort width = 3, ushort height = 2)
    {
        // SOI + SOF0 (1 component) + EOI; verifies header syntax only, never pixel decode.
        return [255, 216, 255, 192, 0, 11, 8,
            (byte)(height >> 8), (byte)height, (byte)(width >> 8), (byte)width,
            1, 1, 0x11, 0, 255, 217];
    }

    private static void ExpectReadonly<T>(IReadOnlyList<T> list)
    {
        Assert.IsFalse(list is T[]);
        Assert.IsInstanceOfType<IList<T>>(list);
        var readOnly = (IList<T>)list;
        Assert.IsTrue(readOnly.IsReadOnly);
        Assert.ThrowsExactly<NotSupportedException>(() => readOnly.Clear());
    }

    [TestMethod]
    public void N05_M_A_LocalPngIndexesAndIsOnlyConventionCandidateWithoutXml()
    {
        Rom("gba/Fictitious.gba");
        Media("gba/covers/Fictitious.png");
        var roms = Roms();
        var svc = FixtureService();
        var image = svc.ScanConfirmed(Scope(), true);
        Assert.AreEqual(InventoryScanStatus.Complete, image.ScanStatus);
        Assert.AreEqual(1, image.ObservedFileCount);
        Assert.AreEqual("FILESYSTEM_MEDIA", image.Observations[0].SourceKind);
        Assert.AreEqual(MediaKind.Cover, image.Observations[0].Kind);
        Assert.AreEqual(MediaImageFormat.Png, image.Observations[0].DetectedFormat);
        Assert.IsTrue(image.Observations[0].HeaderValidated);
        Assert.AreEqual(2, image.Observations[0].Width);
        Assert.AreEqual(3, image.Observations[0].Height);
        var links = MediaLinkProjection.Reconcile(roms, image, Mapping(roms, image));
        Assert.AreEqual(MediaLinkStatus.UNIQUE_CONVENTION_CANDIDATE, links.Single().Status);
        Assert.AreEqual(1, links.Single().CandidateMediaObservationIds.Count);
        ExpectReadonly(links);
        ExpectReadonly(links.Single().Evidence);
        Assert.IsFalse(Directory.EnumerateFiles(_root, "*.xml", SearchOption.AllDirectories).Any());
    }

    [TestMethod]
    public void N05_M_B_XmlWithoutMediaFieldsIsNotNeededForIndependentIndex()
    {
        Rom("gba/NoReference.gba");
        Media("gba/covers/NoReference.png");
        Write(_media, "gba/metadata/es-de-gamelist.xml", "<gameList><game /></gameList>"u8.ToArray());
        var roms = Roms();
        var media = FixtureService().ScanConfirmed(Scope(), true);
        Assert.AreEqual(1, media.ObservedFileCount);
        Assert.AreEqual(MediaLinkStatus.UNIQUE_CONVENTION_CANDIDATE,
            MediaLinkProjection.Reconcile(roms, media, Mapping(roms, media)).Single().Status);
        Assert.AreEqual("MEDIAS/gba/covers/NoReference.png", media.Observations.Single().RelativePath);
    }

    [TestMethod]
    public void N05_M_C_StaleXmlLikeClaimCannotEstablishExactReferenceButVerifiedClaimCan()
    {
        Rom("gba/Replacement.gba");
        Media("gba/covers/Replacement.png");
        var roms = Roms();
        var media = FixtureService().ScanConfirmed(Scope(), true);
        var map = Mapping(roms, media);
        var id = roms.Observations.Single().ObservationId;
        var stale = new ExplicitMediaReference(id, "MEDIAS/gba/covers/Old.png",
            "HISTORICAL_IMPORT", "FIXTURE_STALE_REFERENCE");
        var disputed = MediaLinkProjection.Reconcile(roms, media, map, [stale]).Single();
        Assert.AreEqual(MediaLinkStatus.AMBIGUOUS, disputed.Status);
        Assert.IsTrue(disputed.Conflicts.Contains("STALE_OR_CONFLICTING_EXPLICIT_REFERENCE"));
        var valid = new ExplicitMediaReference(id, "MEDIAS/gba/covers/Replacement.png",
            "MANUAL_FIXTURE_CLAIM", "EXPLICIT_REFERENCE");
        var exact = MediaLinkProjection.Reconcile(roms, media, map, [valid]).Single();
        Assert.AreEqual(MediaLinkStatus.EXACT_REFERENCE, exact.Status);
        Assert.IsTrue(exact.SourceProvenance.Any(p => p.Contains("MANUAL_FIXTURE_CLAIM")));
        Assert.IsFalse(exact.Evidence.Any(e => e.Contains("GAME_IDENTIFIED")));
        var wrong = MediaLinkProjection.Reconcile(roms, media,
            map with { RomScanId = "stale-scan" }).Single();
        Assert.AreEqual(MediaLinkStatus.NOT_COMPARABLE, wrong.Status);
    }

    [TestMethod]
    public void N05_M_D_MissingCoverOnlyUnmatchedOnCompleteAndOfflineIsNotComparable()
    {
        Rom("gba/Missing.gba");
        var roms = Roms();
        var svc = FixtureService();
        var complete = svc.ScanConfirmed(Scope(), true);
        Assert.AreEqual(MediaLinkStatus.UNMATCHED,
            MediaLinkProjection.Reconcile(roms, complete, Mapping(roms, complete)).Single().Status);
        var offline = _media + ".detached";
        Directory.Move(_media, offline);
        try
        {
            var failed = svc.ScanConfirmed(Scope(), true);
            Assert.AreEqual(InventoryAvailability.Offline, failed.Availability);
            Assert.AreEqual(InventoryScanStatus.Failed, failed.ScanStatus);
            Assert.AreSame(complete, svc.Current);
            Assert.AreSame(failed, svc.LastAttempt);
            Assert.IsNull(failed.TotalFileCount);
            Assert.AreEqual(MediaLinkStatus.NOT_COMPARABLE,
                MediaLinkProjection.Reconcile(roms, failed, Mapping(roms, failed)).Single().Status);
        }
        finally { Directory.Move(offline, _media); }
    }

    [TestMethod]
    public void N05_M_E_DuplicateCoversOrRomHomonymsRemainAmbiguous()
    {
        Rom("gba/A/Fictitious.gba");
        Rom("gba/B/Fictitious.gba");
        Media("gba/covers/Fictitious.png");
        Media("gba/covers/Fictitious.jpg", JpegHeader());
        var roms = Roms();
        var media = FixtureService().ScanConfirmed(Scope(), true);
        Assert.AreEqual(2, media.Observations.Count);
        Assert.IsTrue(media.Observations.All(m => m.HeaderValidated));
        var links = MediaLinkProjection.Reconcile(roms, media, Mapping(roms, media));
        Assert.AreEqual(2, links.Count);
        Assert.IsTrue(links.All(l => l.Status == MediaLinkStatus.AMBIGUOUS));
        Assert.IsTrue(links.All(l => l.CandidateMediaObservationIds.Count == 2));
    }

    [TestMethod]
    public void N05_M_F_CancellationTimeoutAndBudgetsKeepPreviousComplete()
    {
        Media("gba/covers/First.png");
        var svc = FixtureService();
        var complete = svc.ScanConfirmed(Scope(), true);
        Media("gba/covers/Second.png");
        using var cts = new CancellationTokenSource();
        var cancelled = svc.ScanConfirmed(Scope() with { HeaderObserved = _ => cts.Cancel() },
            true, cts.Token);
        Assert.AreEqual(InventoryScanStatus.Partial, cancelled.ScanStatus);
        Assert.IsTrue(cancelled.Diagnostics.Any(d => d.Code == "MEDIA_CANCELLED"));
        Assert.IsTrue(cancelled.Observations.Any(o => !o.HeaderValidated),
            "Cancellation during an image header must prevent validation of that image.");
        Assert.IsNull(cancelled.TotalFileCount);
        Assert.AreSame(complete, svc.Current);
        Assert.AreSame(cancelled, svc.LastAttempt);
        var timed = svc.ScanConfirmed(Scope() with { TimeLimit = TimeSpan.FromTicks(1) }, true);
        Assert.AreEqual(InventoryScanStatus.Partial, timed.ScanStatus);
        Assert.IsNull(timed.TotalFileCount);
        var files = svc.ScanConfirmed(Scope(maxFiles: 1), true);
        Assert.AreEqual(InventoryScanStatus.Partial, files.ScanStatus);
        Assert.IsTrue(files.Diagnostics.Any(d => d.Code == "MEDIA_FILE_LIMIT"));
        Media("gba/covers/sub/nested.png");
        var depth = svc.ScanConfirmed(Scope(maxDepth: 2), true);
        Assert.AreEqual(InventoryScanStatus.Partial, depth.ScanStatus);
        Assert.IsTrue(depth.Diagnostics.Any(d => d.Code == "MEDIA_DEPTH_LIMIT"));
        var cumulative = svc.ScanConfirmed(Scope() with { MaxTotalHeaderBytes = 33 }, true);
        Assert.AreEqual(InventoryScanStatus.Partial, cumulative.ScanStatus);
        Assert.IsTrue(cumulative.Diagnostics.Any(d => d.Code == "MEDIA_HEADER_TOTAL_LIMIT"));
        Assert.AreSame(complete, svc.Current);
    }

    [TestMethod]
    public void N05_M_G_MalformedMismatchedOversizedAndWebpAreNotValidated()
    {
        Media("gba/covers/Truncated.png", [137, 80, 78, 71, 13, 10, 26, 10]);
        Media("gba/covers/Huge.png", PngHeader(9000, 9000));
        Media("gba/covers/BadCRC.png", PngHeader(3, 3));
        Media("gba/covers/Bad.jpg", [255, 216, 255, 192, 0, 200, 8]);
        Media("gba/covers/Wrong.jpg", PngHeader(2, 3));
        Media("gba/covers/Unsupported.webp", "RIFF1234WEBP"u8.ToArray());
        var crcBad = Path.Combine(_media, "gba", "covers", "BadCRC.png");
        var content = File.ReadAllBytes(crcBad); content[32] ^= 0x01;
        File.WriteAllBytes(crcBad, content);
        var snapshot = FixtureService().ScanConfirmed(Scope(), true);
        Assert.AreEqual(6, snapshot.ObservedFileCount);
        Assert.AreEqual(InventoryScanStatus.Complete, snapshot.ScanStatus);
        Assert.IsTrue(snapshot.Observations.All(o => !o.HeaderValidated));
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "MEDIA_INVALID_PNG_HEADER"));
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "MEDIA_INVALID_JPEG_HEADER"));
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "MEDIA_DIMENSION_LIMIT"));
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "MEDIA_EXTENSION_MISMATCH"));
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "UNSUPPORTED_FORMAT"));
    }

    [TestMethod]
    public void N05_M_H_UnsafePathsReparseAndTechnicalFoldersCannotBeRead()
    {
        Media("gba/covers/Visible.png");
        Media("ROMOTHEQUE_MANAGER/data/Secret.png");
        Media("gba/covers/logs/Secret.png");
        var outside = Write(_root, "OUTSIDE/External.png", PngHeader(2, 3));
        var link = Path.Combine(_media, "gba", "covers", "linked.png");
        var madeLink = false;
        try { File.CreateSymbolicLink(link, outside); madeLink = true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            Console.WriteLine("N05_M_H_LINK_NOT_EXECUTED host_link_creation_refused");
        }
        try
        {
            var snapshot = FixtureService().ScanConfirmed(Scope(), true);
            Assert.AreEqual(1, snapshot.ObservedFileCount);
            Assert.IsFalse(snapshot.Observations.Any(o => o.RelativePath.Contains("Secret")));
            Assert.IsFalse(snapshot.Observations.Any(o => o.RelativePath.Contains("linked")));
            if (madeLink)
            {
                Assert.AreEqual(InventoryScanStatus.Partial, snapshot.ScanStatus);
                Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "MEDIA_REPARSE_REFUSED"));
            }
            Assert.ThrowsExactly<ArgumentException>(() =>
                new MediaIndexApplicationService().ScanConfirmed(
                    new MediaRootScope(@"\\server\share\MEDIAS"), true));
            Assert.ThrowsExactly<ArgumentException>(() =>
                new MediaIndexApplicationService().ScanConfirmed(
                    new MediaRootScope(Path.Combine(_root, "ROMS")), true));
        }
        finally { if (madeLink) File.Delete(link); }
    }

    [TestMethod]
    public void N05_M_I_MoreThan2200SyntheticReferencesHaveDeterministicMeasuredMatches()
    {
        // 1 100 inert ROM files and 1 100 header-only images: 2 200 independent references.
        for (var i = 0; i < 1100; i++)
        {
            var name = "Fictional-" + i.ToString("D4");
            Rom("gba/" + (i / 100).ToString("D2") + "/" + name + ".gba");
            Media("gba/covers/" + name + ".png");
        }
        var clock = Stopwatch.StartNew();
        var baselineBytes = GC.GetTotalMemory(false);
        var roms = Roms();
        var media = FixtureService().ScanConfirmed(Scope(), true);
        var links = MediaLinkProjection.Reconcile(roms, media, Mapping(roms, media));
        var managedDelta = GC.GetTotalMemory(false) - baselineBytes;
        clock.Stop();
        Assert.AreEqual(1100, roms.ObservedFileCount);
        Assert.AreEqual(1100, media.ObservedFileCount);
        Assert.AreEqual(1100, links.Count);
        Assert.IsTrue(links.All(x => x.Status == MediaLinkStatus.UNIQUE_CONVENTION_CANDIDATE));
        Assert.AreEqual("MEDIAS/gba/covers/Fictional-0000.png", media.Observations[0].RelativePath);
        Assert.AreEqual("MEDIAS/gba/covers/Fictional-1099.png", media.Observations[^1].RelativePath);
        Console.WriteLine("NEXT05_MEDIA_2200_SYNTHETIC_REFS_PASS " +
            "roms=1100 media=1100 elapsed_ms=" + clock.ElapsedMilliseconds +
            " managed_delta_bytes=" + managedDelta);
    }

    [TestMethod]
    public async Task N05_M_J_PublicServiceAlwaysDeniesAndSourcesStayUnmodified()
    {
        var rom = Rom("gba/NoWrite.gba");
        var png = Media("gba/covers/NoWrite.png");
        var romDigest = SHA256.HashData(File.ReadAllBytes(rom));
        var imageDigest = SHA256.HashData(File.ReadAllBytes(png));
        var service = new MediaIndexApplicationService();
        var declined = await service.ScanConfirmedAsync(Scope(), true);
        Assert.AreEqual(InventoryScanStatus.Failed, declined.ScanStatus);
        Assert.AreEqual("REAL_MEDIA_SCAN_HOLD", declined.Diagnostics.Single().Code);
        Assert.IsNull(service.Current);
        Assert.AreEqual(0, declined.ObservedFileCount);
        var noConsent = service.ScanConfirmed(Scope(), false);
        Assert.AreEqual("MEDIA_CONSENT_REQUIRED", noConsent.Diagnostics.Single().Code);
        Assert.AreEqual(0, noConsent.ObservedFileCount);
        var fixture = FixtureService();
        var permitted = fixture.ScanConfirmed(Scope(), true);
        Assert.AreEqual(InventoryScanStatus.Complete, permitted.ScanStatus);
        CollectionAssert.AreEqual(romDigest, SHA256.HashData(File.ReadAllBytes(rom)));
        CollectionAssert.AreEqual(imageDigest, SHA256.HashData(File.ReadAllBytes(png)));
        ExpectReadonly(permitted.Observations);
        ExpectReadonly(permitted.Diagnostics);
        Assert.AreEqual(MediaImageFormat.Png, permitted.Observations.Single().DetectedFormat);
        Assert.IsNull(fixture.Current?.Observations.Single().DiagnosticRefs.FirstOrDefault());
    }
}
