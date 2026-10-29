using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class DemoScenarioUiStateTests
{
    [TestMethod]
    public void CanonicalSelectionNeverConfusesSpecificationWithObservation()
    {
        var state = DemoScenarioUiState.LoadCanonical();
        Assert.AreEqual(30, state.DeclaredCanonicalCount);
        Assert.AreEqual(13, state.DeclaredPlatformCount);
        Assert.IsFalse(state.Active);
        Assert.IsTrue(state.Select("IDEAL"));
        Assert.AreEqual(30, state.VisibleGames.Count);
        Assert.IsTrue(state.Open("demo01-011"));
        Assert.AreEqual("Metal Deer Salad", state.SelectedGame!.Title);
        Assert.IsTrue(state.Provenance.Contains("non observé", StringComparison.Ordinal));
        Assert.IsTrue(state.VisibleGames.All(game => game.Media.Count == 6));
    }

    [TestMethod]
    public void ChaosKeepsMissingRomAndMediaAsScenarioOnlyEvidence()
    {
        var state = DemoScenarioUiState.LoadCanonical();
        Assert.IsTrue(state.Select("CHAOS"));
        Assert.AreEqual(30, state.VisibleGames.Count);
        Assert.IsFalse(state.VisibleGames.Single(game => game.Id == "demo01-005").RomExpectedInFixture);
        Assert.IsFalse(state.VisibleGames.Single(game => game.Id == "demo01-017").RomExpectedInFixture);
        Assert.IsTrue(state.VisibleGames.Any(game => game.Media.Any(m => !m.ExpectedInFixture)));
        state.Search("Jaxson");
        Assert.AreEqual(1, state.VisibleGames.Count);
        Assert.AreEqual("demo01-002", state.VisibleGames.Single().Id);
    }

    [TestMethod]
    public void MysteryNeverLeaksHiddenOracleGameIdentities()
    {
        var state = DemoScenarioUiState.LoadCanonical();
        Assert.IsTrue(state.Select("IDEAL"));
        Assert.IsTrue(state.Open("demo01-001"));
        Assert.IsTrue(state.Select("MYSTERY"));
        Assert.IsFalse(state.IdentityKnown);
        Assert.AreEqual(0, state.VisibleGames.Count);
        Assert.IsNull(state.SelectedGame);
        Assert.IsFalse(state.Open("demo01-001"));
        Assert.IsTrue(DemoScenarioUiState.Explanation("MYSTERY").Contains("INCONNUS", StringComparison.Ordinal));
        Assert.IsFalse(state.Select("UNKNOWN"));
        Assert.IsFalse(state.IdentityKnown);
        state.Clear();
        Assert.IsFalse(state.Active);
    }
}
