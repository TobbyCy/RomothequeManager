using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.MvvmFoundation;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class IdealMediaCoverProposalTests
{
    private static readonly DateTimeOffset Captured = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static FileObservation Rom(string id, string relative, string platform = "gba") =>
        new(id, "fixture", platform, relative, InventoryKind.PotentialRom,
            ".gba", 10, null, Captured, "SYNTHETIC_ROM",
            InventoryAvailability.Online, Array.Empty<string>());

    private static MediaObservation Cover(string id, string relative, string platform = "gba") =>
        new(id, "media-root", relative, platform, MediaKind.Cover, ".png",
            MediaImageFormat.Png, true, 86, 3, 3, null, Captured,
            InventoryAvailability.Online, "SYNTHETIC_COVER", Array.Empty<string>());

    private static InventorySnapshot Inventory(params FileObservation[] observations) =>
        new("fixture", "library", "rom-run", Captured,
            InventoryScanStatus.Complete, InventoryAvailability.Online,
            InventoryFreshness.Fresh, Array.Empty<InventoryCoverage>(),
            observations, Array.Empty<InventoryDiagnostic>(),
            Array.Empty<InventoryPlaylist>(), Array.Empty<PotentialDuplicateGroup>(),
            "fixture-hash", observations.Length, observations.Length, null);

    private static MediaIndexSnapshot Media(params MediaObservation[] covers) =>
        new("media-run", "media-root", Captured,
            InventoryScanStatus.Complete, InventoryAvailability.Online,
            InventoryFreshness.Fresh, Array.Empty<InventoryCoverage>(),
            covers, Array.Empty<InventoryDiagnostic>(), covers.Length, covers.Length);

    private static MediaUiSnapshot Project(InventorySnapshot roms, MediaIndexSnapshot media) =>
        new MediaPresentationService().Build(roms, media,
            VerifiedScopeMapping.FromExplicitSelections(roms, media,
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    { ["gba"] = "gba", ["ps2"] = "ps2" }, "SYNTHETIC_EXPLICIT_ROOT_SELECTIONS"),
            Array.Empty<ExplicitMediaReference>());

    [TestMethod]
    public void MEDIA_GUI_06_NoImageXmlStillYieldsObservedLocalCoverCandidate()
    {
        var roms = Inventory(Rom("rom-1", "ROMS/gba/Fiction.gba"));
        var media = Media(Cover("cover-1", "MEDIAS/gba/covers/Fiction.png"));
        var proposed = IdealMediaCoverProposals.Build(roms, media, Project(roms, media));
        Assert.AreEqual(1, proposed.Count);
        Assert.AreEqual(MediaUiState.CANDIDATE, proposed[0].State);
        Assert.AreEqual("cover-1", proposed[0].MediaObservationId);
        Assert.IsFalse(proposed[0].RequiresExplicitChoice);
        Assert.IsFalse(proposed[0].Label.Contains("ES-DE", StringComparison.Ordinal));
        Assert.IsFalse(proposed[0].Label.Contains("C:", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MEDIA_GUI_07_TwoSameStemCoversRemainSeparateAmbiguousChoices()
    {
        var roms = Inventory(Rom("rom-1", "ROMS/gba/Fiction.gba"));
        var media = Media(
            Cover("cover-a", "MEDIAS/gba/covers/Fiction.png"),
            Cover("cover-b", "MEDIAS/gba/alternative/Fiction.png"));
        var proposed = IdealMediaCoverProposals.Build(roms, media, Project(roms, media));
        Assert.AreEqual(2, proposed.Count);
        Assert.IsTrue(proposed.All(c => c.State == MediaUiState.AMBIGUOUS));
        Assert.IsTrue(proposed.All(c => c.RequiresExplicitChoice));
        CollectionAssert.AreEquivalent(new[] { "cover-a", "cover-b" },
            proposed.Select(x => x.MediaObservationId).ToArray());
    }

    [TestMethod]
    public void MEDIA_GUI_08_DifferentPlatformOrFilenameDoesNotCreateCandidate()
    {
        var roms = Inventory(Rom("rom-1", "ROMS/gba/Fiction.gba"));
        var media = Media(
            Cover("ps2", "MEDIAS/ps2/covers/Fiction.png", "ps2"),
            Cover("other", "MEDIAS/gba/covers/CompletelyDifferent.png"));
        Assert.AreEqual(0, IdealMediaCoverProposals.Build(roms, media,
            Project(roms, media)).Count);
    }

    [TestMethod]
    public void MEDIA_GUI_09_StaleMediaAndIncomparableGenerationDoNotExposeCandidates()
    {
        var roms = Inventory(Rom("rom-1", "ROMS/gba/Fiction.gba"));
        var current = Media(Cover("cover-1", "MEDIAS/gba/covers/Fiction.png"));
        var ui = Project(roms, current);
        var stale = current with { Freshness = InventoryFreshness.Stale };
        Assert.AreEqual(0, IdealMediaCoverProposals.Build(roms, stale, ui).Count);
        var differentRun = current with { RunId = "other-media-run" };
        Assert.AreEqual(0, IdealMediaCoverProposals.Build(roms, differentRun, ui).Count);
        Assert.AreEqual(0, IdealMediaCoverProposals.Build(
            roms with { ScanStatus = InventoryScanStatus.Partial }, current, ui).Count);
    }

    [TestMethod]
    public void MEDIA_GUI_10_IdenticalTitleIsNeverAFileMatchingKey()
    {
        var roms = Inventory(Rom("rom-1", "ROMS/gba/ActualFile.gba"));
        var media = Media(Cover("wrong-title", "MEDIAS/gba/covers/TitleShownInXml.png"));
        Assert.AreEqual(0, IdealMediaCoverProposals.Build(roms, media,
            Project(roms, media)).Count);
    }
}
