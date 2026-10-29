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
    public void DemoIsDefaultAndImportRequiresExplicitSelection()
    {
        var state = new ImportedCatalogueUiState();
        Assert.AreEqual(CatalogueMode.Demonstration, state.Mode);
        Assert.IsFalse(state.IsImported);
        Assert.IsFalse(state.ShowImported());
        Assert.IsNotNull(state.ErrorMessage);
        Assert.IsNull(state.Snapshot);
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

}
