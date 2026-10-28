using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class VisualDashboardTests
{
    [TestMethod]
    public void DemoAlwaysQualifiesItsSyntheticData()
    {
        var catalogue = ReadOnlyCatalogue.Synthetic();
        var projection = LibraryProjection.LoadSynthetic(catalogue);
        var model = VisualDashboardData.Demo(catalogue, projection);
        Assert.AreEqual(6, model.ReferencedGames);
        Assert.AreEqual(3, model.KnownPlatforms);
        Assert.AreEqual(3, model.KnownLibraries);
        Assert.IsTrue(model.Synthetic);
        Assert.AreEqual(0, model.Diagnostics);
    }

    [TestMethod]
    public void ImportedOnlyReportsSnapshotContentsAndUnavailableRom()
    {
        var games = new EsDeGame[]
        {
            new("one", "variant1", "gba", "Fictitious A", "ROMS/gba/a.fixture", true,
                null, Array.Empty<EsDeMedia>(), Array.Empty<string>()),
            new("two", "variant2", "gba", "Fictitious B", "ROMS/gba/b.fixture", false,
                null, Array.Empty<EsDeMedia>(), Array.Empty<string>())
        };
        var diagnostic = new EsDeDiagnostic("ROM_MISSING", "gba", "ROMS/gba/b.fixture", "Missing");
        var snapshot = new EsDeCatalogueSnapshot("fictitious-session", "ES_DE_READ_ONLY", "fake",
            games, new[] { diagnostic });
        var model = VisualDashboardData.Imported(snapshot);
        Assert.AreEqual(2, model.ReferencedGames);
        Assert.AreEqual(1, model.KnownPlatforms);
        Assert.AreEqual(1, model.KnownLibraries);
        Assert.AreEqual(1, model.Diagnostics);
        Assert.AreEqual(1, model.RomUnavailable);
        Assert.IsFalse(model.Synthetic);
        Assert.IsTrue(model.Provenance.Contains("ES-DE", StringComparison.Ordinal));
    }

    [TestMethod]
    public void EmptyImportedSnapshotDoesNotInventDiscsOrCoverage()
    {
        var snapshot = new EsDeCatalogueSnapshot("synthetic-empty", "ES_DE_READ_ONLY",
            "no-game", Array.Empty<EsDeGame>(), Array.Empty<EsDeDiagnostic>());
        var model = VisualDashboardData.Imported(snapshot);
        Assert.AreEqual(0, model.ReferencedGames);
        Assert.AreEqual(0, model.KnownPlatforms);
        Assert.AreEqual(1, model.KnownLibraries);
        Assert.AreEqual(0, model.Diagnostics);
        Assert.IsFalse(model.Synthetic);
    }

    [TestMethod]
    public void SidebarStatesHaveDistinctHighContrastText()
    {
        var active = SidebarNavigationPalette.Resolve(true, false, true);
        var idle = SidebarNavigationPalette.Resolve(false, false, true);
        var hovered = SidebarNavigationPalette.Resolve(false, true, true);
        var disabled = SidebarNavigationPalette.Resolve(false, false, false);
        Assert.AreEqual(active, SidebarNavigationPalette.Resolve(true, true, true));
        Assert.AreEqual(disabled, SidebarNavigationPalette.Resolve(true, true, false));
        Assert.AreNotEqual(active.Background, idle.Background);
        Assert.AreNotEqual(hovered.Background, idle.Background);
        Assert.AreNotEqual(disabled.Background, idle.Background);
        Assert.AreNotEqual(active.Accent, idle.Accent);
        Assert.AreNotEqual(active.Accent, hovered.Accent);
        foreach (var pair in new[] { active, idle, hovered, disabled })
            Assert.IsTrue(Contrast(pair.Background, pair.Foreground) >= 4.5,
                $"Sidebar foreground/background contrast below 4.5:1: {pair}");
    }

    [TestMethod]
    public void HoverAndDisabledRemainVisiblyDistinct()
    {
        var hovered = SidebarNavigationPalette.Resolve(false, true, true);
        var disabled = SidebarNavigationPalette.Resolve(false, true, false);
        Assert.AreNotEqual(hovered.Foreground, disabled.Foreground);
        Assert.AreNotEqual(hovered.Accent, disabled.Accent);
        Assert.IsTrue(Contrast(hovered.Background, hovered.Foreground) > 6.0);
    }

    private static double Contrast(string background, string foreground)
    {
        static double Luminance(string hex)
        {
            double[] rgb = Enumerable.Range(0, 3).Select(i =>
            {
                double value = Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16) / 255.0;
                return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
            }).ToArray();
            return .2126 * rgb[0] + .7152 * rgb[1] + .0722 * rgb[2];
        }
        double a = Luminance(background), b = Luminance(foreground);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

}
