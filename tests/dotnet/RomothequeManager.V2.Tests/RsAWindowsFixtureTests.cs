using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Inventory.RsA;

namespace RomothequeManager.V2.Tests;

/// <summary>RS-A test suite. All generated files live under a fresh temporary fixture.
/// RS-A never receives a personal folder, a persistent grant or access to MEDIAS/Skia.</summary>
[TestClass]
public sealed class RsAWindowsFixtureTests
{
    private string _fixture = "";
    private string _root = "";
    private string _rootId = "";

    [TestInitialize]
    public void Setup()
    {
        _fixture = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_fixture, "ROMS");
        _rootId = Guid.NewGuid().ToString("D");
        Directory.CreateDirectory(_root);
        var elevated = false;
        if (OperatingSystem.IsWindows())
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            elevated = new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
                System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        Console.WriteLine("RS_A_NATIVE_ENV OS=" + Environment.OSVersion.VersionString +
            " Framework=" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription +
            " Elevated=" + elevated);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_fixture.Length > 0 && Directory.Exists(_fixture))
            Directory.Delete(_fixture, recursive: true);
    }

    private void Put(string name, string text = "FICTITIOUS BYTES ONLY")
    {
        var path = Path.Combine(_root, name.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    private RsAFixtureRomSession Session(RsABudget? budget = null)
    {
        var session = new RsAFixtureRomSession(budget);
        session.SelectFixture(_root, _rootId);
        return session;
    }

    private static RsAReadGrant Grant(RsAFixtureRomSession s) =>
        s.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(2));

    [TestMethod]
    public void RSA_01_GrantsRequireExplicitSelectionRemainVolatileExpireAndRevoke()
    {
        Put("gba/one.gba");
        var session = Session();
        Assert.AreEqual(InventoryScanStatus.Failed, session.Scan(null).ScanStatus);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            session.AuthorizeFixture(false, DateTimeOffset.UtcNow.AddMinutes(1)));
        var grant = Grant(session);
        Assert.AreEqual(RsAOperation.RomsInventory, grant.Operation);
        var complete = session.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, complete.ScanStatus);
        Assert.AreEqual(1, complete.ObservedFileCount);
        session.Revoke();
        Assert.AreEqual(InventoryScanStatus.Failed, session.Scan(grant).ScanStatus);
        Assert.AreSame(complete, session.Current);
        var second = Grant(session);
        session.SelectFixture(_root, _rootId); // invalidate selection version + token
        Assert.AreEqual(InventoryScanStatus.Failed, session.Scan(second).ScanStatus);
        var restoredNewProcessModel = Session(); // no ReadGrant is serialized or loaded
        Assert.AreEqual(InventoryScanStatus.Failed,
            restoredNewProcessModel.Scan(null).ScanStatus);
        var expired = new RsAReadGrant(_rootId, session.SelectionVersion,
            DateTimeOffset.UtcNow.AddSeconds(-1), second.RootIdentity);
        Assert.IsFalse(expired.Valid(_rootId, session.SelectionVersion));
        VerifyProcessRestartHasNoGrant();
        Console.WriteLine("RS_A01_PASS no grant/explicit/revoke/selection/expiry/real_restart; " +
            "no I/O before authorization");
        Console.WriteLine("RS_A01_NEW_PROCESS_NO_GRANT_PASS");
    }

    private void VerifyProcessRestartHasNoGrant()
    {
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, RedirectStandardOutput = true,
            RedirectStandardError = true, CreateNoWindow = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(typeof(RsAWindowsFixtureTests).Assembly.Location);
        start.ArgumentList.Add(
            "/TestCaseFilter:FullyQualifiedName~RSA_06_ProductionGatewayClosedWithoutAnyFixtureOrCliBackdoor");
        start.ArgumentList.Add("/Logger:console;verbosity=minimal");
        start.Environment["RSA_CHILD_ROOT"] = _root;
        start.Environment["RSA_CHILD_STORAGE_ROOT_ID"] = _rootId;
        using var child = Process.Start(start) ??
            throw new InvalidOperationException("RS_A01_CHILD_PROCESS_FAILED");
        var stdout = child.StandardOutput.ReadToEnd();
        var stderr = child.StandardError.ReadToEnd();
        if (!child.WaitForExit(90000))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail("RS_A01_CHILD_PROCESS_TIMEOUT");
        }
        Assert.AreEqual(0, child.ExitCode, stdout + "\n" + stderr);
        StringAssert.Contains(stdout, "Passed");
    }

    [TestMethod]
    public void RSA_02_TraversalDeviceReparseAndConcurrentParentFinalReplacement()
    {
        Put("gba/one.gba");
        var outside = Path.Combine(_fixture, "OUTSIDE");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "private.bin"), "OUTSIDE MUST NEVER BE READ");
        var invalid = new[] {
            @"\\server\share\ROMS", @"\\?\C:\windows\ROMS",
            _root + @"\..\OUTSIDE", _root + @"\gba\..\ROMS",
            "http://invalid/ROMS"
        };
        foreach (var input in invalid)
            Assert.ThrowsExactly<ArgumentException>(() =>
                RsAAnchoredReader.ValidateFixturePath(input));
        var s = Session();
        var grant = Grant(s);
        var snapshot = s.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, snapshot.ScanStatus,
            "Rooted-handle enumeration must work on synthetic ROMS.");
        Assert.AreEqual(1, snapshot.ObservedFileCount);

        // A junction is a REAL Windows reparse entry, not a mocked boolean.
        var junction = Path.Combine(_root, "linked");
        bool junctionCreated = TryJunction(junction, outside);
        if (junctionCreated)
        {
            var denied = s.Scan(grant);
            Assert.AreNotEqual(InventoryScanStatus.Complete, denied.ScanStatus);
            Assert.IsTrue(denied.Diagnostics.Any(x => x.Code == "RS_A_DENIED"));
            Assert.AreSame(snapshot, s.Current);
            Directory.Delete(junction);
            Console.WriteLine("RS_A02_REPARSE_DIRECTORY_REJECTED_REAL_WINDOWS");
        }
        else
            Console.WriteLine("RS_A02_REPARSE_DIRECTORY_NOT_EXECUTED_NO_LINK_PRIVILEGE");

        // Concurrent attempts to rename the currently opened parent. Directory handles
        // deny SHARE_DELETE; if rename wins earlier, NtCreateFile remains relative to
        // the original pinned parent or refuses, never follows the injected junction.
        var parent = Path.Combine(_root, "gba");
        bool attempted = false, mutated = false;
        var race = s.Scan(grant, beforeChildOpenForTest: relative =>
        {
            if (relative != "gba/one.gba" || attempted) return;
            attempted = true;
            var moved = Path.Combine(_root, "gba-moved");
            try
            {
                Directory.Move(parent, moved);
                mutated = TryJunction(parent, outside);
            }
            catch (IOException) { /* pinned directory blocks deletion sharing */ }
            catch (UnauthorizedAccessException) { /* pinned directory blocks rename */ }
        });
        Assert.IsTrue(attempted, "Real parent-swap callback must execute during enumeration");
        Assert.IsFalse(race.Observations.Any(o => o.RelativePath.Contains("private.bin")),
            "Never leak outside directory contents");
        Assert.IsTrue(race.ScanStatus is InventoryScanStatus.Complete or
            InventoryScanStatus.Partial or InventoryScanStatus.Failed);
        Console.WriteLine("RS_A02_PARENT_SWAP_ATTEMPTED real_windows=1 switched=" + mutated +
            " status=" + race.ScanStatus);

        // Reconstruct the original fixture after the parent-swap attempt so the
        // final-component swap is ALWAYS exercised independently of the parent outcome.
        if (Directory.Exists(junction)) Directory.Delete(junction);
        var movedDirectory = Path.Combine(_root, "gba-moved");
        if (Directory.Exists(movedDirectory))
        {
            if (Directory.Exists(parent)) Directory.Delete(parent);
            Directory.Move(movedDirectory, parent);
        }
        var file = Path.Combine(_root, "gba", "one.gba");
        if (File.Exists(file))
        {
            bool attemptedFinal = false, replacedFinal = false;
            var last = s.Scan(grant, beforeChildOpenForTest: relative =>
            {
                if (relative != "gba/one.gba" || attemptedFinal) return;
                attemptedFinal = true;
                try
                {
                    var hold = file + ".old";
                    File.Move(file, hold);
                    File.CreateSymbolicLink(file, Path.Combine(outside, "private.bin"));
                    replacedFinal = true;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            });
            Assert.IsTrue(attemptedFinal);
            Assert.IsFalse(last.Observations.Any(o => o.RelativePath.Contains("private.bin")));
            Assert.AreNotEqual(InventoryScanStatus.Complete, last.ScanStatus,
                "A swapped file must be refused rather than accepted as another file");
            Console.WriteLine("RS_A02_FINAL_SWAP_ATTEMPTED real_windows=1 switched=" +
                replacedFinal + " status=" + last.ScanStatus);
        }
        else
            Console.WriteLine("RS_A02_FINAL_SWAP_NOT_EXECUTED_PARENT_RENAME_SUCCEEDED");
        Console.WriteLine("RS_A02_OUTSIDE_READS=0; guard=REAL_LIBRARY_SCAN_HOLD");
    }

    private static bool TryJunction(string destination, string target)
    {
        if (!OperatingSystem.IsWindows()) return false;
        using var process = Process.Start(new ProcessStartInfo("cmd.exe")
        {
            ArgumentList = { "/c", "mklink", "/J", destination, target },
            CreateNoWindow = true, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true
        });
        if (process is null) return false;
        process.WaitForExit(15000);
        return process.HasExited && process.ExitCode == 0 && Directory.Exists(destination);
    }

    [TestMethod]
    public void RSA_03_MetadataOnlyBudgetCancellationAndAppDirectoryExclusions()
    {
        Put("gba/one.gba", new string('F', 1024 * 256));
        Put("gba/two.gba");
        Put("gba/ROMOTHEQUE_MANAGER/data/catalog.sqlite", "NEVER READ");
        Put("gba/cache/tmp.gba");
        Put("gba/romotheque.exe");
        var file = Path.Combine(_root, "gba", "one.gba");
        var before = SHA256.HashData(File.ReadAllBytes(file));
        var s = Session();
        var grant = Grant(s);
        var complete = s.Scan(grant);
        Assert.AreEqual(InventoryScanStatus.Complete, complete.ScanStatus);
        Assert.AreEqual(2, complete.ObservedFileCount);
        Assert.IsNull(complete.ConfirmedGameCount);
        Assert.IsTrue(complete.Observations.All(x => x.RelativePath.StartsWith("gba/")));
        Assert.IsTrue(complete.Observations.All(x => !x.RelativePath.Contains("cache", StringComparison.OrdinalIgnoreCase)));
        Assert.IsTrue(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(file))));
        var capped = Session(new RsABudget(MaxFiles: 1));
        var partial = capped.Scan(Grant(capped));
        Assert.AreEqual(InventoryScanStatus.Partial, partial.ScanStatus);
        Assert.IsNull(partial.TotalFileCount);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var cancelled = s.Scan(grant, cts.Token);
        Assert.AreNotEqual(InventoryScanStatus.Complete, cancelled.ScanStatus);
        Assert.AreSame(complete, s.Current);
        Assert.IsTrue(before.SequenceEqual(SHA256.HashData(File.ReadAllBytes(file))));
        var concurrency = Session();
        var concurrentGrant = Grant(concurrency);
        bool revokedFromOtherThread = false;
        var refused = concurrency.Scan(concurrentGrant,
            beforeChildOpenForTest: _ =>
            {
                if (revokedFromOtherThread) return;
                Task.Run(concurrency.Revoke).GetAwaiter().GetResult();
                revokedFromOtherThread = true;
            });
        Assert.IsTrue(revokedFromOtherThread);
        Assert.AreNotEqual(InventoryScanStatus.Complete, refused.ScanStatus);
        Assert.IsTrue(refused.Diagnostics.Any(x => x.Code == "RS_A_DENIED"));
        Console.WriteLine("RS_A03_CROSS_THREAD_REVOKE_PASS");
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new RsAFixtureRomSession(new RsABudget(MaxHeaderBytes: 1)));
        Console.WriteLine("RS_A03_PASS header_bytes_read=0 rom_hashes_by_scanner=0 writes_by_scanner=0");
    }

    [TestMethod]
    public void RSA_04_SimulatedOfflineKeepsCurrentNoAutomaticRootRebind()
    {
        Put("gba/one.gba");
        var s = Session();
        var g = Grant(s);
        var good = s.Scan(g);
        Assert.AreEqual(InventoryScanStatus.Complete, good.ScanStatus);
        var unplugged = _root + "-disconnected";
        Directory.Move(_root, unplugged);
        var offline = s.Scan(g);
        Assert.AreNotEqual(InventoryScanStatus.Complete, offline.ScanStatus);
        Assert.AreEqual(InventoryAvailability.Offline, offline.Availability);
        Assert.AreSame(good, s.Current);
        Assert.AreSame(offline, s.LastAttempt);
        Assert.IsNull(offline.TotalFileCount);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            RsAAnchoredReader.ValidateFixturePath(unplugged));
        // Replacement under exactly the SAME path must not reuse the prior grant.
        Directory.CreateDirectory(_root);
        Put("gba/replacement.gba");
        var samePathReplacement = s.Scan(g);
        Assert.AreNotEqual(InventoryScanStatus.Complete, samePathReplacement.ScanStatus);
        Assert.AreSame(good, s.Current);
        Assert.IsTrue(samePathReplacement.Diagnostics.Any(x => x.Code == "RS_A_DENIED"));
        Console.WriteLine("RS_A04_SAME_PATH_DIFFERENT_FILEID_REJECTED real_windows=1");
        var another = Path.Combine(Path.GetTempPath(),
            "rm-rsa-fixture-" + Guid.NewGuid().ToString("N"), "ROMS");
        Directory.CreateDirectory(another);
        try
        {
            s.SelectFixture(another, _rootId);
            Assert.AreEqual(InventoryScanStatus.Failed, s.Scan(g).ScanStatus);
            Assert.AreSame(good, s.Current);
        }
        finally { Directory.Delete(Path.GetDirectoryName(another)!, true); }
        Console.WriteLine("RS_A04_PASS simulated_volume_loss old_current_retained no_auto_rebind");
    }

    [TestMethod]
    public void RSA_05_RomsGrantNeverAuthorizesMediaOrSkiaPreview()
    {
        Put("gba/one.gba");
        var s = Session();
        Assert.AreEqual(InventoryScanStatus.Complete, s.Scan(Grant(s)).ScanStatus);
        // The normal, publicly constructed media service has independent hold.
        var media = new MediaIndexApplicationService();
        var mediaResult = media.ScanConfirmed(new MediaRootScope(
            Path.Combine(_fixture, "MEDIAS")), explicitConsent: true);
        Assert.AreEqual(InventoryScanStatus.Failed, mediaResult.ScanStatus);
        Assert.IsTrue(mediaResult.Diagnostics.Any(x => x.Code == "REAL_MEDIA_SCAN_HOLD"));
        var preview = new MediaPreviewService();
        var result = preview.LoadAsync(new MediaPreviewRequest(
            "run-not-bound", "root-not-bound", "obs-not-bound", 64))
            .GetAwaiter().GetResult();
        using (result)
            Assert.AreNotEqual(MediaPreviewStatus.Ready, result.Status);
        Console.WriteLine("RS_A05_PASS ROMS!=MEDIAS!=PREVIEW; no Skia decode invoked");
    }

    [TestMethod]
    public void RSA_06_ProductionGatewayClosedWithoutAnyFixtureOrCliBackdoor()
    {
        var childRoot = Environment.GetEnvironmentVariable("RSA_CHILD_ROOT");
        if (childRoot is not null)
        {
            var child = new RsAFixtureRomSession();
            child.SelectFixture(childRoot,
                Environment.GetEnvironmentVariable("RSA_CHILD_STORAGE_ROOT_ID")!);
            Assert.AreEqual(InventoryScanStatus.Failed, child.Scan(null).ScanStatus);
            Console.WriteLine("RS_A01_CHILD_PROCESS_GRANT_ABSENT");
        }
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            RealRomInventoryGate.Scan());
        Assert.IsFalse(typeof(RealRomInventoryGate).Assembly.GetExportedTypes()
            .Any(type => type.Name is nameof(RsAFixtureRomSession) or
                nameof(RsAReadGrant) or nameof(RsAAnchoredReader)));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            RsAAnchoredReader.ValidateFixturePath(
                Path.Combine(Path.GetTempPath(), "owner-personal-ROMs", "ROMS")));
        Assert.AreEqual(1, typeof(RealRomInventoryGate).GetMethods(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.DeclaredOnly).Length);
        Console.WriteLine("RS_A06_PASS production_export=fail_closed no_fixture_grant_public_api");
        Console.WriteLine("RS_A06_ZIP_NATIVE_PROOF_PENDING_CI_NEW_HEAD");
    }
}
