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
    public void SearchAndPageLimitBoundTwoHundredItems()
    {
        var xml = new StringBuilder();
        for (var index = 0; index < 200; index++)
            xml.Append("<game><path>./G").Append(index.ToString("D3"))
                .Append(".gba</path><name>Game ").Append(index.ToString("D3")).Append("</name></game>");
        Gamelist(xml.ToString());
        var state = new ImportedCatalogueUiState();
        Assert.IsTrue(state.TryImport(_root));
        Assert.AreEqual(200, state.TotalCount);
        Assert.AreEqual(50, state.PageGames.Count);
        Assert.AreEqual(4, state.PageCount);
        Assert.IsTrue(state.NextPage());
        Assert.AreEqual(1, state.PageIndex);
        Assert.IsTrue(state.PreviousPage());
        Assert.AreEqual(0, state.PageIndex);
        state.Search("Game 199");
        Assert.AreEqual(1, state.TotalCount);
        Assert.AreEqual(1, state.PageGames.Count);
        Assert.AreEqual("Game 199", state.PageGames[0].Title);
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
}
