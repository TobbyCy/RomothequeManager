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
        var romForeignOpenSeam = 0;
        var mediaForeignOpenSeam = 0;
        var mediaForeignReadSeam = 0;
        RomDenied(rom.Scan(RomGrant(otherRom), beforeChildOpenForTest: _ =>
            Interlocked.Increment(ref romForeignOpenSeam)));
        MediaDenied(media.Scan(MediaGrant(otherMedia),
            beforeChildOpenForTest: _ => Interlocked.Increment(ref mediaForeignOpenSeam),
            afterHeaderReadForTest: n => Interlocked.Add(ref mediaForeignReadSeam, n)));
        Assert.AreEqual(0, romForeignOpenSeam,
            "PERM_F01_FOREIGN_RSA_ENTERED_CHILD_OPEN_SEAM");
        Assert.AreEqual(0, mediaForeignOpenSeam,
            "PERM_F01_FOREIGN_MIA_ENTERED_CHILD_OPEN_SEAM");
        Assert.AreEqual(0, mediaForeignReadSeam,
            "PERM_F01_FOREIGN_MIA_READ_HEADER");
        Assert.AreEqual(0L, media.LastHeaderBytesRead,
            "PERM_F01_FOREIGN_MIA_ACCOUNTED_HEADER_READ");
        Console.WriteLine("PERM_F01_RUNTIME_FOREIGN_SESSION_REFUSED child_open_callbacks=0 header_read_callbacks=0");
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
        // A production-constructed Preview returns its HOLD even when a REAL
        // fixture file is exclusively locked by the test. This is an observable
        // fail-closed public result, NOT syscall-level evidence of zero stat/open.
        using (var lockedFixture = new FileStream(Path.Combine(_medias, "gba", "fixture.png"),
            FileMode.Open, FileAccess.Read, FileShare.None))
        using (var duringLock = await preview.LoadAsync(request))
        {
            Assert.AreEqual(MediaPreviewStatus.Rejected, duringLock.Status);
            Assert.AreEqual("REAL_MEDIA_PREVIEW_HOLD", duringLock.DiagnosticCode);
            Assert.IsNull(duringLock.Image);
            Assert.AreEqual(0L, duringLock.BufferBytesUpperBound);
        }
        Console.WriteLine("PERM_F02_PUBLIC_PREVIEW_FAIL_CLOSED_WITH_EXCLUSIVE_FILE_LOCK " +
            "native_stat_open_trace=NOT_EXECUTED");
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
        // Run the app from ITS OWN build output. MSTest's copied assembly
        // directory carries a different native SkiaSharp dependency and cannot
        // be treated as a product runtime (CI #41229115352 demonstrated this).
        string? productDll = null;
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            for (var directory = new DirectoryInfo(startDirectory); directory is not null;
                 directory = directory.Parent)
            {
                var candidate = Path.Combine(directory.FullName, "src", "RomothequeManager.V2",
                    "bin", "Release", "net10.0", "win-x64", "RomothequeManager.V2.dll");
                if (File.Exists(candidate)) { productDll = candidate; break; }
            }
            if (productDll is not null) break;
        }
        Assert.IsNotNull(productDll, "G1_PRODUCT_BUILD_OUTPUT_NOT_FOUND");
        start.ArgumentList.Add(productDll);
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

    // Gate G1 P1-ONE-SHOT: an explicit negative oracle, intentionally FAILS if
    // a live RS-A grant can be replayed without revoke/reselect/reconsent.
    // The expected failure is a security finding, never a green canary.
    [TestMethod]
    public void PERM_G1_REPLAY_RSA_SecondDirectScanWithSameLiveGrantMustBeDenied()
    {
        var session = Roms();
        var grant = RomGrant(session, expiresSeconds: 120);

        var first = session.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, first.ScanStatus,
            "G1_PRECONDITION_RSA_FIRST_SCAN_NOT_COMPLETE");
        Assert.AreEqual(1, first.ObservedFileCount,
            "G1_PRECONDITION_RSA_FIXTURE_NOT_OBSERVED");
        Assert.IsTrue(grant.Valid(_romId, session.SelectionVersion),
            "G1_PRECONDITION_RSA_GRANT_NOT_LIVE_BEFORE_REPLAY");

        // EXACT SAME object, SAME session, SAME root, SAME selection,
        // no Revoke/AuthorizeFixture/SelectFixture between calls.
        var replay = session.Scan(grant);
        var replayAccepted = replay.ScanStatus == InventoryScanStatus.Complete;
        Console.WriteLine("G1_RSA_DIRECT_GRANT_REPLAY " +
            "first=" + first.ScanStatus + " second=" + replay.ScanStatus +
            " secondObservations=" + replay.ObservedFileCount +
            " sameGrant=true sameSession=true revokedBetween=false");
        if (replayAccepted)
            Assert.Fail("P1_G1_RSA_ONE_SHOT_BYPASS: replayed same live grant performed " +
                replay.ObservedFileCount + " synthetic metadata observation(s) " +
                "without renewed consent/revocation. STOP: owner GO required for permission fix.");

        RomDenied(replay);
        Assert.AreSame(first, session.Current,
            "Denied replay must preserve the previous CurrentComplete.");
        Console.WriteLine("G1_RSA_DIRECT_GRANT_REPLAY_REFUSED");
    }

    // Gate G1 P1-ONE-SHOT: actual MI-A header bytes read by the SAME token
    // are the causal oracle. A second Complete is a red security regression.
    [TestMethod]
    public void PERM_G1_REPLAY_MIA_SecondDirectScanWithSameLiveGrantMustBeDenied()
    {
        var session = Medias();
        var grant = MediaGrant(session, expiresSeconds: 120);

        var first = session.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, first.ScanStatus,
            "G1_PRECONDITION_MIA_FIRST_SCAN_NOT_COMPLETE");
        Assert.AreEqual(1, first.ObservedFileCount,
            "G1_PRECONDITION_MIA_FIXTURE_NOT_OBSERVED");
        var firstBytes = session.LastHeaderBytesRead;
        Assert.IsTrue(firstBytes > 0, "G1_PRECONDITION_MIA_NO_HEADER_BYTES");
        Assert.IsTrue(grant.Valid(_mediaId, session.SelectionVersion, grant.Generation),
            "G1_PRECONDITION_MIA_GRANT_NOT_LIVE_BEFORE_REPLAY");

        var replay = session.Scan(grant);
        var replayBytes = session.LastHeaderBytesRead;
        var replayAccepted = replay.ScanStatus == InventoryScanStatus.Complete;
        Console.WriteLine("G1_MIA_DIRECT_GRANT_REPLAY " +
            "first=" + first.ScanStatus + " second=" + replay.ScanStatus +
            " firstHeaderBytes=" + firstBytes +
            " secondHeaderBytes=" + replayBytes +
            " secondObservations=" + replay.ObservedFileCount +
            " sameGrant=true sameSession=true revokedBetween=false");
        if (replayAccepted)
            Assert.Fail("P1_G1_MIA_ONE_SHOT_BYPASS: replayed same live grant read " +
                replayBytes + " synthetic header bytes without renewed consent/revocation. " +
                "STOP: owner GO required for permission fix.");

        MediaDenied(replay);
        Assert.AreEqual(0L, replayBytes, "G1_MIA_READ_AFTER_DENIED_REPLAY");
        Assert.AreSame(first, session.Current,
            "Denied replay must preserve the previous CurrentComplete.");
        Console.WriteLine("G1_MIA_DIRECT_GRANT_REPLAY_REFUSED");
    }

    // One task has claimed the token and is deliberately stopped immediately
    // before the native child open. Another concurrent Scan must fail BEFORE
    // its own native enumeration/callback. Both calls use EXACTLY the same grant.
    [TestMethod]
    public async Task PERM_G1_CONCURRENT_RSA_ClaimIsAtomicAndSecondHasNoOpens()
    {
        var session = Roms();
        var grant = RomGrant(session);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var rejectedOpens = 0;
        var firstTask = Task.Run(() => session.Scan(grant, beforeChildOpenForTest: relative =>
        {
            if (relative != "gba/fixture.gba") return;
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("G1_RSA_FIRST_SCANNER_WAIT_EXPIRED");
        }));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
                "G1_RSA_FIRST_SCAN_DID_NOT_REACH_NATIVE_OPEN_SEAM");
            var replay = session.Scan(grant, beforeChildOpenForTest: _ =>
                Interlocked.Increment(ref rejectedOpens));
            RomDenied(replay);
            Assert.AreEqual(0, Volatile.Read(ref rejectedOpens),
                "G1_RSA_DENIED_CONCURRENT_SCAN_ENTERED_NATIVE_CHILD_OPEN");
            Assert.AreEqual(0, replay.ObservedFileCount);
            Console.WriteLine("G1_RSA_CONCURRENT_REPLAY_REFUSED before_child_open_calls=0 observations=0");
        }
        finally { resume.Set(); }
        var first = await firstTask;
        Assert.AreEqual(InventoryScanStatus.Complete, first.ScanStatus);
        Assert.AreEqual(1, first.ObservedFileCount);
        Assert.AreSame(first, session.Current);
        Console.WriteLine("G1_RSA_ATOMIC_CLAIM_FIRST_SCAN_COMPLETED");
    }

    [TestMethod]
    public async Task PERM_G1_CONCURRENT_MIA_ClaimIsAtomicAndSecondReadsZeroHeaders()
    {
        var session = Medias();
        var grant = MediaGrant(session);
        using var entered = new ManualResetEventSlim();
        using var resume = new ManualResetEventSlim();
        var rejectedOpens = 0;
        var rejectedHeaderBytes = 0;
        var firstTask = Task.Run(() => session.Scan(grant, beforeChildOpenForTest: relative =>
        {
            if (relative != "gba/fixture.png") return;
            entered.Set();
            if (!resume.Wait(TimeSpan.FromSeconds(15)))
                throw new TimeoutException("G1_MIA_FIRST_SCANNER_WAIT_EXPIRED");
        }));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)),
                "G1_MIA_FIRST_SCAN_DID_NOT_REACH_NATIVE_OPEN_SEAM");
            var replay = session.Scan(grant,
                beforeChildOpenForTest: _ => Interlocked.Increment(ref rejectedOpens),
                afterHeaderReadForTest: bytes => Interlocked.Add(ref rejectedHeaderBytes, bytes));
            MediaDenied(replay);
            Assert.AreEqual(0, Volatile.Read(ref rejectedOpens),
                "G1_MIA_DENIED_CONCURRENT_SCAN_ENTERED_NATIVE_CHILD_OPEN");
            Assert.AreEqual(0, Volatile.Read(ref rejectedHeaderBytes),
                "G1_MIA_DENIED_CONCURRENT_SCAN_READ_HEADER");
            Assert.AreEqual(0L, session.LastHeaderBytesRead,
                "G1_MIA_DENIED_CONCURRENT_SCAN_USED_READ_BUDGET");
            Console.WriteLine("G1_MIA_CONCURRENT_REPLAY_REFUSED child_opens=0 header_bytes=0");
        }
        finally { resume.Set(); }
        var first = await firstTask;
        Assert.AreEqual(InventoryScanStatus.Complete, first.ScanStatus);
        Assert.AreEqual(1, first.ObservedFileCount);
        Assert.IsTrue(session.LastHeaderBytesRead > 0);
        Assert.AreSame(first, session.Current);
        Console.WriteLine("G1_MIA_ATOMIC_CLAIM_FIRST_SCAN_COMPLETED");
    }

    // New, deliberately isolated G1 gap: replay AFTER a consumed grant's first
    // attempt was Partial, Failed or Cancelled. Current was established with a
    // DIFFERENT previously consented grant. The second attempt must not even
    // reach the child-open callback; this is not a native syscall trace.
    [TestMethod]
    public void PERM_G1_REPLAY_RSA_AfterPartialFailedCancelMustNotReadOrReplaceCurrent()
    {
        foreach (var scenario in new[] { "Partial", "Failed", "Cancel" })
        {
            var session = new RsAFixtureRomSession(
                scenario == "Partial" ? new RsABudget(MaxFiles: 1) : null);
            session.SelectFixture(_roms, _romId);
            var complete = session.Scan(RomGrant(session));
            Assert.AreEqual(InventoryScanStatus.Complete, complete.ScanStatus, scenario);
            var grant = RomGrant(session); // New consent for the actual attempt.
            var extra = Path.Combine(_roms, "gba", "extra.gba");
            using var cancellation = new CancellationTokenSource();
            if (scenario == "Partial")
                File.WriteAllBytes(extra, "SYNTHETIC_SECOND_FILE"u8.ToArray());
            try
            {
                var first = scenario switch
                {
                    "Partial" => session.Scan(grant),
                    "Failed" => session.Scan(grant, beforeChildOpenForTest: _ =>
                        throw new UnauthorizedAccessException("G1_SYNTHETIC_DENIAL_AFTER_CLAIM")),
                    _ => session.Scan(grant, cancellation.Token,
                        beforeChildOpenForTest: _ => cancellation.Cancel())
                };
                Assert.AreEqual(scenario == "Partial" ? InventoryScanStatus.Partial :
                    InventoryScanStatus.Failed, first.ScanStatus, scenario);
                if (scenario == "Cancel")
                    Assert.IsTrue(first.Diagnostics.Any(x => x.Code == "RS_A_CANCELLED"));
                if (scenario == "Failed")
                    Assert.IsTrue(first.Diagnostics.Any(x => x.Code == "RS_A_DENIED"));
                Assert.IsTrue(grant.Valid(_romId, session.SelectionVersion),
                    "G1_RSA_GRANT_EXPIRED_OR_REVOKED_NOT_A_ONE_SHOT_PROOF");
                Assert.AreSame(complete, session.Current,
                    "G1_RSA_NONCOMPLETE_REPLACED_CURRENT");

                var reachedChildOpen = 0;
                var replay = session.Scan(grant, beforeChildOpenForTest: _ =>
                    Interlocked.Increment(ref reachedChildOpen));
                RomDenied(replay);
                Assert.AreEqual(0, reachedChildOpen, scenario);
                Assert.AreSame(complete, session.Current, scenario);
                Assert.AreSame(replay, session.LastAttempt, scenario);
                Assert.AreEqual(0, replay.ObservedFileCount, scenario);
                Console.WriteLine("G1_RSA_NONCOMPLETE_REPLAY_DENIED scenario=" + scenario +
                    " first=" + first.ScanStatus + " second=" + replay.ScanStatus +
                    " child_open_callbacks=0 observations=0 current_preserved=true " +
                    "native_syscall_trace=NOT_EXECUTED");
            }
            finally
            {
                if (File.Exists(extra)) File.Delete(extra);
            }
        }
    }

    [TestMethod]
    public void PERM_G1_REPLAY_MIA_AfterPartialFailedCancelMustReadZeroHeaders()
    {
        foreach (var scenario in new[] { "Partial", "Failed", "Cancel" })
        {
            var session = new MiAFixtureMediaSession(
                scenario == "Partial" ? new MiABudget(MaxFiles: 1) : null);
            session.SelectFixture(_medias, _mediaId);
            var complete = session.Scan(MediaGrant(session));
            Assert.AreEqual(InventoryScanStatus.Complete, complete.ScanStatus, scenario);
            Assert.IsTrue(session.LastHeaderBytesRead > 0);
            var grant = MediaGrant(session); // Fresh grant for the next run.
            var extra = Path.Combine(_medias, "gba", "extra.png");
            using var cancellation = new CancellationTokenSource();
            if (scenario == "Partial")
                File.WriteAllBytes(extra, TinyPngHeader());
            try
            {
                var first = scenario switch
                {
                    "Partial" => session.Scan(grant),
                    "Failed" => session.Scan(grant, beforeChildOpenForTest: _ =>
                        throw new UnauthorizedAccessException("G1_SYNTHETIC_DENIAL_AFTER_CLAIM")),
                    _ => session.Scan(grant, cancellation.Token,
                        beforeChildOpenForTest: _ => cancellation.Cancel())
                };
                Assert.AreEqual(scenario == "Failed" ? InventoryScanStatus.Failed :
                    InventoryScanStatus.Partial, first.ScanStatus, scenario);
                if (scenario == "Cancel")
                    Assert.IsTrue(first.Diagnostics.Any(x => x.Code == "MI_A_CANCELLED"));
                if (scenario == "Failed")
                    Assert.IsTrue(first.Diagnostics.Any(x => x.Code == "MI_A_DENIED"));
                Assert.IsTrue(grant.Valid(_mediaId, session.SelectionVersion, grant.Generation),
                    "G1_MIA_GRANT_EXPIRED_OR_REVOKED_NOT_A_ONE_SHOT_PROOF");
                Assert.AreSame(complete, session.Current,
                    "G1_MIA_NONCOMPLETE_REPLACED_CURRENT");

                var childOpens = 0;
                var headerReads = 0;
                var replay = session.Scan(grant,
                    beforeChildOpenForTest: _ => Interlocked.Increment(ref childOpens),
                    afterHeaderReadForTest: bytes => Interlocked.Add(ref headerReads, bytes));
                MediaDenied(replay);
                Assert.AreEqual(0, childOpens, scenario);
                Assert.AreEqual(0, headerReads, scenario);
                Assert.AreEqual(0L, session.LastHeaderBytesRead, scenario);
                Assert.AreSame(complete, session.Current, scenario);
                Assert.AreSame(replay, session.LastAttempt, scenario);
                Console.WriteLine("G1_MIA_NONCOMPLETE_REPLAY_DENIED scenario=" + scenario +
                    " first=" + first.ScanStatus + " second=" + replay.ScanStatus +
                    " child_open_callbacks=0 header_read_callbacks=0 accounted_header_bytes=0 " +
                    "current_preserved=true native_syscall_trace=NOT_EXECUTED");
            }
            finally
            {
                if (File.Exists(extra)) File.Delete(extra);
            }
        }
    }

    [TestMethod]
    public void PERM_F04_BackupRestoreVerifiedThenSeparateProcessReallyLoadsWithoutGrant()
    {
        // Generated ROMS/MEDIAS snapshots from the production fixture scanners.
        var rom = Roms();
        var media = Medias();
        var romRun = rom.Scan(RomGrant(rom));
        var mediaRun = media.Scan(MediaGrant(media));
        Assert.AreEqual(InventoryScanStatus.Complete, romRun.ScanStatus);
        Assert.AreEqual(InventoryScanStatus.Complete, mediaRun.ScanStatus);
        string backup;
        using (var store = new SqliteCatalogStore(_database))
        {
            store.AddLibrary(new CatalogLibrary(_libraryId, "G1 restored test library"));
            store.AddStorageRoot(new CatalogStorageRoot(_romId, _libraryId, "ROMS", "ROMS"));
            store.AddStorageRoot(new CatalogStorageRoot(_mediaId, _libraryId, "MEDIAS", "MEDIAS"));
            store.SaveAttempt(_romId, romRun);
            store.SaveAttempt(_mediaId, mediaRun);
            backup = store.BackupVerified();
            Assert.IsTrue(File.Exists(backup), "G1_BACKUP_FILE_NOT_CREATED");
            store.SaveManualDecision(new CatalogUserDecision(
                Guid.NewGuid().ToString("D"), _libraryId, "roms:gba/fixture.gba",
                "MANUAL_COVER_CHOICE", "medias:gba/fixture.png", DateTimeOffset.UtcNow), true);
            Assert.AreEqual(1, store.LoadLastKnown(_romId).Decisions.Count,
                "G1_BACKUP_POSITIVE_MUTATION_NOT_VISIBLE");
            store.RestoreVerified(backup);
            var restored = store.LoadLastKnown(_romId);
            Assert.AreEqual(0, restored.Decisions.Count,
                "G1_RESTORE_VERIFIED_DID_NOT_RESTORE_PRE_DECISION_STATE");
            Assert.AreEqual(romRun.ScanId, restored.CurrentComplete!.Inventory!.ScanId);
            Assert.AreEqual(mediaRun.RunId,
                store.LoadLastKnown(_mediaId).CurrentComplete!.Media!.RunId);
        }

        // A NEW OS process (not a second in-process SqliteCatalogStore instance)
        // is sent only the fixture DB, durable root IDs and expected saved run IDs.
        // It is NOT supplied either physical source root or a consent/grant.
        var childProof = Path.Combine(_dbFixture, "g1-restored-child-proof.txt");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(G1PermissionBoundaryFixtureTests).Assembly.Location);
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~PERM_F04_ChildProcessOpenRestoredBackupNoGrant");
        start.ArgumentList.Add("/Logger:console;verbosity=minimal");
        start.Environment["G1_RESTORED_CHILD_DB"] = _database;
        start.Environment["G1_RESTORED_CHILD_ROM_ID"] = _romId;
        start.Environment["G1_RESTORED_CHILD_MEDIA_ID"] = _mediaId;
        start.Environment["G1_RESTORED_CHILD_ROM_RUN"] = romRun.ScanId;
        start.Environment["G1_RESTORED_CHILD_MEDIA_RUN"] = mediaRun.RunId;
        start.Environment["G1_RESTORED_CHILD_PROOF"] = childProof;
        start.Environment["G1_RESTORED_CHILD_PARENT_PID"] = Environment.ProcessId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        using var child = Process.Start(start) ??
            throw new InvalidOperationException("G1_RESTORED_CHILD_START_FAILED");
        var stdout = child.StandardOutput.ReadToEnd();
        var stderr = child.StandardError.ReadToEnd();
        if (!child.WaitForExit(90000))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail("G1_RESTORED_CHILD_TIMEOUT");
        }
        Assert.AreEqual(0, child.ExitCode, stdout + "\n" + stderr);
        StringAssert.Contains(stdout, "Passed");
        Assert.IsTrue(File.Exists(childProof),
            "G1_RESTORED_CHILD_NO_COMPLETION_PROOF");
        var proof = File.ReadAllText(childProof);
        StringAssert.Contains(proof, "G1_RESTORED_BACKUP_CHILD_PASS");
        StringAssert.Contains(proof, romRun.ScanId);
        StringAssert.Contains(proof, mediaRun.RunId);
        Console.WriteLine("G1_RESTORED_BACKUP_SECOND_PROCESS_PASS " +
            "actual_sqlite_reopened=true offline_stale=true child_scan_without_grant_denied=true " +
            "implicit_source_open=NOT_EXECUTED_NATIVELY");
    }

    // Standalone vstest child method. With no child-only environment it is
    // deliberately inert when enumerated by the normal parent test suite.
    [TestMethod]
    public void PERM_F04_ChildProcessOpenRestoredBackupNoGrant()
    {
        var path = Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_DB");
        if (path is null) return;
        Assert.IsTrue(Environment.ProcessId != int.Parse(
            Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_PARENT_PID")!,
            System.Globalization.CultureInfo.InvariantCulture),
            "G1_SQLITE_CHILD_IS_NOT_SEPARATE_PROCESS");

        using var store = new SqliteCatalogStore(path);
        var romId = Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_ROM_ID")!;
        var mediaId = Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_MEDIA_ID")!;
        var expectedRom = Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_ROM_RUN")!;
        var expectedMedia = Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_MEDIA_RUN")!;
        var rom = store.LoadLastKnown(romId);
        var media = store.LoadLastKnown(mediaId);
        Assert.IsNotNull(rom.CurrentComplete?.Inventory);
        Assert.IsNotNull(media.CurrentComplete?.Media);
        Assert.AreEqual(expectedRom, rom.CurrentComplete.Inventory.ScanId);
        Assert.AreEqual(expectedMedia, media.CurrentComplete.Media.RunId);
        Assert.AreEqual(InventoryAvailability.Offline, rom.CurrentComplete.DisplayAvailability);
        Assert.AreEqual(InventoryFreshness.Stale, rom.CurrentComplete.DisplayFreshness);
        Assert.AreEqual(InventoryAvailability.Offline, media.CurrentComplete.DisplayAvailability);
        Assert.AreEqual(InventoryFreshness.Stale, media.CurrentComplete.DisplayFreshness);
        Assert.AreEqual(0, rom.Decisions.Count, "G1_RESTORED_DECISION_MUST_BE_ABSENT");
        Assert.AreEqual("ROMS", rom.StorageRoot.RelativeBinding);
        Assert.AreEqual("MEDIAS", media.StorageRoot.RelativeBinding);

        // The only data received from SQLite are historical facts. A fresh
        // scanner in THIS NEW process has no selection or grant to restore.
        var romScanner = new RsAFixtureRomSession();
        var mediaScanner = new MiAFixtureMediaSession();
        RomDenied(romScanner.Scan(null));
        MediaDenied(mediaScanner.Scan(null));
        Assert.IsNull(romScanner.Current);
        Assert.IsNull(mediaScanner.Current);
        Assert.AreEqual(0L, mediaScanner.LastHeaderBytesRead);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());
        Assert.ThrowsExactly<InvalidOperationException>(() => new SqliteCatalogStore());
        using var publicPreview = new MediaPreviewService().LoadAsync(
            new MediaPreviewRequest("restored", "restored", "restored", 128))
            .GetAwaiter().GetResult();
        Assert.AreEqual(MediaPreviewStatus.Rejected, publicPreview.Status);
        Assert.AreEqual("REAL_MEDIA_PREVIEW_HOLD", publicPreview.DiagnosticCode);
        Assert.IsNull(publicPreview.Image);
        var proofFile = Environment.GetEnvironmentVariable("G1_RESTORED_CHILD_PROOF")!;
        File.WriteAllText(proofFile, "G1_RESTORED_BACKUP_CHILD_PASS pid=" +
            Environment.ProcessId + " rom=" + expectedRom + " media=" + expectedMedia);
        Console.WriteLine("G1_RESTORED_BACKUP_CHILD_PASS sqlite_opened=true " +
            "roms_and_medias_no_grant=true public_holds=true");
    }
}
