using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class ReadOnlyCatalogueTests
{
    [TestMethod] public void OnlySixInventedRecordsExist()
    {
        var records = ReadOnlyCatalogue.Synthetic().Games;
        Assert.AreEqual(6, records.Count);
        Assert.IsTrue(records.All(x => x.GameId.StartsWith("syn-", StringComparison.Ordinal)));
        Assert.AreEqual(6, records.Select(x => x.GameId).Distinct().Count());
    }

    [TestMethod] public void DashboardStartsWithoutSelection()
    {
        var state = new NavigationState(ReadOnlyCatalogue.Synthetic());
        Assert.AreEqual(POCView.Dashboard, state.CurrentView);
        Assert.IsNull(state.Selected);
    }

    [TestMethod] public void SearchIgnoresCaseAndKeepsIdentities()
    {
        var state = new NavigationState(ReadOnlyCatalogue.Synthetic());
        state.Search("ÉTOILES");
        Assert.AreEqual(POCView.Collection, state.CurrentView);
        Assert.AreEqual("syn-003", state.FilteredGames.Single().GameId);
    }

    [TestMethod] public void OpenUnknownIdCannotInventGame()
    {
        var state = new NavigationState(ReadOnlyCatalogue.Synthetic());
        Assert.IsFalse(state.Open("syn-missing"));
        Assert.AreEqual(POCView.Dashboard, state.CurrentView);
        Assert.IsNull(state.Selected);
    }

    [TestMethod] public void DetailKeepsExactProvenanceAndTimestamp()
    {
        var state = new NavigationState(ReadOnlyCatalogue.Synthetic());
        Assert.IsTrue(state.Open("syn-002"));
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Assert.AreEqual("fictif-2026-10-20", state.Selected!.SnapshotId);
        Assert.AreEqual("INCOMPLETE", state.Selected.Coverage);
        Assert.AreEqual(TimeSpan.Zero, state.Selected.ObservedAt.Offset);
    }

    [TestMethod] public void OfflineAndStaleNeverMeanDeleted()
    {
        var model = ReadOnlyCatalogue.Synthetic();
        Assert.AreEqual(1, model.CountLibraries("OFFLINE"));
        Assert.AreEqual(1, model.CountLibraries("UNKNOWN"));
        Assert.IsTrue(model.Games.Where(x => x.Availability == "OFFLINE")
            .All(x => x.Evidence.Contains("observation", StringComparison.OrdinalIgnoreCase)
                   || x.Evidence.Contains("disparition", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(model.Games.Any(x => x.Availability == "DELETED"));
    }

    [TestMethod] public void ReturningFromDetailPreservesSearchButClearsSelection()
    {
        var state = new NavigationState(ReadOnlyCatalogue.Synthetic());
        state.Search("GBA");
        Assert.IsTrue(state.Open("syn-001"));
        state.ShowCollection();
        Assert.AreEqual(POCView.Collection, state.CurrentView);
        Assert.IsNull(state.Selected);
        Assert.IsTrue(state.FilteredGames.Count >= 2);
    }

    [TestMethod] public void PublicModelHasNoWriteOrImportMethods()
    {
        var publicMethods = typeof(ReadOnlyCatalogue).GetMethods(System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly);
        CollectionAssert.AreEquivalent(new[] { "get_Games", "Search", "Find", "CountLibraries" },
            publicMethods.Select(m => m.Name).ToArray());
    }
}
