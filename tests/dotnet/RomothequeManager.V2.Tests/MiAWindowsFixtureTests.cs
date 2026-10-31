using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Inventory.Media.MiA;
using RomothequeManager.V2.Inventory.RsA;

namespace RomothequeManager.V2.Tests;

/// <summary>MI-A fixtures only. No personal MEDIAS, Skia decoder, network or writes
/// to the scanned source by the scanner. Physical USB and non-admin ACL remain NOT_EXECUTED.</summary>
[TestClass]
public sealed class MiAWindowsFixtureTests
{
    private string _fixture = "";
    private string _root = "";
    private string _rootId = "";

    [TestInitialize]
    public void Setup()
    {
        if (!OperatingSystem.IsWindows()) Assert.Inconclusive("MI_A_WINDOWS_ONLY");
        _fixture = Path.Combine(Path.GetTempPath(),
            "rm-mia-fixture-" + Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_fixture, "MEDIAS");
        _rootId = Guid.NewGuid().ToString("D");
        Directory.CreateDirectory(_root);
        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var admin = new System.Security.Principal.WindowsPrincipal(identity).IsInRole(
            System.Security.Principal.WindowsBuiltInRole.Administrator);
        Console.WriteLine("MI_A_NATIVE_ENV OS=" + Environment.OSVersion.VersionString +
            " NET=" + System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription +
            " Elevated=" + admin + " FS=" + new DriveInfo(Path.GetPathRoot(_root)!).DriveFormat);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (_fixture.Length > 0 && Directory.Exists(_fixture))
            Directory.Delete(_fixture, recursive: true);
    }

    private static uint PngCrc(ReadOnlySpan<byte> bytes)
    {
        uint crc = 0xffffffff;
        foreach (var b in bytes)
        {
            crc ^= b;
            for (int i = 0; i < 8; i++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static byte[] TinyPng()
    {
        var b = new byte[33];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(b, 0);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(8, 4), 13);
        "IHDR"u8.CopyTo(b.AsSpan(12, 4));
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(16, 4), 1);
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(20, 4), 1);
        b[24] = 8; b[25] = 6;
        BinaryPrimitives.WriteUInt32BigEndian(b.AsSpan(29, 4), PngCrc(b.AsSpan(12, 17)));
        return b;
    }

