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
}
