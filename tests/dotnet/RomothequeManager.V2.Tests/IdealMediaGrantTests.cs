using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.Tests;

/// <summary>PREPARATORY MEDIAS/PREVIEW tests. Native handles touch only new fictional
/// directories in disposable test storage, never a selected user collection.</summary>
[TestClass]
public sealed class IdealMediaGrantTests
{
    private readonly List<string> _fixtures = new();

    private string NewSyntheticMediaRoot()
    {
        var parent = Environment.GetEnvironmentVariable("RUNNER_TEMP");
        if (string.IsNullOrWhiteSpace(parent))
            parent = Path.GetTempPath();
        var fixture = Path.Combine(parent,
            "rm-ideal-media-grant-" + Guid.NewGuid().ToString("N"));
        var media = Path.Combine(fixture, "MEDIAS");
        Directory.CreateDirectory(Path.Combine(media, "gba"));
        _fixtures.Add(fixture);
        return media;
    }

    [TestCleanup]
    public void RemoveSyntheticFolders()
    {
        foreach (var folder in _fixtures)
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
    }

    [TestMethod]
    public void MEDIA_01_IndependentIndexGrantCannotBecomePreviewOrBeReplayed()
    {
        if (!OperatingSystem.IsWindows()) return;
        var media = NewSyntheticMediaRoot();
        using var session = new IdealMediaGrantSession();
        var rootId = Guid.NewGuid().ToString("D");
        session.Select(media, rootId);
        var version = session.Generation;
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            session.AuthorizeIndex(false, DateTimeOffset.UtcNow.AddMinutes(1)));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            session.AuthorizeIndex(true, DateTimeOffset.UtcNow.AddMinutes(3)));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
            session.AuthorizePreview("unseen-run", "unseen-image", true,
                DateTimeOffset.UtcNow.AddMinutes(1)));
        var indexGrant = session.AuthorizeIndex(true,
            DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.AreEqual(IdealMediaOperation.Index, indexGrant.Operation);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
        {
            using var denied = session.ClaimRoot(indexGrant,
                IdealMediaOperation.Preview, version, "run", "image");
        });
        using (var handle = session.ClaimRoot(indexGrant, IdealMediaOperation.Index, version))
            Assert.IsFalse(handle.IsInvalid);
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
        {
            using var replay = session.ClaimRoot(indexGrant, IdealMediaOperation.Index, version);
        });
        session.Revoke();
        Assert.IsFalse(indexGrant.Valid(IdealMediaOperation.Index, rootId, version));
    }

    [TestMethod]
    public void MEDIA_02_NewSelectionRevokesOldGrantAndRoleRootNeverAcceptsRoms()
    {
        if (!OperatingSystem.IsWindows()) return;
        var a = NewSyntheticMediaRoot();
        var b = NewSyntheticMediaRoot();
        var rootA = Guid.NewGuid().ToString("D");
        using var session = new IdealMediaGrantSession();
        session.Select(a, rootA);
        var versionA = session.Generation;
        var old = session.AuthorizeIndex(true, DateTimeOffset.UtcNow.AddMinutes(1));
        session.Select(b, Guid.NewGuid().ToString("D"));
        Assert.IsTrue(session.Generation > versionA);
        Assert.IsFalse(old.Valid(IdealMediaOperation.Index, rootA, versionA));
        Assert.ThrowsExactly<UnauthorizedAccessException>(() =>
        {
            using var outOfScope = session.ClaimRoot(old,
                IdealMediaOperation.Index, versionA);
        });
        var denied = false;
        try { session.Select(Path.GetDirectoryName(b)!, Guid.NewGuid().ToString("D")); }
        catch (UnauthorizedAccessException) { denied = true; }
        Assert.IsTrue(denied);
    }
}
