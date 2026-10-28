using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;
using RomothequeManager.V2.Inventory;

namespace RomothequeManager.V2.Tests;

// DEMO-LIBRARY-01: invokes Node only on self-created temp fixtures; no real library path.
[TestClass]
public sealed class DemoLibraryGeneratorTests
{
    private static string _repo = "";
    private static string _temporary = "";
    private static string _first = "";
    private static string _second = "";

    [ClassInitialize]
    public static void Setup(TestContext _)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null &&
               !File.Exists(Path.Combine(directory.FullName, "tools", "demo-library", "generate.cjs")))
            directory = directory.Parent;
        Assert.IsNotNull(directory, "Repository generator source must be available on the Windows CI runner.");
        _repo = directory.FullName;
        _temporary = Path.Combine(Path.GetTempPath(), "demo01-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporary);
        _first = Path.Combine(_temporary, "generated-a");
        _second = Path.Combine(_temporary, "generated-b");
        CallNode(0, "--output", _first);
        CallNode(0, "--verify", _first);
    }

    [ClassCleanup]
    public static void Cleanup()
    {
        // Only our uniquely named, self-created synthetic test directory is removed.
        if (Directory.Exists(_temporary)) Directory.Delete(_temporary, recursive: true);
    }

    private static string CallNode(int expectedCode, params string[] args)
    {
        var info = new ProcessStartInfo("node")
        {
            WorkingDirectory = _repo, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        info.ArgumentList.Add(Path.Combine(_repo, "tools", "demo-library", "generate.cjs"));
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process = Process.Start(info);
        Assert.IsNotNull(process, "Node.js 20+ is required for DEMO-01 in the CI environment.");
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            process.Kill(entireProcessTree: true);
            Assert.Fail("Synthetic fixture generator exceeded the 120-second test budget.");
        }
        Assert.AreEqual(expectedCode, process.ExitCode,
            "Generator exit mismatch. stdout=" + stdout + " stderr=" + stderr);
        return stdout + stderr;
    }

    private static JsonDocument Manifest(string variant) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(_first, variant, "manifest.json")));

    [TestMethod]
    public void Demo01_GeneratedExactlyThirtyStableIdentitiesPerVariant()
    {
        var sets = new List<string[]>();
        foreach (var variant in new[] { "IDEAL", "CHAOS", "MYSTERY" })
        {
            using var doc = Manifest(variant);
            var games = doc.RootElement.GetProperty("games").EnumerateArray().ToArray();
            Assert.AreEqual(30, games.Length);
            var ids = games.Select(x => x.GetProperty("id").GetString()!).OrderBy(x => x).ToArray();
            Assert.AreEqual(30, ids.Distinct(StringComparer.Ordinal).Count());
            sets.Add(ids);
        }
        CollectionAssert.AreEqual(sets[0], sets[1]);
        CollectionAssert.AreEqual(sets[1], sets[2]);
    }

    [TestMethod]
    public void Demo01_IdealEsDeImportsAllGamesAndOriginalPngMedia()
    {
        var root = Path.Combine(_first, "IDEAL");
        var session = new EsDeImportSession(root);
        var snapshot = session.Import();
        Assert.AreEqual(30, snapshot.Games.Count);
        Assert.AreEqual(30, snapshot.Games.Select(g => g.GameId).Distinct().Count());
        Assert.IsTrue(snapshot.Games.All(g => g.RomExists));
        Assert.IsTrue(snapshot.Games.All(g => g.Media.Count == 3 && g.Media.All(m => m.Exists)));
        Assert.IsTrue(snapshot.Games.Any(g => g.DiscPaths.Count == 2));
        Assert.AreEqual(2, snapshot.Games.Count(g => g.Title == "Average Anto"));
        Assert.AreEqual(2, snapshot.Games.Count(g => g.Title == "SacMon : Titane Damas"));
        var image = snapshot.Games.First().Media.First(m => m.Kind == "image");
        Assert.IsTrue(session.TryOpenMedia(image, out var stream));
        Assert.IsNotNull(stream);
        using (stream)
        {
            var header = new byte[8];
            Assert.AreEqual(8, stream.Read(header));
            CollectionAssert.AreEqual(Convert.FromHexString("89504e470d0a1a0a"), header);
        }
        Assert.AreEqual(0, snapshot.Diagnostics.Count(x => x.Code is "MEDIA_MISSING" or "ROM_MISSING"));
    }

    [TestMethod]
    public void Demo01_ChaosIsImportableAndReportsControlledMissingSources()
    {
        var root = Path.Combine(_first, "CHAOS");
        var result = EsDeCatalogueImporter.Read(root);
        Assert.AreEqual(30, result.Games.Count);
        Assert.AreEqual(2, result.Games.Count(g => !g.RomExists));
        Assert.IsTrue(result.Diagnostics.Any(x => x.Code == "MEDIA_MISSING"));
        Assert.IsTrue(result.Diagnostics.Any(x => x.Code == "FOLDER_ENTRY"));
        var scan = new InventorySession(new InventoryScope(root)).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, scan.ScanStatus);
        Assert.IsNull(scan.ConfirmedGameCount);
        using var doc = Manifest("CHAOS");
        var expected = doc.RootElement.GetProperty("expected");
        Assert.AreEqual(expected.GetProperty("observedFileCount").GetInt32(),
            scan.ObservedFileCount);
        Assert.IsTrue(expected.GetProperty("duplicateCandidateFiles").GetInt32() > 0);
    }

    [TestMethod]
    public void Demo01_MysteryIsInventoriableWithoutAnyEsDeXml()
    {
        var root = Path.Combine(_first, "MYSTERY");
        Assert.IsFalse(Directory.Exists(Path.Combine(root, "MEDIAS")));
        Assert.ThrowsExactly<InvalidDataException>(() => EsDeCatalogueImporter.Read(root));
        var inventory = new InventorySession(new InventoryScope(root)).Scan();
        Assert.AreEqual(InventoryScanStatus.Complete, inventory.ScanStatus);
        using var doc = Manifest("MYSTERY");
        Assert.AreEqual(doc.RootElement.GetProperty("expected").GetProperty("observedFileCount").GetInt32(),
            inventory.ObservedFileCount);
        Assert.IsNull(inventory.ConfirmedGameCount);
        Assert.IsTrue(inventory.Observations.All(o => o.SourceKind == "FILESYSTEM_OBSERVATION"));
    }

    [TestMethod]
    public void Demo01_FilesAreDeterministicAndHashesCanBeVerified()
    {
        CallNode(0, "--output", _second);
        CallNode(0, "--verify", _second);
        var a = SHA256.HashData(File.ReadAllBytes(Path.Combine(_first, "manifest.json")));
        var b = SHA256.HashData(File.ReadAllBytes(Path.Combine(_second, "manifest.json")));
        CollectionAssert.AreEqual(a, b, "Output must not depend on the location or wall clock.");
        foreach (var variant in new[] { "IDEAL", "CHAOS", "MYSTERY" })
        {
            CollectionAssert.AreEqual(
                SHA256.HashData(File.ReadAllBytes(Path.Combine(_first, variant, "manifest.json"))),
                SHA256.HashData(File.ReadAllBytes(Path.Combine(_second, variant, "manifest.json"))));
        }
    }

    [TestMethod]
    public void Demo01_ExistingOutputIsRejectedWithoutMutation()
    {
        var before = SHA256.HashData(File.ReadAllBytes(Path.Combine(_first, "manifest.json")));
        var output = CallNode(1, "--output", _first);
        StringAssert.Contains(output, "Output must not exist");
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(
            Path.Combine(_first, "manifest.json"))));
    }

    [TestMethod]
    public void Demo01_AbsoluteOutputAndValidatedCatalogueAreMandatory()
    {
        CallNode(1, "--output", "relative-folder");
        var invalid = Path.Combine(_temporary, "invalid-catalogue.json");
        File.WriteAllText(invalid, """
          {"schemaVersion":"demo-library-01/1","games":[{"id":"demo01-001","romFile":"../../escape.exe"}]}
          """);
        var destination = Path.Combine(_temporary, "never-created");
        CallNode(1, "--output", destination, "--catalog", invalid);
        Assert.IsFalse(Directory.Exists(destination));
    }

    [TestMethod]
    public void Demo01_OnlySimulatedNonExecutableFilesAndVerifiablePngMedia()
    {
        foreach (var variant in new[] { "IDEAL", "CHAOS", "MYSTERY" })
        {
            using var doc = Manifest(variant);
            foreach (var f in doc.RootElement.GetProperty("files").EnumerateArray())
            {
                var relative = f.GetProperty("path").GetString()!;
                var kind = f.GetProperty("kind").GetString()!;
                var full = Path.Combine(_first, variant, relative.Replace('/', Path.DirectorySeparatorChar));
                var bytes = File.ReadAllBytes(full);
                Assert.AreEqual(f.GetProperty("byteLength").GetInt32(), bytes.Length);
                Assert.AreEqual(f.GetProperty("sha256").GetString(),
                    Convert.ToHexStringLower(SHA256.HashData(bytes)));
                if (kind == "synthetic-rom" || kind == "synthetic-disc")
                    StringAssert.StartsWith(Encoding.UTF8.GetString(bytes),
                        "DEMO01_SIMULATED_NON_EXECUTABLE_NO_ROM_OR_BIOS_CONTENT");
                if (kind.StartsWith("media-", StringComparison.Ordinal))
                    CollectionAssert.AreEqual(Convert.FromHexString("89504e470d0a1a0a"),
                        bytes.Take(8).ToArray());
                Assert.IsFalse(relative.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
                Assert.IsFalse(relative.EndsWith(".bios", StringComparison.OrdinalIgnoreCase));
            }
        }
    }
}
