using System.Text.Json.Nodes;
using Avalonia.Controls;
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
    private static JsonNode MultiRootMediaFixture()
    {
        var node = Fixture();
        var library = node["registry"]!["libraries"]!.AsArray()[2]!;
        library["sources"]!.AsArray().Add(JsonNode.Parse(
            """{"sourceKey":"synthetic-extra-c","rootKey":"synthetic-extra-root-c","domains":["ROMS","MEDIAS"]}"""));
        foreach (var generation in library["generations"]!.AsArray())
        {
            var proofs = generation!["coverageProof"]!.AsArray();
            foreach (var platform in new[] { "GBA", "PS1" })
            foreach (var domain in new[] { "ROMS", "MEDIAS" })
                proofs.Add(new JsonObject
                {
                    ["console"] = JsonValue.Create(platform),
                    ["sourceKey"] = JsonValue.Create("synthetic-extra-c"),
                    ["rootKey"] = JsonValue.Create("synthetic-extra-root-c"),
                    ["domain"] = JsonValue.Create(domain),
                    ["status"] = JsonValue.Create("complete")
                });
        }
        var comparison = node["comparisons"]!.AsArray()[2]!;
        comparison["before"]!["coverageProof"] =
            library["generations"]!.AsArray()[0]!["coverageProof"]!.DeepClone();
        comparison["after"]!["coverageProof"] =
            library["generations"]!.AsArray()[1]!["coverageProof"]!.DeepClone();
        return node;
    }

    private static void Reject(JsonNode node) =>
        Assert.ThrowsExactly<ArgumentException>(() => LibraryProjection.FromJson(node.ToJsonString()));

    [TestMethod]
    public void MultiRootAndRomsMediaRequireEveryExactPartition()
    {
        var complete = MultiRootMediaFixture();
        var valid = LibraryProjection.FromJson(complete.ToJsonString());
        Assert.AreEqual(3, valid.FindLibrary("lib-c")!.Sources
            .SelectMany(source => source.Domains).Count());

        var missingMedia = MultiRootMediaFixture();
        missingMedia["registry"]!["libraries"]!.AsArray()[2]!["generations"]!
            .AsArray()[0]!["coverageProof"]!.AsArray().RemoveAt(5);
        Reject(missingMedia);

        var missingRoot = Fixture();
        missingRoot["registry"]!["libraries"]!.AsArray()[2]!["sources"]!.AsArray().Add(
            JsonNode.Parse("""{"sourceKey":"extra","rootKey":"another-root","domains":["ROMS"]}"""));
        Reject(missingRoot);

        var missingDomain = Fixture();
        missingDomain["registry"]!["libraries"]!.AsArray()[2]!["sources"]!
            .AsArray()[0]!["domains"]!.AsArray().Add(JsonValue.Create("MEDIAS"));
        Reject(missingDomain);
    }

    [TestMethod]
    public void ForeignDuplicateAndMissingCoverageProofsFailClosed()
    {
        var missing = Fixture();
        missing["registry"]!["libraries"]!.AsArray()[0]!["generations"]!
            .AsArray()[0]!["coverageProof"]!.AsArray().RemoveAt(0);
        Reject(missing);

        var duplicate = Fixture();
        var proofs = duplicate["registry"]!["libraries"]!.AsArray()[0]!["generations"]!
            .AsArray()[0]!["coverageProof"]!.AsArray();
        proofs.Add(proofs[0]!.DeepClone());
        Reject(duplicate);

        foreach (var field in new[] { "sourceKey", "rootKey", "domain" })
        {
            var foreign = Fixture();
            foreign["registry"]!["libraries"]!.AsArray()[0]!["generations"]!
                .AsArray()[0]!["coverageProof"]!.AsArray()[0]![field] =
                    JsonValue.Create("FOREIGN");
            Reject(foreign);
        }
    }

    [TestMethod]
    public void FalseLastVerifiedCompleteAndCoverageClaimsAreRejected()
    {
        var partialRef = Fixture();
        partialRef["registry"]!["libraries"]!.AsArray()[0]!["lastVerifiedCompleteRef"] =
            JsonValue.Create("fictif-2026-10-20");
        Reject(partialRef);

        var falseComplete = Fixture();
        falseComplete["registry"]!["libraries"]!.AsArray()[0]!["generations"]!
            .AsArray()[0]!["coverageProof"]!.AsArray()[0]!["status"] =
                JsonValue.Create("partial");
        Reject(falseComplete);

        var bogusScan = Fixture();
        bogusScan["registry"]!["libraries"]!.AsArray()[0]!["generations"]!
            .AsArray()[0]!["scanStatus"] = JsonValue.Create("failed");
        Reject(bogusScan);
    }

    [TestMethod]
    public void ComparisonScopesMustMatchProvenABIntersectionExactly()
    {
        var absent = Fixture();
        absent["comparisons"]!.AsArray()[2]!["scopeCompared"] = new JsonArray();
        Reject(absent);

        var duplicate = Fixture();
        var scopes = duplicate["comparisons"]!.AsArray()[2]!["scopeCompared"]!.AsArray();
        scopes.Add(scopes[0]!.DeepClone());
        Reject(duplicate);

        var foreign = Fixture();
        foreign["comparisons"]!.AsArray()[2]!["scopeCompared"]!
            .AsArray()[0]!["console"] = JsonValue.Create("NES");
        Reject(foreign);

        var falseComplete = Fixture();
        falseComplete["comparisons"]!.AsArray()[0]!["coverageStatus"] =
            JsonValue.Create("COMPLETE");
        Reject(falseComplete);

        var excludedMismatch = Fixture();
        excludedMismatch["comparisons"]!.AsArray()[0]!["scopeNotComparable"]!
            .AsArray()[0]!["afterStatus"] = JsonValue.Create("complete");
        Reject(excludedMismatch);
    }

    [TestMethod]
    public void EmbeddedABAndLibraryMetadataMustMatchCanonicalRegistry()
    {
        foreach (var field in new[] { "capturedAtUtc", "scanStatus", "provenance" })
        {
            var node = Fixture();
            node["comparisons"]!.AsArray()[0]!["after"]![field] =
                JsonValue.Create("CONTRADICTORY");
            Reject(node);
        }
        var proof = Fixture();
        proof["comparisons"]!.AsArray()[2]!["before"]!["coverageProof"]!
            .AsArray()[0]!["rootKey"] = JsonValue.Create("wrong-root");
        Reject(proof);

        var meta = Fixture();
        meta["comparisons"]!.AsArray()[2]!["library"]!["freshness"] =
            JsonValue.Create("STALE");
        Reject(meta);
    }

    [TestMethod]
    public void ThreeFrozenUiOraclesAreIndependentOfFixtureCounts()
    {
        var projection = Projection();
        var expected = new[]
        {
            (Id: "lib-a", A: "a-complete-20261019", B: "fictif-2026-10-20",
                Coverage: "INCOMPLETE", Denominator: 2, Unknown: 2, Added: 0, Changed: 0,
                Unchanged: 0, NotObserved: 0, NotComparable: 0),
            (Id: "lib-b", A: "b-complete-20261017", B: "fictif-2026-10-18",
                Coverage: "COMPLETE", Denominator: 2, Unknown: 0, Added: 0, Changed: 0,
                Unchanged: 1, NotObserved: 0, NotComparable: 1),
            (Id: "lib-c", A: "c-complete-20261021", B: "fictif-2026-10-22",
                Coverage: "COMPLETE", Denominator: 4, Unknown: 0, Added: 1, Changed: 1,
                Unchanged: 1, NotObserved: 1, NotComparable: 0)
        };
        foreach (var oracle in expected)
        {
            var comparison = projection.FindComparison(oracle.Id, oracle.A, oracle.B)!;
            Assert.IsNotNull(comparison);
            Assert.AreEqual(oracle.Coverage, comparison.CoverageStatus);
            Assert.AreEqual(oracle.Denominator, comparison.CountDenominator);
            Assert.AreEqual(oracle.Unknown, comparison.Counts["UNKNOWN"]);
            Assert.AreEqual(oracle.Added, comparison.Counts["ADDED"]);
            Assert.AreEqual(oracle.Changed, comparison.Counts["CHANGED"]);
            Assert.AreEqual(oracle.Unchanged, comparison.Counts["UNCHANGED"]);
            Assert.AreEqual(oracle.NotObserved,
                comparison.Counts["NOT_OBSERVED_IN_B_COVERED_SCOPE"]);
            Assert.AreEqual(oracle.NotComparable, comparison.Counts["NOT_COMPARABLE"]);
        }
        CollectionAssert.AreEquivalent(new[] {
            "unit-syn-001:UNKNOWN", "unit-syn-002:UNKNOWN"
        }, projection.Comparisons[0].Entries.Select(e => e.RomUnitId + ":" + e.Classification).ToArray());
        CollectionAssert.AreEquivalent(new[] {
            "unit-syn-003:UNCHANGED", "unit-syn-004:NOT_COMPARABLE"
        }, projection.Comparisons[1].Entries.Select(e => e.RomUnitId + ":" + e.Classification).ToArray());
        CollectionAssert.AreEquivalent(new[] {
            "unit-syn-005:CHANGED", "unit-syn-006:UNCHANGED",
            "unit-history-new:ADDED", "unit-history-old:NOT_OBSERVED_IN_B_COVERED_SCOPE"
        }, projection.Comparisons[2].Entries.Select(e => e.RomUnitId + ":" + e.Classification).ToArray());
    }

    private static IEnumerable<string> VisibleText(Control control)
    {
        if (control is TextBlock text)
            yield return text.Text ?? "";
        if (control is Panel panel)
            foreach (var child in panel.Children)
                foreach (var value in VisibleText(child))
                    yield return value;
        else if (control is Border border && border.Child is Control borderChild)
            foreach (var value in VisibleText(borderChild))
                yield return value;
        else if (control is ContentControl content && content.Content is Control contentChild)
            foreach (var value in VisibleText(contentChild))
                yield return value;
    }

    [TestMethod]
    public void InaccessibleIndexRendersNoConsultableHistoryOrCount()
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
        var state = new LibraryViewState(projection);
        var page = LibraryPage.Build(projection, state, () => { }, compact: true);
        var labels = string.Join("\n", VisibleText(page));
        Assert.IsTrue(labels.Contains("historique détaillé indisponible", StringComparison.Ordinal));
        Assert.IsTrue(labels.Contains("compteur INCONNU", StringComparison.Ordinal));
        Assert.IsTrue(labels.Contains("Référence mémorisée", StringComparison.Ordinal));
        Assert.IsFalse(labels.Contains("Historique des snapshots", StringComparison.Ordinal));
        Assert.IsFalse(labels.Contains("Génération :", StringComparison.Ordinal));
        Assert.IsFalse(labels.Contains("Unités observées : 0", StringComparison.Ordinal));
    }

    [TestMethod]
    public void FailedAttemptCannotReplaceLastVerifiedComplete()
    {
        var node = Fixture();
        node["registry"]!["libraries"]!.AsArray()[0]!["generations"]!
            .AsArray()[1]!["scanStatus"] = JsonValue.Create("failed");
        node["comparisons"]!.AsArray()[0]!["after"]!["scanStatus"] =
            JsonValue.Create("failed");
        var projection = LibraryProjection.FromJson(node.ToJsonString());
        Assert.AreEqual("a-complete-20261019", projection.FindLibrary("lib-a")!.LastVerifiedCompleteRef);
        Assert.AreEqual("failed", projection.FindLibrary("lib-a")!.Generations.Last().ScanStatus);
    }

    [TestMethod]
    public void ComparisonUnitIdsAreOrdinalAndUnique()
    {
        var caseDistinct = Fixture();
        caseDistinct["comparisons"]!.AsArray()[2]!["entries"]!.AsArray()[0]!["romUnitId"] =
            JsonValue.Create("Unit-A");
        caseDistinct["comparisons"]!.AsArray()[2]!["entries"]!.AsArray()[1]!["romUnitId"] =
            JsonValue.Create("unit-a");
        Assert.IsNotNull(LibraryProjection.FromJson(caseDistinct.ToJsonString()));

        var duplicate = Fixture();
        duplicate["comparisons"]!.AsArray()[2]!["entries"]!.AsArray()[1]!["romUnitId"] =
            JsonValue.Create("unit-syn-005");
        Reject(duplicate);
    }

}