    private string Put(string relative, byte[]? bytes = null)
    {
        var file = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, bytes ?? TinyPng());
        return file;
    }

    private MiAFixtureMediaSession Session(MiABudget? budget = null)
    {
        var s = new MiAFixtureMediaSession(budget);
        s.SelectFixture(_root, _rootId);
        return s;
    }

    private static MiAReadGrant Grant(MiAFixtureMediaSession session) =>
        session.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(2));

    [TestMethod]
    public void MIA_01_GrantIsSpecificVolatileRevocableAndNotRestored()
    {
        Put("gb/one.png");
        var s = Session();
        Assert.AreEqual(InventoryScanStatus.Failed, s.Scan(null).ScanStatus);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            s.AuthorizeFixture(false, DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            s.AuthorizeFixture(true, DateTimeOffset.UtcNow.AddMinutes(6)));
        var g = Grant(s);
        Assert.AreEqual(MiAOperation.MediaIndex, g.Operation);
        Assert.AreNotEqual(typeof(MiAReadGrant), typeof(RsAReadGrant));
        Assert.AreEqual(InventoryScanStatus.Complete, s.Scan(g).ScanStatus);
        s.Revoke();
        Assert.AreEqual(InventoryScanStatus.Failed, s.Scan(g).ScanStatus);
        var fresh = Session();
        Assert.AreEqual(InventoryScanStatus.Failed, fresh.Scan(null).ScanStatus);
        var second = Grant(s);
        s.SelectFixture(_root, _rootId);
        Assert.AreEqual(InventoryScanStatus.Failed, s.Scan(second).ScanStatus);
        var expired = MiAReadGrant.Issue(_rootId, s.SelectionVersion, 0,
            DateTimeOffset.UtcNow.AddSeconds(-1), g.RootIdentity, new MiABudget());
        Assert.IsFalse(expired.Valid(_rootId, s.SelectionVersion, 0));
        Console.WriteLine("MI_A01_PASS independent_media_grant expiry revoked version restart_model");
    }

    [TestMethod]
    public void MIA_02_AnchoredOpensRejectTraversalSymlinksHardlinksAndSwaps()
    {
        var inFile = Put("gb/one.png");
        var outside = Path.Combine(_fixture, "OUTSIDE");
        Directory.CreateDirectory(outside);
        var secret = Path.Combine(outside, "secret.png");
        File.WriteAllBytes(secret, TinyPng());
        foreach (var invalid in new[] { @"\\server\share\MEDIAS", @"\\?\C:\temp\MEDIAS",
            _root + @"\..\OUTSIDE", "https://invalid/MEDIAS" })
            Assert.ThrowsExactly<ArgumentException>(() =>
                MiAAnchoredMediaReader.ValidateFixturePath(invalid));
        var s = Session();
        var g = Grant(s);
        var good = s.Scan(g);
        Assert.AreEqual(InventoryScanStatus.Complete, good.ScanStatus);
        Assert.AreEqual(1, good.ObservedFileCount);

        var hardlink = Path.Combine(_root, "gb", "outside.png");
        bool hardlinkCreated = CreateHardLinkW(hardlink, secret, IntPtr.Zero);
        if (hardlinkCreated)
        {
            var result = s.Scan(g);
            Assert.AreNotEqual(InventoryScanStatus.Complete, result.ScanStatus);
            Assert.AreSame(good, s.Current);
            File.Delete(hardlink);
            Console.WriteLine("MI_A02_HARDLINK_CREATED_AND_REFUSED");
        }
        else Console.WriteLine("MI_A02_HARDLINK_NOT_EXECUTED win32=" + Marshal.GetLastWin32Error());

        var symlink = Path.Combine(_root, "gb", "linked.png");
        var symlinkMade = false;
        try { File.CreateSymbolicLink(symlink, secret); symlinkMade = true; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        if (symlinkMade)
        {
            var result = s.Scan(g);
            Assert.AreNotEqual(InventoryScanStatus.Complete, result.ScanStatus);
            File.Delete(symlink);
            Console.WriteLine("MI_A02_FILE_REPARSE_CREATED_AND_REFUSED");
        }
        else Console.WriteLine("MI_A02_REPARSE_NOT_EXECUTED_NO_PRIVILEGE");

        bool parentAttempted = false, parentSwitched = false;
        var parent = Path.GetDirectoryName(inFile)!;
        var duringParent = s.Scan(g, beforeChildOpenForTest: rel =>
        {
            if (rel != "gb/one.png" || parentAttempted) return;
            parentAttempted = true;
            try
            {
                Directory.Move(parent, parent + "-moved");
                Directory.CreateSymbolicLink(parent, outside);
                parentSwitched = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        });
        Assert.IsTrue(parentAttempted);
        Assert.IsFalse(duringParent.Observations.Any(x => x.RelativePath.Contains("secret.png")));
        Console.WriteLine("MI_A02_PARENT_SWAP_ATTEMPTED switched=" + parentSwitched +
            " status=" + duringParent.ScanStatus);

        // Recreate clean parent fixture for the final-file substitution trial.
        if (Directory.Exists(parent + "-moved"))
        {
            if (Directory.Exists(parent)) Directory.Delete(parent);
            Directory.Move(parent + "-moved", parent);
        }
        if (!File.Exists(inFile)) Put("gb/one.png");
        bool finalAttempted = false, finalSwitched = false;
        var final = s.Scan(g, beforeChildOpenForTest: rel =>
        {
            if (rel != "gb/one.png" || finalAttempted) return;
            finalAttempted = true;
            try
            {
                File.Move(inFile, inFile + ".old");
                File.CreateSymbolicLink(inFile, secret);
                finalSwitched = true;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        });
        Assert.IsTrue(finalAttempted);
        Assert.AreNotEqual(InventoryScanStatus.Complete, final.ScanStatus);
        Assert.IsFalse(final.Observations.Any(x => x.RelativePath.Contains("secret.png")));
        Assert.AreSame(good, s.Current);
        Console.WriteLine("MI_A02_FINAL_SWAP_ATTEMPTED switched=" + finalSwitched +
            " status=" + final.ScanStatus + " OUTSIDE_READS=0");

        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            MiAAnchoredMediaReader.ValidateComponent("GAME~1.PNG"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            MiAAnchoredMediaReader.ValidateComponent("image.png:alt"));
    }

    [TestMethod]
    public void MIA_03_HeaderFormatHardCapsAndBytesReallyRead()
    {
        Put("gb/valid.png");
        Put("gb/invalid.jpg", new byte[64]);
        var webp = new byte[33];
        "RIFF"u8.CopyTo(webp);
        "WEBP"u8.CopyTo(webp.AsSpan(8));
        Put("gb/not-decoded.webp", webp);
        var s = Session();
        var good = s.Scan(Grant(s));
        Assert.AreEqual(InventoryScanStatus.Complete, good.ScanStatus);
        Assert.AreEqual(3, good.ObservedFileCount);
        Assert.IsTrue(good.Observations.Any(x => x.DetectedFormat == MediaImageFormat.Png &&
            x.HeaderValidated && x.Width == 1 && x.Height == 1));
        Assert.IsTrue(good.Observations.Any(x => x.DetectedFormat == MediaImageFormat.Webp &&
            !x.HeaderValidated && x.DiagnosticRefs.Contains("UNSUPPORTED_FORMAT")));
        Assert.IsTrue(s.LastHeaderBytesRead > 0);
        Assert.IsTrue(s.LastHeaderBytesRead <= 8388608);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new MiAFixtureMediaSession(new MiABudget(MaxDepth: 9)));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new MiAFixtureMediaSession(new MiABudget(MaxTotalHeaderBytes: 8388609)));
        var cap = Session(new MiABudget(MaxFiles: 1));
        Assert.AreEqual(InventoryScanStatus.Partial, cap.Scan(Grant(cap)).ScanStatus);
        var sum = Session(new MiABudget(MaxTotalHeaderBytes: 33));
        Assert.AreEqual(InventoryScanStatus.Partial, sum.Scan(Grant(sum)).ScanStatus);
        Assert.IsTrue(sum.LastHeaderBytesRead <= 33);
        Console.WriteLine("MI_A03_PASS actual_header_bytes=" + s.LastHeaderBytesRead +
            " cap_bytes=" + sum.LastHeaderBytesRead + " no_skia_decode");
    }

    [TestMethod]
    public void MIA_04_CancelDuringNativeHeaderReadPreservesCurrent()
    {
        Put("gb/base.png");
        var s = Session();
        var g = Grant(s);
        var last = s.Scan(g);
        Assert.AreEqual(InventoryScanStatus.Complete, last.ScanStatus);
        Put("gb/second.png", new byte[8192]);
        using var cts = new CancellationTokenSource();
        var result = s.Scan(g, cts.Token, afterHeaderReadForTest: _ => cts.Cancel());
        Assert.AreEqual(InventoryScanStatus.Partial, result.ScanStatus);
        Assert.AreSame(last, s.Current);
        Assert.AreSame(result, s.LastAttempt);
        Console.WriteLine("MI_A04_PASS cancelled_during_real_handle_read");
    }

    [TestMethod]
    public void MIA_05_ReplacementAtSamePathAndOfflineHaveNoRebind()
    {
        Put("gb/old.png");
        var s = Session();
        var g = Grant(s);
        var original = s.Scan(g);
        Assert.AreEqual(InventoryScanStatus.Complete, original.ScanStatus);
        Directory.Move(_root, _root + "-old");
        Directory.CreateDirectory(_root);
        Put("gb/new.png");
        var replaced = s.Scan(g);
        Assert.AreNotEqual(InventoryScanStatus.Complete, replaced.ScanStatus);
        Assert.AreSame(original, s.Current);
        Directory.Delete(_root, true);
        var offline = s.Scan(g);
        Assert.AreNotEqual(InventoryScanStatus.Complete, offline.ScanStatus);
        Assert.AreSame(original, s.Current);
        Console.WriteLine("MI_A05_PASS root_identity_no_rebind_simulated_offline");
    }

    [TestMethod]
    public void MIA_06_AppExclusionsNoOverlapAndNoAutomaticDecision()
    {
        Put("gb/visible.png");
        Put("ROMOTHEQUE_MANAGER/data/private.png");
        Put("cache/ignored.png");
        Directory.CreateDirectory(Path.Combine(_fixture, "ROMS"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            MiAAnchoredMediaReader.ValidateFixturePath(Path.Combine(_fixture, "ROMS")));
        var s = Session();
        var result = s.Scan(Grant(s));
        Assert.AreEqual(InventoryScanStatus.Complete, result.ScanStatus);
        Assert.AreEqual(1, result.ObservedFileCount);
        Assert.IsFalse(result.Observations.Any(x =>
            x.RelativePath.Contains("private.png") || x.RelativePath.Contains("ignored.png")));
        Console.WriteLine("MI_A06_PASS app_exclusions no_roms no_auto_user_decision");
    }

    [TestMethod]
    public void MIA_08_StandardPublicGateCannotUseFixtureCapability()
    {
        var result = new MediaIndexApplicationService().ScanConfirmed(
            new MediaRootScope(_root), explicitConsent: true);
        Assert.AreEqual(InventoryScanStatus.Failed, result.ScanStatus);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Code == "REAL_MEDIA_SCAN_HOLD"));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() => RealRomInventoryGate.Scan());
        Assert.IsFalse(typeof(MiAReadGrant).IsPublic);
        Assert.IsFalse(typeof(MiAFixtureMediaSession).IsPublic);
        Console.WriteLine("MI_A08_PASS public_media_roms_holds fixture_api_internal");
        Console.WriteLine("MI_A07_NOT_EXECUTED_NON_ADMIN_ACL_AND_FILESYSTEM_MATRIX");
        Console.WriteLine("MI_A09_NOT_EXECUTED_PHYSICAL_USB_UNPLUG");
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string newFile, string existingFile, IntPtr security);
}
