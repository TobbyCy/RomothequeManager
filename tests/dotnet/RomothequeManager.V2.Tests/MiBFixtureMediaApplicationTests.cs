using System.Buffers.Binary;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Inventory.Media.MiA;
using RomothequeManager.V2.Inventory.Media.MiB;

namespace RomothequeManager.V2.Tests;

/// <summary>MI-B01..04: only uniquely generated Windows %TEMP% MEDIAS fixtures.
/// No real library, persistent permission, preview, Skia, network or source mutation.</summary>
[TestClass]
public sealed class MiBFixtureMediaApplicationTests
{
    private string _fixture = "";
    private string _root = "";

    [TestInitialize]
    public void Init()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("MI_B_WINDOWS_ONLY");
            return;
        }
        _fixture = Path.Combine(Path.GetTempPath(),
            "rm-mia-fixture-" + Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_fixture, "MEDIAS");
        Directory.CreateDirectory(_root);
        Console.WriteLine("MI_B_NATIVE_ENV OS=" + Environment.OSVersion +
            " NET=" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription +
            " FS=" + new DriveInfo(Path.GetPathRoot(_root)!).DriveFormat);
    }

    [TestCleanup]
    public void Clean()
    {
        if (_fixture.Length > 0 && Directory.Exists(_fixture))
            Directory.Delete(_fixture, recursive: true);
    }

    private static uint Crc(ReadOnlySpan<byte> data)
    {
        uint crc = 0xffffffff;
        foreach (byte x in data)
        {
            crc ^= x;
            for (var bit = 0; bit < 8; ++bit)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static byte[] Png()
    {
        var data = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(data, 0);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(data.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(20, 4), 1);
        data[24] = 8; data[25] = 6;
        BinaryPrimitives.WriteUInt32BigEndian(data.AsSpan(29, 4), Crc(data.AsSpan(12, 17)));
        return data;
    }

    private void Put(string relative, byte[]? bytes = null)
    {
        var path = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, bytes ?? Png());
    }

    [TestMethod]
    public async Task MIB_01_SelectionConsentAndOneShotStartAreIndependentAndPublicStillDenied()
    {
        Put("gb/one.png");
        await using var service = new MiBFixtureMediaApplicationService();
        Assert.AreEqual(MiBPhase.NoSelection, service.State.Phase);
        var forbidden = await service.StartConfirmedAsync(0);
        Assert.AreEqual("MI_B_SELECTION_REQUIRED", forbidden.SafeNoticeCode);
        Assert.IsNull(forbidden.LastAttempt);

        var pick = service.SelectFixtureRoot(_root);
        Assert.IsTrue(pick.Accepted);
        Assert.AreEqual(MiBPhase.Ready, service.State.Phase);
        Assert.IsNull(service.State.CurrentComplete);
        Assert.IsNull(service.State.LastAttempt);
        Assert.IsFalse(service.ConfirmMediaIndexConsent(pick.SelectionVersion, false));
        Assert.IsFalse(service.ConfirmMediaIndexConsent(pick.SelectionVersion - 1, true));

        var premature = await service.StartConfirmedAsync(pick.SelectionVersion);
        Assert.AreEqual(MiBPhase.Rejected, premature.Phase);
        Assert.AreEqual("MI_B_CONSENT_REQUIRED", premature.SafeNoticeCode);
        Assert.IsNull(premature.LastAttempt);
        Assert.IsTrue(service.ConfirmMediaIndexConsent(pick.SelectionVersion, true));
        Assert.AreEqual(MiBPhase.Ready, service.State.Phase);

        var result = await service.StartConfirmedAsync(pick.SelectionVersion);
        Assert.AreEqual(MiBPhase.Completed, result.Phase);
        Assert.AreEqual(InventoryScanStatus.Complete, result.LastAttempt!.ScanStatus);
        Assert.AreEqual(1, result.LastAttempt.ObservedMediaCount);
        Assert.AreEqual(1, result.LastAttempt.KnownObservedMediaTotal);
        Assert.IsNotNull(result.CurrentComplete);
        Assert.AreEqual(result.CurrentComplete.RunRef, result.LastAttempt.RunRef);
        Assert.IsFalse(result.ProgressIsIndeterminate);
        Assert.IsTrue(result.LastAttempt.Rows.All(r =>
            !r.DisplayLabel.Contains('/') && !r.DisplayLabel.Contains('\\')));
        Assert.IsFalse(typeof(IMiBFixtureMediaApplicationService).IsPublic);
        Assert.IsFalse(typeof(MiBFixtureMediaApplicationService).IsPublic);
        Assert.IsFalse(typeof(MiBProjectionState).IsPublic);

        var reused = await service.StartConfirmedAsync(pick.SelectionVersion);
        Assert.AreEqual("MI_B_CONSENT_REQUIRED", reused.SafeNoticeCode);
        Assert.AreEqual(result.CurrentComplete.RunRef, reused.CurrentComplete!.RunRef);
        Assert.IsTrue(service.ConfirmMediaIndexConsent(pick.SelectionVersion, true));
        var second = await service.StartConfirmedAsync(pick.SelectionVersion);
        Assert.AreEqual(InventoryScanStatus.Complete, second.LastAttempt!.ScanStatus);
        Assert.AreNotEqual(result.LastAttempt.RunRef, second.LastAttempt.RunRef);

        var publicDenied = new MediaIndexApplicationService().ScanConfirmed(
            new MediaRootScope(_root), explicitConsent: true);
        Assert.AreEqual(InventoryScanStatus.Failed, publicDenied.ScanStatus);
        Assert.IsTrue(publicDenied.Diagnostics.Any(x => x.Code == "REAL_MEDIA_SCAN_HOLD"));
        Console.WriteLine("MI_B01_PASS SELECT_CONSENT_START_DISTINCT ONE_SHOT PUBLIC_FAIL_CLOSED");
    }

    [TestMethod]
    public async Task MIB_02_CancelKeepsSameRootCurrentAndLateRootAReturnCannotPolluteB()
    {
        Put("gb/a.png");
        await using var service = new MiBFixtureMediaApplicationService();
        var initial = service.SelectFixtureRoot(_root);
        Assert.IsTrue(service.ConfirmMediaIndexConsent(initial.SelectionVersion, true));
        var good = await service.StartConfirmedAsync(initial.SelectionVersion);
        Assert.AreEqual(InventoryScanStatus.Complete, good.LastAttempt!.ScanStatus);
        var refA = good.CurrentComplete!.MediaRootRef;
        Put("gb/second.png", new byte[8192]);

        service.AfterHeaderReadForTest = _ => service.RequestCancellation();
        Assert.IsTrue(service.ConfirmMediaIndexConsent(initial.SelectionVersion, true));
        var cancelled = await service.StartConfirmedAsync(initial.SelectionVersion);
        Assert.AreEqual(InventoryScanStatus.Partial, cancelled.LastAttempt!.ScanStatus);
        Assert.AreEqual("MI_B_CANCELLED", cancelled.SafeNoticeCode);
        Assert.IsTrue(cancelled.CurrentIsHistorical);
        Assert.AreEqual(good.CurrentComplete.RunRef, cancelled.CurrentComplete!.RunRef);
        Assert.AreEqual(refA, cancelled.LastAttempt.MediaRootRef);
        Assert.IsNull(cancelled.LastAttempt.KnownObservedMediaTotal);
        service.AfterHeaderReadForTest = null;

        var bFolder = Path.Combine(Path.GetTempPath(),
            "rm-mia-fixture-" + Guid.NewGuid().ToString("N"));
        var rootB = Path.Combine(bFolder, "MEDIAS");
        Directory.CreateDirectory(rootB);
        try
        {
            long versionB = -1;
            var publishedAfterB = new List<MiBProjectionState>();
            bool bSelected = false;
            service.StateChanged += (_, state) =>
            {
                if (bSelected) publishedAfterB.Add(state);
            };
            service.AfterHeaderReadForTest = _ =>
            {
                if (bSelected) return;
                var pickedB = service.SelectFixtureRoot(rootB);
                Assert.IsTrue(pickedB.Accepted);
                versionB = pickedB.SelectionVersion;
                bSelected = true;
            };
            Assert.IsTrue(service.ConfirmMediaIndexConsent(initial.SelectionVersion, true));
            var oldTask = service.StartConfirmedAsync(initial.SelectionVersion);
            var lateResult = await oldTask;
            Assert.IsTrue(bSelected);
            Assert.IsTrue(versionB > initial.SelectionVersion);
            Assert.AreEqual(MiBPhase.Ready, service.State.Phase);
            Assert.IsNull(service.State.CurrentComplete);
            Assert.IsNull(service.State.LastAttempt);
            Assert.IsNull(lateResult.CurrentComplete);
            Assert.IsTrue(publishedAfterB.All(x =>
                x.CurrentComplete is null && x.LastAttempt is null));
            Assert.IsTrue(service.ConfirmMediaIndexConsent(versionB, true));
            var completeB = await service.StartConfirmedAsync(versionB);
            Assert.AreEqual(InventoryScanStatus.Complete, completeB.LastAttempt!.ScanStatus);
            Assert.AreEqual(0, completeB.LastAttempt.ObservedMediaCount);
            Assert.AreNotEqual(refA, completeB.CurrentComplete!.MediaRootRef);
            Assert.IsFalse(service.RequestCancellation());

            // Dispose during a real header-read callback: wait for worker drain,
            // then forbid any notification or permission restoration.
            var disposing = new MiBFixtureMediaApplicationService();
            var closePick = disposing.SelectFixtureRoot(_root);
            Assert.IsTrue(disposing.ConfirmMediaIndexConsent(closePick.SelectionVersion, true));
            using var enteredRead = new ManualResetEventSlim(false);
            using var releaseRead = new ManualResetEventSlim(false);
            var notifications = 0;
            disposing.StateChanged += (_, _) => Interlocked.Increment(ref notifications);
            disposing.AfterHeaderReadForTest = _ =>
            {
                enteredRead.Set();
                releaseRead.Wait(TimeSpan.FromSeconds(10));
            };
            var active = disposing.StartConfirmedAsync(closePick.SelectionVersion);
            try
            {
                Assert.IsTrue(enteredRead.Wait(TimeSpan.FromSeconds(10)),
                    "The fixture worker must enter a native header read before Dispose.");
                var closing = disposing.DisposeAsync().AsTask();
                releaseRead.Set();
                await closing;
                var afterDispose = Volatile.Read(ref notifications);
                await active;
                Assert.AreEqual(afterDispose, Volatile.Read(ref notifications),
                    "No late StateChanged event is allowed after disposal.");
                Assert.IsFalse(disposing.ConfirmMediaIndexConsent(closePick.SelectionVersion, true));
            }
            finally
            {
                releaseRead.Set();
                await disposing.DisposeAsync();
            }
            Console.WriteLine("MI_B02_PASS CANCEL_CURRENT A_TO_B_STALE_DISCARDED DISPOSE_DRAIN_NO_LATE_EVENT");
        }
        finally
        {
            if (Directory.Exists(bFolder)) Directory.Delete(bFolder, recursive: true);
        }
    }

    [TestMethod]
    public async Task MIB_03_UnknownPartialBudgetsAndSafeImmutableProjection()
    {
        Put("gb/valid.png");
        Put("gb/invalid.jpg", new byte[64]);
        var webp = new byte[33];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));
        Put("gb/unsupported.webp", webp);
        Put("gb/ignored.txt", new byte[100]);

        await using var app = new MiBFixtureMediaApplicationService();
        var selection = app.SelectFixtureRoot(_root);
        Assert.IsTrue(app.ConfirmMediaIndexConsent(selection.SelectionVersion, true));
        var view = (await app.StartConfirmedAsync(selection.SelectionVersion)).CurrentComplete!;
        Assert.AreEqual(3, view.ObservedMediaCount);
        Assert.AreEqual(3, view.KnownObservedMediaTotal);
        Assert.IsTrue(view.Rows.Any(x => x.Format == MediaImageFormat.Png && x.HeaderValidated));
        Assert.IsTrue(view.Rows.Any(x => x.Format == MediaImageFormat.Webp &&
            !x.HeaderValidated && x.SafeDiagnosticCodes.Contains("UNSUPPORTED_FORMAT")));
        Assert.IsTrue(view.Rows.Any(x => x.DisplayLabel == "invalid.jpg" &&
            x.Format == MediaImageFormat.Unknown && !x.HeaderValidated &&
            x.SafeDiagnosticCodes.Contains("MI_A_SIGNATURE_OR_EXTENSION_MISMATCH")),
            "A .jpg extension with invalid bytes is Unknown, never a detected JPEG.");
        Assert.IsTrue(view.Rows.All(x => x.ObservationRef.StartsWith("mia-observation-")));
        Assert.IsTrue(view.Rows.All(x => x.DisplayLabel.IndexOfAny(new[] { '/', '\\' }) < 0));
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<MiBMediaRow>)view.Rows)[0] = view.Rows[1]);
        Assert.ThrowsExactly<NotSupportedException>(() =>
            ((IList<InventoryCoverage>)view.Coverage)[0] = view.Coverage[0]);

        // The projection must not leak path-bearing diagnostics or unknown codes.
        var raw = new MediaIndexSnapshot("mia-abcdef", "mia-media-abcdef",
            DateTimeOffset.UtcNow, InventoryScanStatus.Partial,
            InventoryAvailability.Unknown, InventoryFreshness.Unknown,
            new[] { new InventoryCoverage("MEDIAS", InventoryScanStatus.Partial) },
            new[] { new MediaObservation(
                "mia-observation-abc-0", "mia-media-abcdef",
                @"C:\private\notes.png", "bad/absolute", MediaKind.Unknown, ".png",
                MediaImageFormat.Unknown, false, null, null, null, null,
                DateTimeOffset.UtcNow, InventoryAvailability.Unknown, "FIxture",
                new[] { "SECRET:C:\\user\\file" }) },
            new[] { new InventoryDiagnostic("C:\\secret\\exception",
                InventorySeverity.Error, "C:\\private", "PII", "native", DateTimeOffset.UtcNow) },
            1, 999);
        var redacted = MiBMediaProjection.Project(raw);
        Assert.IsNull(redacted.KnownObservedMediaTotal);
        Assert.AreEqual("notes.png", redacted.Rows[0].DisplayLabel);
        Assert.AreEqual("UNKNOWN", redacted.Rows[0].PlatformKey);
        Assert.AreEqual("MI_B_UNCLASSIFIED_DIAGNOSTIC", redacted.SafeDiagnosticCodes[0]);
        Assert.AreEqual("MI_B_UNCLASSIFIED_DIAGNOSTIC",
            redacted.Rows[0].SafeDiagnosticCodes[0]);

        await using var limited = new MiBFixtureMediaApplicationService(new MiABudget(MaxFiles: 1));
        var pick = limited.SelectFixtureRoot(_root);
        Assert.IsTrue(limited.ConfirmMediaIndexConsent(pick.SelectionVersion, true));
        var partial = await limited.StartConfirmedAsync(pick.SelectionVersion);
        Assert.AreEqual(InventoryScanStatus.Partial, partial.LastAttempt!.ScanStatus);
        Assert.AreEqual("MI_B_PARTIAL", partial.SafeNoticeCode);
        Assert.IsNull(partial.LastAttempt.KnownObservedMediaTotal);
        Assert.IsNull(partial.CurrentComplete);
        Assert.IsFalse(partial.ProgressIsIndeterminate);
        Assert.IsTrue(partial.LastAttempt.SafeDiagnosticCodes.Contains("MI_A_FILE_BUDGET"));
        Console.WriteLine("MI_B03_PASS PNG_JPEG_WEBP HEADER_ONLY COUNTS_PARTIAL_NULL " +
            "READONLY_COLLECTIONS PATH_DIAGNOSTICS_REDACTED CAPS");
    }

    [TestMethod]
    public async Task MIB_04_HistoricalFixtureOfflineStaleWithoutRestoredPermissionOrAutoScan()
    {
        Put("gb/source.png");
        var scanner = new MiAFixtureMediaSession();
        scanner.SelectFixture(_root, Guid.NewGuid().ToString("D"));
        var grant = scanner.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(2));
        var complete = scanner.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, complete.ScanStatus);
        scanner.Revoke();

        // Simulate a later invocation with only an in-memory captured archive.
        // No SQLite MEDIAS persistence or actual new process is asserted.
        Directory.Delete(_root, recursive: true);
        var archive = MiBMediaProjection.HistoricalFixture(complete);
        Assert.IsTrue(archive.CurrentIsHistorical);
        Assert.AreEqual(MiBPhase.NoSelection, archive.Phase);
        Assert.IsNull(archive.LastAttempt);
        Assert.AreEqual(InventoryAvailability.Offline, archive.CurrentComplete!.Availability);
        Assert.AreEqual(InventoryFreshness.Stale, archive.CurrentComplete.SourceFreshness);
        Assert.IsTrue(archive.CurrentComplete.IsHistoricalFixture);
        Assert.AreEqual(1, archive.CurrentComplete.KnownObservedMediaTotal);
        Assert.AreEqual("MI_B_HISTORY_OFFLINE", archive.SafeNoticeCode);

        await using var fresh = new MiBFixtureMediaApplicationService();
        Assert.IsNull(fresh.State.CurrentComplete);
        Assert.IsFalse(fresh.ConfirmMediaIndexConsent(0, true));
        var rejected = await fresh.StartConfirmedAsync(0);
        Assert.AreEqual(MiBPhase.Rejected, rejected.Phase);
        Assert.AreEqual("MI_B_SELECTION_REQUIRED", rejected.SafeNoticeCode);
        Assert.IsNull(rejected.CurrentComplete);
        Assert.IsNull(rejected.LastAttempt);
        Assert.IsTrue(fresh.ClearSelection());
        Assert.IsNull(fresh.State.CurrentComplete);

        var failedSnapshot = complete with { ScanStatus = InventoryScanStatus.Partial,
            TotalFileCount = null };
        var invalidArchive = MiBMediaProjection.HistoricalFixture(failedSnapshot);
        Assert.IsNull(invalidArchive.CurrentComplete);
        Assert.AreEqual("MI_B_ARCHIVE_NOT_COMPLETE", invalidArchive.SafeNoticeCode);
        Console.WriteLine("MI_B04_PASS ARCHIVE_OFFLINE_STALE_IN_MEMORY_ONLY NO_GRANT_NO_AUTOSCAN " +
            "SQLITE_MEDIA_PERSISTENCE_NOT_IMPLEMENTED");
    }
}
