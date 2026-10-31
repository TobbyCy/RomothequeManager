using System.Buffers.Binary;
using System.Diagnostics;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Inventory.Media.MiA;
using RomothequeManager.V2.Inventory.Media.MiB;
using RomothequeManager.V2.Inventory.RsA;
using RomothequeManager.V2.MvvmFoundation;
using RomothequeManager.V2.Persistence;

namespace RomothequeManager.V2.Tests;

/// <summary>
/// G1 PERM-F01..04: adversarial checks against the actual production gates,
/// anchored ROMS/MEDIAS scanners, MI-B application service, UI ViewModel and SQLite.
/// Only generated Windows %TEMP% fixture directories; never a personal device.
/// No fake grant service, injected permission delegate or scanner mock.
/// </summary>
[TestClass]
public sealed class G1PermissionBoundaryFixtureTests
{
    private string _romFixture = "";
    private string _mediaFixture = "";
    private string _dbFixture = "";
    private string _roms = "";
    private string _medias = "";
    private string _database = "";
    private readonly string _romId = Guid.NewGuid().ToString("D");
    private readonly string _mediaId = Guid.NewGuid().ToString("D");
    private readonly string _libraryId = Guid.NewGuid().ToString("D");

    [TestInitialize]
    public void Setup()
    {
        Assert.IsTrue(OperatingSystem.IsWindows(), "G1_NATIVE_WINDOWS_REQUIRED");
        _romFixture = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"));
        _mediaFixture = Path.Combine(Path.GetTempPath(),
            "rm-mia-fixture-" + Guid.NewGuid().ToString("N"));
        _dbFixture = Path.Combine(Path.GetTempPath(),
            "rm-persist01-fixture-" + Guid.NewGuid().ToString("N"));
        _roms = Path.Combine(_romFixture, "ROMS");
        _medias = Path.Combine(_mediaFixture, "MEDIAS");
        _database = Path.Combine(_dbFixture, "ROMOTHEQUE_MANAGER", "data", "catalog.sqlite");
        Directory.CreateDirectory(Path.Combine(_roms, "gba"));
        Directory.CreateDirectory(Path.Combine(_medias, "gba"));
        File.WriteAllBytes(Path.Combine(_roms, "gba", "fixture.gba"),
            "SYNTHETIC_NON_ROM_FILE"u8.ToArray());
        File.WriteAllBytes(Path.Combine(_medias, "gba", "fixture.png"), TinyPngHeader());
        Console.WriteLine("G1_NATIVE_ENV OS=" + Environment.OSVersion +
            " FS=" + new DriveInfo(Path.GetPathRoot(_roms)!).DriveFormat);
    }

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var folder in new[] { _romFixture, _mediaFixture, _dbFixture })
        {
            if (!string.IsNullOrEmpty(folder) &&
                (Path.GetFileName(folder).StartsWith("rm-rsa-fixture-", StringComparison.Ordinal) ||
                 Path.GetFileName(folder).StartsWith("rm-mia-fixture-", StringComparison.Ordinal) ||
                 Path.GetFileName(folder).StartsWith("rm-persist01-fixture-", StringComparison.Ordinal)) &&
                Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    private static byte[] TinyPngHeader()
    {
        var b = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(b, 0);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(b.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20, 4), 1);
        b[24] = 8;
        b[25] = 6;
        uint crc = 0xffffffff;
        foreach (var value in b.AsSpan(12, 17))
        {
            crc ^= value;
            for (int i = 0; i < 8; ++i)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(29, 4), ~crc);
        return b;
    }

    private RsAFixtureRomSession Roms()
    {
        var session = new RsAFixtureRomSession();
        session.SelectFixture(_roms, _romId);
        return session;
    }

    private MiAFixtureMediaSession Medias()
    {
        var session = new MiAFixtureMediaSession();
        session.SelectFixture(_medias, _mediaId);
        return session;
    }

    private static RsAReadGrant RomGrant(RsAFixtureRomSession session, int expiresSeconds = 120) =>
        session.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddSeconds(expiresSeconds));

    private static MiAReadGrant MediaGrant(MiAFixtureMediaSession session, int expiresSeconds = 120) =>
        session.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddSeconds(expiresSeconds));

    private static void RomDenied(InventorySnapshot snapshot)
    {
        Assert.AreEqual(InventoryScanStatus.Failed, snapshot.ScanStatus);
        Assert.AreEqual(0, snapshot.ObservedFileCount);
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "RS_A_DENIED"));
    }

    private static void MediaDenied(MediaIndexSnapshot snapshot)
    {
        Assert.AreEqual(InventoryScanStatus.Failed, snapshot.ScanStatus);
        Assert.AreEqual(0, snapshot.ObservedFileCount);
        Assert.AreEqual(0L, snapshot.Observations.Sum(_ => 1L));
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "MI_A_DENIED"));
    }

    [TestMethod]
    public void PERM_F01_RomGrantCannotEnterMediaScannerAndMediaGrantCannotEnterRomScanner()
    {
        var rom = Roms();
        var media = Medias();
        var romGrant = RomGrant(rom);
        var mediaGrant = MediaGrant(media);
        Assert.AreEqual(RsAOperation.RomsInventory, romGrant.Operation);
        Assert.AreEqual(MiAOperation.MediaIndex, mediaGrant.Operation);

        // Invoke the REAL Scan method through reflection with the wrong token type.
        // CLR dispatch must reject the argument BEFORE the filesystem reader runs.
        var romScan = typeof(RsAFixtureRomSession).GetMethod("Scan",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        var mediaScan = typeof(MiAFixtureMediaSession).GetMethod("Scan",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.IsNotNull(romScan);
        Assert.IsNotNull(mediaScan);
        Assert.AreEqual(typeof(RsAReadGrant), romScan.GetParameters()[0].ParameterType);
        Assert.AreEqual(typeof(MiAReadGrant), mediaScan.GetParameters()[0].ParameterType);
        Assert.IsFalse(typeof(RsAReadGrant).IsAssignableFrom(typeof(MiAReadGrant)));
        Assert.IsFalse(typeof(MiAReadGrant).IsAssignableFrom(typeof(RsAReadGrant)));

        Assert.ThrowsExactly<ArgumentException>(() => romScan.Invoke(rom,
            new object?[] { mediaGrant, CancellationToken.None, null }));
        Assert.ThrowsExactly<ArgumentException>(() => mediaScan.Invoke(media,
            new object?[] { romGrant, CancellationToken.None, null, null }));
        Assert.IsNull(rom.LastAttempt);
        Assert.IsNull(media.LastAttempt);
        Assert.AreEqual(0, media.LastHeaderBytesRead);

        // Positive controls prove that the same *real* scanners do read their own
        // explicitly authorized generated files, rather than always returning FAIL.
        var romGood = rom.Scan(romGrant);
        var mediaGood = media.Scan(mediaGrant);
        Assert.AreEqual(InventoryScanStatus.Complete, romGood.ScanStatus);
        Assert.AreEqual(InventoryScanStatus.Complete, mediaGood.ScanStatus);
        Assert.AreEqual(1, romGood.ObservedFileCount);
        Assert.AreEqual(1, mediaGood.ObservedFileCount);
        Assert.IsTrue(media.LastHeaderBytesRead > 0);

        // Correct TYPE, wrong issuing session: runtime instance identity must
        // still reject the token, preserving both last known complete results.
        var otherRom = Roms();
        var otherMedia = Medias();
        RomDenied(rom.Scan(RomGrant(otherRom)));
        MediaDenied(media.Scan(MediaGrant(otherMedia)));
        Assert.AreSame(romGood, rom.Current);
        Assert.AreSame(mediaGood, media.Current);
        Console.WriteLine("PERM_F01_PASS CLR_CROSS_TYPE_REJECTED OWN_GRANTS_READ_FIXTURE FOREIGN_SESSION_DENIED");
    }

    [TestMethod]
    public async Task PERM_F02_PublicPreviewDoesNotInheritMediaOrRomConsent()
    {
        var rom = Roms();
        var media = Medias();
        var romGrant = RomGrant(rom);
        var mediaGrant = MediaGrant(media);
        Assert.AreEqual(InventoryScanStatus.Complete, rom.Scan(romGrant).ScanStatus);
        var mediaRun = media.Scan(mediaGrant);
        Assert.AreEqual(InventoryScanStatus.Complete, mediaRun.ScanStatus);
        Assert.AreEqual(1, mediaRun.ObservedFileCount);

        // Actual production preview constructor, not a mock and not the internal
        // fixture-only constructor. A real MI-A run/observation grants no preview.
        var observation = mediaRun.Observations.Single();
        var request = new MediaPreviewRequest(mediaRun.RunId,
            mediaRun.MediaRootRef, observation.MediaObservationId, 128);
        var preview = new MediaPreviewService();
        using var denied = await preview.LoadAsync(request);
        Assert.AreEqual(MediaPreviewStatus.Rejected, denied.Status);
        Assert.AreEqual("REAL_MEDIA_PREVIEW_HOLD", denied.DiagnosticCode);
        Assert.IsNull(denied.Image);
        Assert.AreEqual(0L, denied.BufferBytesUpperBound);
        using var forged = await preview.LoadAsync(request with { MediaObservationId = "forged" });
        Assert.AreEqual(MediaPreviewStatus.Rejected, forged.Status);
        Assert.AreEqual("REAL_MEDIA_PREVIEW_HOLD", forged.DiagnosticCode);

        var publicMedia = new MediaIndexApplicationService().ScanConfirmed(
            new MediaRootScope(_medias), explicitConsent: true);
        MediaDeniedPublic(publicMedia);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());
        Console.WriteLine("PERM_F02_PASS REAL_MEDIA_RUN_NOT_PREVIEW_PERMISSION PUBLIC_PREVIEW_REFUSED");
    }

    private static void MediaDeniedPublic(MediaIndexSnapshot snapshot)
    {
        Assert.AreEqual(InventoryScanStatus.Failed, snapshot.ScanStatus);
        Assert.AreEqual(0, snapshot.ObservedFileCount);
        Assert.IsTrue(snapshot.Diagnostics.Any(d => d.Code == "REAL_MEDIA_SCAN_HOLD"));
    }

    [TestMethod]
    public async Task PERM_F03_NoConsentExpiredRevokedOrReselectedGrantCanStart()
    {
        var rom = Roms();
        var media = Medias();
        RomDenied(rom.Scan(null));
        MediaDenied(media.Scan(null));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            rom.AuthorizeFixture(false, DateTimeOffset.UtcNow.AddMinutes(2)));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            media.AuthorizeFixture(false, DateTimeOffset.UtcNow.AddMinutes(2)));

        var expRom = RomGrant(rom, expiresSeconds: 3);
        var expMedia = MediaGrant(media, expiresSeconds: 3);
        Assert.IsTrue(expRom.Valid(_romId, rom.SelectionVersion));
        Assert.IsTrue(expMedia.Valid(_mediaId, media.SelectionVersion, expMedia.Generation));
        Assert.IsTrue(SpinWait.SpinUntil(() =>
            !expRom.Valid(_romId, rom.SelectionVersion) &&
            !expMedia.Valid(_mediaId, media.SelectionVersion, expMedia.Generation),
            TimeSpan.FromSeconds(8)), "G1_EXPIRATION_TIMEOUT");
        RomDenied(rom.Scan(expRom));
        MediaDenied(media.Scan(expMedia));

        var revokeRom = RomGrant(rom);
        var revokeMedia = MediaGrant(media);
        rom.Revoke();
        media.Revoke();
        RomDenied(rom.Scan(revokeRom));
        MediaDenied(media.Scan(revokeMedia));
        var selectRom = RomGrant(rom);
        var selectMedia = MediaGrant(media);
        rom.SelectFixture(_roms, _romId);
        media.SelectFixture(_medias, _mediaId);
        RomDenied(rom.Scan(selectRom));
        MediaDenied(media.Scan(selectMedia));

        // Exercise actual MI-B orchestration and one-shot permission semantics.
        await using var application = new MiBFixtureMediaApplicationService();
        var noSelection = await application.StartConfirmedAsync(0);
        Assert.AreEqual("MI_B_SELECTION_REQUIRED", noSelection.SafeNoticeCode);
        var selected = application.SelectFixtureRoot(_medias);
        Assert.IsTrue(selected.Accepted);
        Assert.IsFalse(application.ConfirmMediaIndexConsent(selected.SelectionVersion, false));
        var noConsent = await application.StartConfirmedAsync(selected.SelectionVersion);
        Assert.AreEqual("MI_B_CONSENT_REQUIRED", noConsent.SafeNoticeCode);
        Assert.IsNull(noConsent.LastAttempt);
        Assert.IsTrue(application.ConfirmMediaIndexConsent(selected.SelectionVersion, true));
        var changed = application.SelectFixtureRoot(_medias);
        Assert.IsFalse(application.ConfirmMediaIndexConsent(selected.SelectionVersion, true));
        var stale = await application.StartConfirmedAsync(selected.SelectionVersion);
        Assert.AreEqual("MI_B_STALE_SELECTION", stale.SafeNoticeCode);
        Assert.IsNull(stale.LastAttempt);
        Assert.IsTrue(application.ConfirmMediaIndexConsent(changed.SelectionVersion, true));
        var complete = await application.StartConfirmedAsync(changed.SelectionVersion);
        Assert.AreEqual(InventoryScanStatus.Complete, complete.LastAttempt!.ScanStatus);
        var replay = await application.StartConfirmedAsync(changed.SelectionVersion);
        Assert.AreEqual("MI_B_CONSENT_REQUIRED", replay.SafeNoticeCode);
        Assert.AreEqual(complete.CurrentComplete!.RunRef, replay.CurrentComplete!.RunRef);
        Console.WriteLine("PERM_F03_PASS NULL_FALSE_EXPIRED_REVOKED_RESELECTED_STALE_AND_ONE_SHOT");
    }

    [TestMethod]
    public async Task PERM_F04_SqliteUiAndProductCliCannotRestoreOrForgeAReadGrant()
    {
        var rom = Roms();
        var media = Medias();
        var romSnapshot = rom.Scan(RomGrant(rom));
        var mediaSnapshot = media.Scan(MediaGrant(media));
        Assert.AreEqual(InventoryScanStatus.Complete, romSnapshot.ScanStatus);
        Assert.AreEqual(InventoryScanStatus.Complete, mediaSnapshot.ScanStatus);

        using (var store = new SqliteCatalogStore(_database))
        {
            store.AddLibrary(new CatalogLibrary(_libraryId, "G1 generated fixture"));
            store.AddStorageRoot(new CatalogStorageRoot(_romId, _libraryId, "ROMS", "ROMS"));
            store.AddStorageRoot(new CatalogStorageRoot(_mediaId, _libraryId, "MEDIAS", "MEDIAS"));
            store.SaveAttempt(_romId, romSnapshot);
            store.SaveAttempt(_mediaId, mediaSnapshot);
        }
        using (var reopened = new SqliteCatalogStore(_database))
        {
            var lastRoms = reopened.LoadLastKnown(_romId);
            var lastMedia = reopened.LoadLastKnown(_mediaId);
            Assert.IsNotNull(lastRoms.CurrentComplete?.Inventory);
            Assert.IsNotNull(lastMedia.CurrentComplete?.Media);
            Assert.AreEqual(InventoryAvailability.Offline,
                lastRoms.CurrentComplete.DisplayAvailability);
            Assert.AreEqual(InventoryFreshness.Stale,
                lastMedia.CurrentComplete.DisplayFreshness);
            Assert.AreEqual("ROMS", lastRoms.StorageRoot.RelativeBinding);
            Assert.AreEqual("MEDIAS", lastMedia.StorageRoot.RelativeBinding);
        }

        // The restored IDs/paths are facts, never an access capability.
        var romReopened = Roms();
        var mediaReopened = Medias();
        RomDenied(romReopened.Scan(null));
        MediaDenied(mediaReopened.Scan(null));
        Assert.IsNull(romReopened.Current);
        Assert.IsNull(mediaReopened.Current);
        Assert.ThrowsExactly<InvalidOperationException>(() => new SqliteCatalogStore());
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());

        var publicMedia = new MediaIndexApplicationService().ScanConfirmed(
            new MediaRootScope(_medias), explicitConsent: true);
        MediaDeniedPublic(publicMedia);
        using (var noPreview = await new MediaPreviewService().LoadAsync(
            new MediaPreviewRequest("restored", "restored", "restored", 128)))
        {
            Assert.AreEqual("REAL_MEDIA_PREVIEW_HOLD", noPreview.DiagnosticCode);
            Assert.AreEqual(MediaPreviewStatus.Rejected, noPreview.Status);
        }

        // Actual UI ViewModel + actual MI-B backend: selecting never creates consent.
        var realService = new MiBFixtureMediaApplicationService();
        await using (var viewModel = new MiBUiViewModel(realService, _medias, callback => callback()))
        {
            Assert.IsFalse(viewModel.CanStart);
            Assert.IsTrue(viewModel.Select());
            Assert.IsFalse(viewModel.ConsentConfirmed);
            Assert.IsFalse(viewModel.CanStart);
            Assert.IsNull(viewModel.State.LastAttempt);
            await viewModel.StartAsync();
            Assert.IsNull(viewModel.State.LastAttempt);
            Assert.IsNull(viewModel.State.CurrentComplete);
        }

        // Exercise the real product's normal executable entry, not a test double:
        // unauthorized CLI switches accompany its existing synthetic --smoke-window.
        // The app must exit without accepting an access/restore switch; startup is
        // a generated demonstration only, not a personal catalogue or SSD.
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add(typeof(RomothequeManager.V2.Program).Assembly.Location);
        foreach (var arg in new[] { "--smoke-window", "--real-library-scan",
            "--real-media-scan", "--real-media-preview", "--restore-grants" })
            start.ArgumentList.Add(arg);
        using var child = Process.Start(start) ??
            throw new InvalidOperationException("G1_PRODUCT_CLI_CHILD_START_FAILED");
        if (!child.WaitForExit(45000))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail("G1_PRODUCT_CLI_TIMEOUT");
        }
        var stdout = child.StandardOutput.ReadToEnd();
        var stderr = child.StandardError.ReadToEnd();
        Assert.AreEqual(0, child.ExitCode, stdout + "\n" + stderr);
        StringAssert.Contains(stdout, "POC_WINDOW_OPENED_PASS");
        Console.WriteLine("PERM_F04_PASS SQLITE_REOPEN_OFFLINE_STALE_UI_NO_CONSENT_PRODUCT_CLI_FLAGS_IGNORED");
    }
}
