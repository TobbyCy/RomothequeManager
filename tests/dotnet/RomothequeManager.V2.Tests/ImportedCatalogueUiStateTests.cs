using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

// NEXT-01-UX: no private collection, third-party media, or actual ROM content.
[TestClass]
public sealed class ImportedCatalogueUiStateTests
{
    private string _root = "";

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "rm-next01ux-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "gba"));
        Directory.CreateDirectory(Path.Combine(_root, "MEDIAS", "gba", "metadata"));
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private string Put(string relative, string content = "invented fixture")
    {
        var full = Path.Combine(_root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    private void Gamelist(string entries) =>
        Put("MEDIAS/gba/metadata/es-de-gamelist.xml", "<gameList>" + entries + "</gameList>");

    [TestMethod]
    public void NoCatalogIsDefaultAndDemoRequiresExplicitChoice()
    {
        var state = new ImportedCatalogueUiState();
        Assert.AreEqual(CatalogueMode.NoCatalog, state.Mode);
        Assert.IsFalse(state.IsImported);
        Assert.IsFalse(state.HasImportedCatalogue);
        Assert.AreEqual(0, state.Systems.Count);
        Assert.AreEqual(0, state.TotalCount);
        Assert.AreEqual(0, state.PageGames.Count);
        Assert.IsNull(state.Snapshot);
        state.ShowDemo();
        Assert.AreEqual(CatalogueMode.Demonstration, state.Mode);
        state.ShowWelcome();
        Assert.AreEqual(CatalogueMode.NoCatalog, state.Mode);
        Assert.IsFalse(state.ShowImported());
        Assert.IsNotNull(state.ErrorMessage);
    }

    [TestMethod]
    public void ImportedCatalogPreservesHomonymsAndSeparateIdentity()
    {
        Put("ROMS/gba/A.gba");
        Put("ROMS/gba/B.gba");
        Gamelist("""
            <game><path>./A.gba</path><name>Same Title</name></game>
            <game><path>./B.gba</path><name>Same Title</name></game>
            """);
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.IsTrue(state.IsImported);
        Assert.AreEqual("ES_DE_READ_ONLY", state.Snapshot!.SourceKind);
        Assert.AreEqual(2, state.TotalCount);
        Assert.AreNotEqual(state.PageGames[0].GameId, state.PageGames[1].GameId);
        Assert.IsTrue(state.Open(state.PageGames[1].GameId));
        Assert.AreEqual("B.gba", Path.GetFileName(state.Selected!.RomRelativePath));
        state.ShowDemo();
        Assert.IsFalse(state.IsImported);
        Assert.IsTrue(state.ShowImported());
        Assert.AreEqual(2, state.TotalCount);
    }

    [TestMethod]
    public void OptInDemoCanReturnToWelcomeWithoutLosingValidImport()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>Fiction</name></game>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var current = state.Snapshot;
        state.ShowDemo();
        Assert.AreEqual(CatalogueMode.Demonstration, state.Mode);
        state.ShowWelcome();
        Assert.AreEqual(CatalogueMode.NoCatalog, state.Mode);
        Assert.AreSame(current, state.Snapshot);
        Assert.IsTrue(state.ShowImported());
        Assert.AreSame(current, state.Snapshot);
        Assert.IsTrue(state.IsImported);
    }

    [TestMethod]
    public void SearchAndPageLimitBoundTwoThousandItems()
    {
        var xml = new StringBuilder();
        for (var index = 0; index < 2000; index++)
            xml.Append("<game><path>./G").Append(index.ToString("D3"))
                .Append(".gba</path><name>Game ").Append(index.ToString("D3")).Append("</name></game>");
        Gamelist(xml.ToString());
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.AreEqual(2000, state.TotalCount);
        Assert.AreEqual(50, state.PageGames.Count);
        Assert.AreEqual(40, state.PageCount);
        Assert.IsTrue(state.NextPage());
        Assert.AreEqual(1, state.PageIndex);
        Assert.IsTrue(state.PreviousPage());
        Assert.AreEqual(0, state.PageIndex);
        state.Search("Game 1999");
        Assert.AreEqual(1, state.TotalCount);
        Assert.AreEqual(1, state.PageGames.Count);
        Assert.AreEqual("Game 1999", state.PageGames[0].Title);
    }

    [TestMethod]
    public void FailedReimportKeepsLastCatalogAndSelection()
    {
        Put("ROMS/gba/First.gba");
        Gamelist("<game><path>./First.gba</path><name>First</name></game>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var accepted = state.Snapshot;
        Assert.IsTrue(state.Open(accepted!.Games[0].GameId));
        Gamelist("<game><path>broken");
        Assert.IsFalse(state.TryReimport());
        Assert.AreSame(accepted, state.Snapshot);
        Assert.AreEqual(accepted.Games[0].GameId, state.Selected!.GameId);
        Assert.IsTrue(state.IsImported);
        Assert.IsNotNull(state.ErrorMessage);
    }

    [TestMethod]
    public void SuccessfulReimportChangesFingerprintButKeepsSessionIdentity()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>A</name></game>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var before = state.Snapshot!;
        Put("ROMS/gba/B.gba");
        Gamelist("""
            <game><path>./A.gba</path><name>A</name></game>
            <game><path>./B.gba</path><name>B</name></game>
            """);
        Assert.IsTrue(state.TryReimport());
        Assert.AreEqual(before.LibraryId, state.Snapshot!.LibraryId);
        Assert.AreNotEqual(before.ContentFingerprint, state.Snapshot.ContentFingerprint);
        Assert.AreEqual(2, state.TotalCount);
    }

    [TestMethod]
    public void MediaFallbackCanUseReadOnlyStreamAndRejectMissingFile()
    {
        Put("ROMS/gba/A.gba");
        var cover = Put("MEDIAS/gba/covers/cover.png", "synthetic bytes (not a genuine image)");
        Gamelist("""
            <game><path>./A.gba</path><image>../covers/cover.png</image>
            <thumbnail>../covers/absent.png</thumbnail></game>
            """);
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var media = state.Snapshot!.Games[0].Media;
        Assert.IsTrue(state.TryOpenMedia(media[0], out var stream));
        using (stream) Assert.IsFalse(stream!.CanWrite);
        Assert.IsFalse(state.TryOpenMedia(media[1], out var missing));
        Assert.IsNull(missing);
        File.Delete(cover);
        Assert.IsFalse(state.TryOpenMedia(media[0], out var removed));
        Assert.IsNull(removed);
    }

    [TestMethod]
    public void SelectingAnotherDemoLibraryClearsAbComparisonAndError()
    {
        var projection = LibraryProjection.LoadSynthetic(ReadOnlyCatalogue.Synthetic());
        var state = new LibraryViewState(projection);
        Assert.IsNotNull(state.SelectedLibraryId);
        state.SetBefore("invalid-before");
        Assert.IsFalse(state.TryCompare());
        Assert.IsNotNull(state.ErrorMessage);
        var another = projection.Libraries.First(x => x.LibraryId != state.SelectedLibraryId);
        Assert.IsTrue(state.SelectLibrary(another.LibraryId));
        Assert.IsNull(state.ErrorMessage);
        Assert.IsNull(state.CurrentComparison);
    }
    [TestMethod]
    public void MissingGamelistIsHandledAsInvalidDataWithoutAnUnhandledImportFailure()
    {
        // The importer throws InvalidDataException for a root without a gamelist.
        // A user-selected empty folder must produce a visible refusal, not crash the UI.
        var state = new ImportedCatalogueUiState();
        Assert.IsFalse(state.TryImport(_root));
        Assert.IsFalse(state.IsImported);
        Assert.IsNull(state.Snapshot);
        Assert.IsTrue(state.ErrorMessage!.Contains("gamelist ES-DE", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void AmbiguousReimportPreservesPriorGameAndVisibleSelection()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>A</name></game>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var previous = state.Snapshot!;
        Assert.IsTrue(state.Open(previous.Games[0].GameId));
        // Two supported XML locations for one platform are deliberately ambiguous.
        Put("ROMS/gba/gamelist.xml", "<gameList></gameList>");
        Assert.IsFalse(state.TryReimport());
        Assert.AreSame(previous, state.Snapshot);
        Assert.AreEqual(previous.Games[0].GameId, state.Selected!.GameId);
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Assert.IsNotNull(state.ErrorMessage);
        File.Delete(Path.Combine(_root, "ROMS", "gba", "gamelist.xml"));
        Assert.IsTrue(state.TryReimport());
        Assert.IsNull(state.ErrorMessage);
    }

    [TestMethod]
    public void MissingPrimaryCoverFallsBackToReadOnlyThumbnailCandidate()
    {
        Put("ROMS/gba/A.gba");
        // Tiny self-contained synthetic PNG bytes: no third-party or user image.
        var thumbnail = Put("MEDIAS/gba/covers/thumb.png");
        File.WriteAllBytes(thumbnail, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9oYMNz8AAAAASUVORK5CYII="));
        Gamelist("""
            <game><path>./A.gba</path><name>A</name>
            <image>../covers/missing.png</image>
            <thumbnail>../covers/thumb.png</thumbnail></game>
            """);
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var game = state.Snapshot!.Games.Single();
        var candidates = ImportedCatalogueUiState.CoverCandidates(game);
        Assert.AreEqual(2, candidates.Count);
        Assert.AreEqual("image", candidates[0].Kind);
        Assert.AreEqual("thumbnail", candidates[1].Kind);
        Assert.IsFalse(state.TryOpenMedia(candidates[0], out var missing));
        Assert.IsNull(missing);
        Assert.IsTrue(state.TryOpenMedia(candidates[1], out var stream));
        using (stream)
        {
            Assert.IsTrue(stream!.CanRead);
            Assert.IsFalse(stream.CanWrite);
            var signature = new byte[8];
            stream.ReadExactly(signature);
            CollectionAssert.AreEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, signature);
        }
    }

    [TestMethod]
    public void ImportedNavigationAndDemoAbComparisonRemainIndependent()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>A</name></game>");
        var ui = new ImportedCatalogueUiState();
        Assert.IsTrue(ui.TryImport(_root));
        Assert.AreEqual(POCView.Dashboard, ui.CurrentView);
        ui.Navigate(POCView.GameDetail); // no selection: safe Collection fallback
        Assert.AreEqual(POCView.Collection, ui.CurrentView);
        Assert.IsTrue(ui.Open(ui.Snapshot!.Games.Single().GameId));
        Assert.AreEqual(POCView.GameDetail, ui.CurrentView);
        ui.Navigate(POCView.Libraries);
        Assert.AreEqual(POCView.Libraries, ui.CurrentView);
        Assert.IsTrue(ui.IsImported);

        var demo = new LibraryViewState(LibraryProjection.LoadSynthetic(ReadOnlyCatalogue.Synthetic()));
        Assert.IsTrue(demo.TryCompare());
        Assert.IsNotNull(demo.CurrentComparison);
        Assert.IsTrue(demo.SelectLibrary("lib-c"));
        Assert.IsNull(demo.CurrentComparison); // do not carry stale A/B across selection
        Assert.IsTrue(demo.TryCompare());
        ui.ShowDemo();
        Assert.IsFalse(ui.IsImported);
        Assert.AreEqual(POCView.Dashboard, ui.CurrentView);
        Assert.IsTrue(ui.ShowImported());
        Assert.AreEqual(POCView.Dashboard, ui.CurrentView);
        Assert.AreEqual(1, ui.TotalCount);
    }

    [TestMethod]
    public void FailedNewImportRetainsPreviouslyAcceptedCatalogue()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>A</name></game>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var accepted = state.Snapshot;
        Assert.IsFalse(state.TryImport(Path.Combine(_root, "missing-root")));
        Assert.AreSame(accepted, state.Snapshot);
        Assert.IsTrue(state.IsImported);
        Assert.IsNotNull(state.ErrorMessage);
    }


    [TestMethod]
    public void SyntheticImportDiagnosticsAreReadableAndPreserveSnapshotSemantics()
    {
        Gamelist("""
            <folder><path>./ignored</path></folder>
            <game><path>./Missing.gba</path><image>../covers/absent.png</image></game>
            """);
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.AreEqual(1, state.Snapshot!.Games.Count);
        var diagnostics = state.Snapshot.Diagnostics;
        Assert.AreEqual(4, diagnostics.Count); // folder, title fallback, ROM, cover
        Assert.AreEqual(2, diagnostics.Count(EsDeDiagnosticPresentation.NeedsAttention));
        Assert.IsTrue(EsDeDiagnosticPresentation.Summary(diagnostics).Contains("2 point(s) à vérifier",
            StringComparison.Ordinal));
        var rom = diagnostics.Single(d => d.Code == "ROM_MISSING");
        Assert.AreEqual("ROM indisponible", EsDeDiagnosticPresentation.Title(rom));
        Assert.IsTrue(EsDeDiagnosticPresentation.Context(rom).Contains("ROMS/gba/Missing.gba",
            StringComparison.Ordinal));
        Assert.IsTrue(EsDeDiagnosticPresentation.Explanation(rom).Contains("Aucune suppression",
            StringComparison.Ordinal));
        Assert.AreEqual("Média indisponible",
            EsDeDiagnosticPresentation.Title(diagnostics.Single(d => d.Code == "MEDIA_MISSING")));
        Assert.IsFalse(EsDeDiagnosticPresentation.NeedsAttention(
            diagnostics.Single(d => d.Code == "TITLE_FALLBACK")));
        Assert.IsFalse(state.Snapshot.Games.Single().RomExists);
    }

    [TestMethod]
    public void FailedReimportUsesActionableFrenchErrorAndRetainsValidSnapshot()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>A</name></game>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        var accepted = state.Snapshot;
        File.Delete(Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"));
        Assert.IsFalse(state.TryReimport());
        Assert.AreSame(accepted, state.Snapshot);
        Assert.IsTrue(state.IsImported);
        Assert.IsTrue(state.ErrorMessage!.Contains("gamelist ES-DE", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(state.ErrorMessage.Contains("Dernier catalogue valide conservé", StringComparison.Ordinal));
        Assert.IsTrue(state.ErrorMessage.Contains("Aucun fichier source modifié", StringComparison.Ordinal));
    }

    [TestMethod]
    public void InvalidXmlReportsReadableRefusalWithoutCreatingCatalogue()
    {
        Put("MEDIAS/gba/metadata/es-de-gamelist.xml", "<gameList><game><path>");
        var state = new ImportedCatalogueUiState();
        Assert.IsFalse(state.TryImport(_root));
        Assert.IsNull(state.Snapshot);
        Assert.IsTrue(state.ErrorMessage!.Contains("XML ES-DE n'a pas pu être analysé",
            StringComparison.Ordinal));
        Assert.IsTrue(state.ErrorMessage.Contains("Aucun catalogue chargé", StringComparison.Ordinal));
    }

    [TestMethod]
    public void UnknownDiagnosticCodeStillDisplaysCodeAndOriginalExplanation()
    {
        var unknown = new EsDeDiagnostic("FUTURE_WARNING", "gbc", "item", "Additional diagnostic");
        Assert.AreEqual("Diagnostic ES-DE (FUTURE_WARNING)", EsDeDiagnosticPresentation.Title(unknown));
        Assert.AreEqual("Additional diagnostic", EsDeDiagnosticPresentation.Explanation(unknown));
        Assert.IsTrue(EsDeDiagnosticPresentation.Context(unknown).Contains("gbc", StringComparison.Ordinal));
        Assert.IsFalse(EsDeDiagnosticPresentation.NeedsAttention(unknown));
    }


    [TestMethod]
    public void SystemsAreFromXmlOnlyAndSearchPaginatesWithinSelectedSystem()
    {
        var gba = new StringBuilder();
        for (var index = 0; index < 75; index++)
            gba.Append("<game><path>./G").Append(index).Append(".gba</path><name>GBA ")
                .Append(index).Append("</name></game>");
        Gamelist(gba.ToString());
        Put("ROMS/snes/Only.sfc");
        Put("MEDIAS/snes/metadata/es-de-gamelist.xml",
            "<gameList><game><path>./Only.sfc</path><name>SNES Only</name></game></gameList>");
        Directory.CreateDirectory(Path.Combine(_root, "ROMS", "empty"));
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.AreEqual(76, state.TotalCount);
        CollectionAssert.AreEquivalent(new[] { "gba", "snes" },
            state.Systems.Select(s => s.Platform).ToArray());
        Assert.AreEqual(75, state.Systems.Single(s => s.Platform == "gba").ReferencedGames);
        Assert.IsFalse(state.SelectPlatform("empty")); // no XML: not an invented zero-game system
        Assert.IsTrue(state.SelectPlatform("gba"));
        Assert.AreEqual(75, state.TotalCount);
        Assert.AreEqual(2, state.PageCount);
        Assert.AreEqual(50, state.PageGames.Count);
        Assert.IsTrue(state.NextPage());
        Assert.AreEqual(25, state.PageGames.Count);
        state.Search("GBA 74");
        Assert.AreEqual("gba", state.SelectedPlatform);
        Assert.AreEqual(1, state.TotalCount);
        Assert.AreEqual(0, state.PageIndex);
        Assert.IsTrue(state.SelectPlatform("snes"));
        state.Search("");
        Assert.AreEqual(1, state.TotalCount);
        Assert.AreEqual("SNES Only", state.PageGames.Single().Title);
        Assert.IsTrue(state.SelectPlatform(null));
        Assert.AreEqual(76, state.TotalCount);
    }

    [TestMethod]
    public void ReimportKeepsSystemSearchPageAndDetailOnSuccessOrFailure()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>Featured A</name></game>");
        Put("ROMS/snes/B.sfc");
        Put("MEDIAS/snes/metadata/es-de-gamelist.xml",
            "<gameList><game><path>./B.sfc</path><name>Featured B</name></game></gameList>");
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.IsTrue(state.SelectPlatform("gba"));
        state.Search("Featured");
        Assert.IsTrue(state.Open(state.PageGames.Single().GameId));
        var preservedId = state.SelectedGameId;
        var original = state.Snapshot;
        Assert.IsTrue(state.TryReimport());
        Assert.AreEqual(preservedId, state.SelectedGameId);
        Assert.AreEqual("gba", state.SelectedPlatform);
        Assert.AreEqual("Featured", state.SearchText);
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Gamelist("<gameList><game>"); // invalid: last valid and all UI context remain
        Assert.IsFalse(state.TryReimport());
        Assert.AreEqual(preservedId, state.Snapshot!.Games.Single(g => g.Platform == "gba").GameId);
        Assert.AreEqual(preservedId, state.SelectedGameId);
        Assert.AreEqual("gba", state.SelectedPlatform);
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Assert.IsNotNull(state.ErrorMessage);
        Gamelist("<game><path>./C.gba</path><name>Featured C</name></game>");
        Assert.IsTrue(state.TryReimport());
        Assert.IsNull(state.SelectedGameId);
        Assert.AreEqual(POCView.Collection, state.CurrentView);
        Assert.AreEqual("gba", state.SelectedPlatform);
        Assert.AreEqual("Featured", state.SearchText);
        Assert.AreNotSame(original, state.Snapshot);
        File.Delete(Path.Combine(_root, "MEDIAS", "gba", "metadata", "es-de-gamelist.xml"));
        Assert.IsTrue(state.TryReimport()); // SNES still has XML; no guessed GBA system
        Assert.IsNull(state.SelectedPlatform);
        Assert.AreEqual(1, state.Systems.Count);
    }

    [TestMethod]
    [DataRow("2024", "2024")]
    [DataRow("20240101", "1er janvier 2024")]
    [DataRow("2024-07-22", "22 juillet 2024")]
    [DataRow("20241231T235959", "31 décembre 2024 à 23:59:59")]
    [DataRow("", "Non renseigné")]
    public void ReleaseDatePresentationIsFrenchButNeverInventsPrecision(string raw, string expected)
    {
        Assert.AreEqual(expected, EsDeReleaseDatePresentation.Display(raw));
    }

    [TestMethod]
    public void ReleaseDatePresentationPreservesUnknownTextAndOriginalMetadata()
    {
        Assert.AreEqual("Non renseigné", EsDeReleaseDatePresentation.Display(null));
        Assert.AreEqual("UNKNOWN", EsDeReleaseDatePresentation.Display("UNKNOWN"));
        Put("ROMS/gba/A.gba");
        Gamelist("<game><path>./A.gba</path><name>Fiction</name>" +
            "<releasedate>20240507T135942</releasedate></game>");
        var ui = new ImportedCatalogueUiState();
        Assert.IsTrue(ui.TryImport(_root));
        var game = ui.Snapshot!.Games.Single();
        Assert.AreEqual("20240507T135942", game.ReleaseDate);
        Assert.AreEqual("7 mai 2024 à 13:59:42",
            EsDeReleaseDatePresentation.Display(game.ReleaseDate));
        Assert.AreEqual("20240507T135942", game.ReleaseDate);
    }

    [TestMethod]
    public void DiagnosticGroupsCountAllTypesWithoutDiscardingUnknownCodes()
    {
        var diagnostics = new[]
        {
            new EsDeDiagnostic("MEDIA_MISSING", "x", "a", "synthetic"),
            new EsDeDiagnostic("TITLE_FALLBACK", "x", "b", "synthetic"),
            new EsDeDiagnostic("MEDIA_MISSING", "x", "c", "synthetic"),
            new EsDeDiagnostic("UNKNOWN_TEST", "x", "d", "synthetic"),
            new EsDeDiagnostic("ROM_MISSING", "x", "e", "synthetic")
        };
        var types = EsDeDiagnosticPresentation.ByType(diagnostics);
        Assert.AreEqual(4, types.Count);
        Assert.AreEqual(diagnostics.Length, types.Sum(t => t.Count));
        Assert.AreEqual("MEDIA_MISSING", types[0].Code);
        Assert.AreEqual(2, types[0].Count);
        Assert.IsTrue(types[0].NeedsAttention);
        Assert.AreEqual(1, types.Single(t => t.Code == "UNKNOWN_TEST").Count);
        Assert.IsFalse(types.Single(t => t.Code == "UNKNOWN_TEST").NeedsAttention);
        Assert.AreEqual("Diagnostic ES-DE (UNKNOWN_TEST)",
            types.Single(t => t.Code == "UNKNOWN_TEST").Title);
    }

    [TestMethod]
    public void OptionalEsDeMetadataUsesOnlyDeclaredFieldsAndNonInventedFallback()
    {
        Put("ROMS/gba/A.gba");
        Gamelist("""
            <game><path>./A.gba</path><name>Alpha</name><genre>Exploration</genre>
            <developer>Invented Studio</developer><releasedate>20240101</releasedate></game>
            """);
        var ui = new ImportedCatalogueUiState();
        Assert.IsTrue(ui.TryImport(_root));
        var original = ui.Snapshot!;
        var game = original.Games.Single();
        Assert.AreEqual("Exploration", game.Genre);
        Assert.AreEqual("Invented Studio", game.Developer);
        Assert.AreEqual("20240101", game.ReleaseDate);
        Assert.AreEqual("Non renseigné", ImportedCatalogueUiState.MetadataOrUnspecified(null));
        Assert.AreEqual("Non renseigné", ImportedCatalogueUiState.MetadataOrUnspecified(""));
        Assert.AreEqual(game.Developer, ImportedCatalogueUiState.MetadataOrUnspecified(game.Developer));
        Assert.IsTrue(ui.SelectPlatform("gba"));
        Assert.IsTrue(ui.Open(game.GameId));
        Gamelist("""
            <game><path>./A.gba</path><name>Alpha</name>
            <genre>Strategy</genre></game>
            """);
        Assert.IsTrue(ui.TryReimport());
        var newer = ui.Snapshot!;
        Assert.AreEqual(original.LibraryId, newer.LibraryId);
        Assert.AreNotEqual(original.ContentFingerprint, newer.ContentFingerprint);
        Assert.AreEqual(game.GameId, newer.Games.Single().GameId);
        Assert.AreEqual(POCView.GameDetail, ui.CurrentView);
        Assert.AreEqual("gba", ui.SelectedPlatform);
        Assert.AreEqual("Strategy", ui.Selected!.Genre);
        Assert.IsNull(ui.Selected.Developer);
        Assert.IsNull(ui.Selected.ReleaseDate);
        Assert.AreEqual("Non renseigné", ImportedCatalogueUiState.MetadataOrUnspecified(ui.Selected.ReleaseDate));
    }

    [TestMethod]
    [DataRow("ESDE_XML_BYTES_LIMIT", "8 Mio")]
    [DataRow("ESDE_PLAYLIST_BYTES_LIMIT", "64 Kio")]
    [DataRow("ESDE_PLAYLIST_LINES_LIMIT", "256 lignes")]
    [DataRow("ESDE_PLAYLIST_ENCODING_UNSUPPORTED", "encodage")]
    [DataRow("ESDE_PLATFORM_CASE_COLLISION", "majuscules/minuscules")]
    [DataRow("Duplicated ROM identity in gba", "identité en conflit")]
    public void HardenedImportRejectionsRemainNeutralAndActionable(string code, string expected)
    {
        const string privateFixturePath = "C:\\PRIVATE-ROM-LIBRARY\\secret.gba";
        var result = EsDeDiagnosticPresentation.ImportFailure(
            new InvalidDataException(code));
        StringAssert.Contains(result, expected);
        Assert.IsFalse(result.Contains(privateFixturePath, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains("secret.gba", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(result.Contains(code, StringComparison.Ordinal),
            "The backend refusal token is not an end-user explanation.");
        Assert.IsFalse(result.Contains("malformé", StringComparison.OrdinalIgnoreCase),
            "An oversized XML is not necessarily malformed.");
    }

    [TestMethod]
    public void InvalidRootExplainsParentContainingRomsWithoutScanning()
    {
        var text = EsDeDiagnosticPresentation.ImportFailure(
            new DirectoryNotFoundException("C:\\PRIVATE-ROM-LIBRARY"));
        StringAssert.Contains(text, "parent contenant directement ROMS/");
        Assert.IsFalse(text.Contains("PRIVATE-ROM-LIBRARY", StringComparison.Ordinal));
        StringAssert.Contains(text, "Aucun scan automatique");
    }

    [TestMethod]
    public void HardenedOversizedXmlRetainsSameCurrentAndSelection()
    {
        Put("ROMS/gba/Valid.gba");
        Gamelist("<game><path>./Valid.gba</path><name>Valid</name></game>");
        var ui = new ImportedCatalogueUiState();
        Assert.IsTrue(ui.TryImport(_root));
        var current = ui.Snapshot;
        Assert.IsTrue(ui.Open(current!.Games.Single().GameId));
        var xml = new string('x', 8 * 1024 * 1024 + 1);
        Gamelist("<gameList>" + xml + "</gameList>");
        Assert.IsFalse(ui.TryReimport());
        Assert.AreSame(current, ui.Snapshot);
        Assert.AreEqual(POCView.GameDetail, ui.CurrentView);
        Assert.AreEqual(current.Games.Single().GameId, ui.SelectedGameId);
        StringAssert.Contains(ui.ErrorMessage!, "8 Mio");
        Assert.IsFalse(ui.ErrorMessage!.Contains(_root, StringComparison.Ordinal),
            "No absolute fixture root should appear in a refusal.");
    }

    [TestMethod]
    public void DeepPaginationFilterAndDetailSurviveReimportsAndInvalidXml()
    {
        var xml = new StringBuilder();
        for (var index = 0; index < 155; index++)
            xml.Append("<game><path>./Game").Append(index.ToString("D3"))
                .Append(".gba</path><name>Game ").Append(index.ToString("D3"))
                .Append("</name></game>");
        Gamelist(xml.ToString());
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.IsTrue(state.SelectPlatform("gba"));
        state.Search("Game ");
        Assert.IsTrue(state.NextPage());
        Assert.AreEqual(1, state.PageIndex);
        Assert.AreEqual(50, state.PageGames.Count);
        // Reattached native ComboBox/TextBox can publish their unchanged values.
        // That must never clear a page or selection during a Collection render.
        Assert.IsTrue(state.SelectPlatform("gba"));
        Assert.AreEqual(1, state.PageIndex);
        state.Search("Game ");
        Assert.AreEqual(1, state.PageIndex);
        var gameId = state.PageGames[0].GameId;
        Assert.IsTrue(state.Open(gameId));
        Assert.IsTrue(state.TryReimport());
        var valid = state.Snapshot;
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Assert.AreEqual(gameId, state.SelectedGameId);
        Assert.AreEqual(1, state.PageIndex);
        Assert.AreEqual("gba", state.SelectedPlatform);
        Assert.AreEqual("Game ", state.SearchText);
        Gamelist("<game><path>broken");
        Assert.IsFalse(state.TryReimport());
        Assert.AreSame(valid, state.Snapshot);
        Assert.AreEqual(POCView.GameDetail, state.CurrentView);
        Assert.AreEqual(gameId, state.SelectedGameId);
        Assert.AreEqual(1, state.PageIndex);
        Assert.AreEqual("Game ", state.SearchText);
        Gamelist(xml.ToString().Replace("<path>./Game050.gba</path><name>Game 050</name>", 
                                         "<path>./Game250.gba</path><name>Game 250</name>", StringComparison.Ordinal));
        Assert.IsTrue(state.TryReimport());
        Assert.IsNull(state.SelectedGameId);
        Assert.AreEqual(POCView.Collection, state.CurrentView);
        Assert.AreEqual(1, state.PageIndex);
        Assert.AreEqual("gba", state.SelectedPlatform);
        Assert.AreEqual("Game ", state.SearchText);
    }
}
