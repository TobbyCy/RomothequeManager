using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

[TestClass]
public sealed class LibraryViewTests
{
    private static readonly ReadOnlyCatalogue Catalogue = ReadOnlyCatalogue.Synthetic();

    private static JsonNode Fixture()
    {
        var assembly = typeof(LibraryProjection).Assembly;
        var name = assembly.GetManifestResourceNames().Single(value =>
            value.EndsWith("library-ui-v2-01b.synthetic.json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(name)!;
        using var reader = new StreamReader(stream);
        return JsonNode.Parse(reader.ReadToEnd())!;
    }

    private static LibraryProjection Projection() => LibraryProjection.LoadSynthetic(Catalogue);

    [TestMethod]
    public void ThreeLibrariesMapExactlyToSixInventedGames()
    {
        var projection = Projection();
        Assert.AreEqual(3, projection.Libraries.Count);
        Assert.AreEqual(6, Catalogue.Games.Count);
        CollectionAssert.AreEquivalent(
            projection.Libraries.Select(item => item.LibraryId).ToArray(),
            Catalogue.Games.Select(item => item.LibraryId).Distinct(StringComparer.Ordinal).ToArray());
        Assert.IsTrue(Catalogue.Games.All(item => item.GameId.StartsWith("syn-", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void OfflineStaleHistoryRemainsAccessible()
    {
        var library = Projection().FindLibrary("lib-a")!;
        Assert.AreEqual("OFFLINE", library.CurrentAvailability);
        Assert.AreEqual("STALE", library.Freshness);
        Assert.IsTrue(library.IndexAccessible);
        Assert.AreEqual(2, library.Generations.Count);
        Assert.IsNotNull(library.LastVerifiedCompleteRef);
    }

    [TestMethod]
    public void UnknownAvailabilityHasNoInventedObservationTime()
    {
        var library = Projection().FindLibrary("lib-b")!;
        Assert.AreEqual("UNKNOWN", library.CurrentAvailability);
        Assert.IsNull(library.AvailabilityObservedAtUtc);
        Assert.AreEqual("UNKNOWN", library.Freshness);
    }

    [TestMethod]
    public void PartialAttemptDoesNotReplaceLastVerifiedComplete()
    {
        var library = Projection().FindLibrary("lib-a")!;
        Assert.AreEqual("a-complete-20261019", library.LastVerifiedCompleteRef);
        Assert.AreEqual("partial", library.Generations.Last().ScanStatus);
        Assert.AreNotEqual(library.LastVerifiedCompleteRef, library.Generations.Last().SnapshotId);
    }

    [TestMethod]
    public void PrequalifiedComparisonsHaveSixClassesAndUnionDenominators()
    {
        var projection = Projection();
        Assert.AreEqual(3, projection.Comparisons.Count);
        var classifications = projection.Comparisons.SelectMany(item => item.Entries)
            .Select(item => item.Classification).Distinct(StringComparer.Ordinal).ToArray();
        CollectionAssert.AreEquivalent(LibraryProjection.Classifications.ToArray(), classifications);
        foreach (var comparison in projection.Comparisons)
        {
            Assert.AreEqual(comparison.Entries.Count, comparison.CountDenominator);
            Assert.AreEqual(comparison.CountDenominator, comparison.Counts.Values.Sum());
            foreach (var classification in LibraryProjection.Classifications)
                Assert.AreEqual(comparison.Entries.Count(item => item.Classification == classification),
                    comparison.Counts[classification]);
        }
    }

    [TestMethod]
    public void IncompleteCoverageStaysUnknown()
    {
        var comparison = Projection().FindComparison("lib-a", "a-complete-20261019", "fictif-2026-10-20")!;
        Assert.AreEqual("INCOMPLETE", comparison.CoverageStatus);
        Assert.AreEqual(2, comparison.Counts["UNKNOWN"]);
        Assert.AreEqual(0, comparison.Counts["NOT_OBSERVED_IN_B_COVERED_SCOPE"]);
        Assert.IsTrue(comparison.ScopeNotComparable.Count > 0);
    }

    [TestMethod]
    public void NotObservedNeverBecomesDeletedClassification()
    {
        var comparison = Projection().FindComparison("lib-c", "c-complete-20261021", "fictif-2026-10-22")!;
        Assert.AreEqual(1, comparison.Counts["NOT_OBSERVED_IN_B_COVERED_SCOPE"]);
        Assert.IsFalse(comparison.Counts.ContainsKey("DELETED"));
        Assert.IsTrue(comparison.Entries.Any(item => item.Classification == "NOT_OBSERVED_IN_B_COVERED_SCOPE"));
    }

    [TestMethod]
    public void SelectionRejectsSameReversedAndUnknownSnapshots()
    {
        var state = new LibraryViewState(Projection());
        state.SetAfter(state.BeforeSnapshotId);
        Assert.IsFalse(state.TryCompare());
        Assert.IsTrue(state.ErrorMessage!.Contains("distinctes", StringComparison.Ordinal));
        state.SetBefore("fictif-2026-10-20");
        state.SetAfter("a-complete-20261019");
        Assert.IsFalse(state.TryCompare());
        Assert.IsTrue(state.ErrorMessage!.Contains("précéder", StringComparison.Ordinal));
        state.SetAfter("missing-snapshot");
        Assert.IsFalse(state.TryCompare());
        Assert.IsTrue(state.ErrorMessage!.Contains("introuvable", StringComparison.Ordinal));
        Assert.IsFalse(state.SelectLibrary("missing-library"));
    }

    [TestMethod]
    public void SelectionReadsOnlyQualifiedPairs()
    {
        var state = new LibraryViewState(Projection());
        Assert.AreEqual("lib-a", state.SelectedLibraryId);
        Assert.IsTrue(state.TryCompare());
        Assert.IsNotNull(state.CurrentComparison);
        Assert.IsTrue(state.SelectLibrary("lib-c"));
        Assert.IsTrue(state.TryCompare());
        Assert.AreEqual("COMPLETE", state.CurrentComparison!.CoverageStatus);
        Assert.IsNull(state.ErrorMessage);
    }

    [TestMethod]
    public void InaccessibleIndexPreservesNullAndRefusesComparison()
    {
        var node = Fixture();
        var library = node["registry"]!["libraries"]!.AsArray()[0]!;
        library["indexAccessible"] = JsonValue.Create(false);
        foreach (var generation in library["generations"]!.AsArray())
        {
            generation!["countKnown"] = JsonValue.Create(false);
            generation["observedUnitCount"] = null;
        }
        node["comparisons"]!.AsArray().RemoveAt(0);
        var projection = LibraryProjection.FromJson(node.ToJsonString());
        var inaccessible = projection.FindLibrary("lib-a")!;
        Assert.IsFalse(inaccessible.IndexAccessible);
        Assert.IsFalse(inaccessible.Generations[0].CountKnown);
        Assert.IsNull(inaccessible.Generations[0].ObservedUnitCount);
        var state = new LibraryViewState(projection);
        Assert.IsFalse(state.TryCompare());
        Assert.IsTrue(state.ErrorMessage!.Contains("inaccessible", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvalidSchemaDuplicateIdentitiesAndUnprovenCoverageAreRejected()
    {
        var node = Fixture();
        node["registry"]!["schemaVersion"] = JsonValue.Create("unexpected");
        Assert.ThrowsExactly<ArgumentException>(() => LibraryProjection.FromJson(node.ToJsonString()));

        node = Fixture();
        node["registry"]!["libraries"]!.AsArray()[1]!["libraryId"] = JsonValue.Create("lib-a");
        Assert.ThrowsExactly<ArgumentException>(() => LibraryProjection.FromJson(node.ToJsonString()));

        node = Fixture();
        node["registry"]!["libraries"]!.AsArray()[0]!["generations"]!.AsArray()[0]!["coverageProof"] = new JsonArray();
        Assert.ThrowsExactly<ArgumentException>(() => LibraryProjection.FromJson(node.ToJsonString()));
    }

    [TestMethod]
    public void InconsistentCountsAndInventedZeroAreRejected()
    {
        var node = Fixture();
        node["comparisons"]!.AsArray()[0]!["counts"]!["UNKNOWN"] = JsonValue.Create(999);
        Assert.ThrowsExactly<ArgumentException>(() => LibraryProjection.FromJson(node.ToJsonString()));

        node = Fixture();
        var library = node["registry"]!["libraries"]!.AsArray()[0]!;
        library["indexAccessible"] = JsonValue.Create(false);
        foreach (var generation in library["generations"]!.AsArray())
            generation!["countKnown"] = JsonValue.Create(false);
        node["comparisons"]!.AsArray().RemoveAt(0);
        Assert.ThrowsExactly<ArgumentException>(() => LibraryProjection.FromJson(node.ToJsonString()));
    }

    [TestMethod]
    public void NavigationPreservesPreviousThreeViews()
    {
        var navigation = new NavigationState(Catalogue);
        navigation.ShowLibraries();
        Assert.AreEqual(POCView.Libraries, navigation.CurrentView);
        navigation.ShowCollection();
        navigation.Search("GBA");
        Assert.IsTrue(navigation.Open("syn-001"));
        Assert.AreEqual(POCView.GameDetail, navigation.CurrentView);
        navigation.ShowDashboard();
        Assert.AreEqual(POCView.Dashboard, navigation.CurrentView);
    }

    [TestMethod]
    public void PresentationProjectionHasNoPhysicalMutationApi()
    {
        var methodNames = typeof(LibraryProjection).GetMethods(
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.DeclaredOnly)
            .Select(item => item.Name).ToArray();
        Assert.IsFalse(methodNames.Any(name =>
            name.StartsWith("Scan", StringComparison.Ordinal) ||
            name.StartsWith("Write", StringComparison.Ordinal) ||
            name.StartsWith("Export", StringComparison.Ordinal) ||
            name.StartsWith("Commit", StringComparison.Ordinal)));
    }
}
