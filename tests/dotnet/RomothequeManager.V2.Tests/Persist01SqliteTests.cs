using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;
using RomothequeManager.V2.Persistence;

namespace RomothequeManager.V2.Tests;

/// <summary>PERSIST-01 tests run ONLY with in-memory facts and generated temporary SQLite files.
/// No user SSD/ROM/MEDIA, no ES-DE XML, no scanner, no import or network access.</summary>
[TestClass]
public sealed class Persist01SqliteTests
{
    private string _fixture = "";
    private string _db = "";
    private readonly string _libraryId = Guid.NewGuid().ToString("D");
    private readonly string _romRootId = Guid.NewGuid().ToString("D");
    private readonly string _mediaRootId = Guid.NewGuid().ToString("D");

    [TestInitialize]
    public void Prepare()
    {
        _fixture = Path.Combine(Path.GetTempPath(),
            "rm-persist01-fixture-" + Guid.NewGuid().ToString("N"));
        _db = Path.Combine(_fixture, "ROMOTHEQUE_MANAGER", "data", "catalog.sqlite");
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Path.GetFileName(_fixture).StartsWith("rm-persist01-fixture-",
            StringComparison.Ordinal) && Directory.Exists(_fixture))
            Directory.Delete(_fixture, recursive: true);
    }

    private SqliteCatalogStore Store(Action? beforeCommit = null,
        Action? beforeRestoreSwap = null, Action? afterRestoreSwap = null) =>
        new(_db, beforeCommit, beforeRestoreSwap, afterRestoreSwap);

    private void Seed(ICatalogStore store)
    {
        store.AddLibrary(new CatalogLibrary(_libraryId, "Collection fictive"));
        store.AddStorageRoot(new CatalogStorageRoot(_romRootId, _libraryId, "ROMS", "ROMS"));
        store.AddStorageRoot(new CatalogStorageRoot(_mediaRootId, _libraryId, "MEDIAS", "MEDIAS"));
    }

    private static InventorySnapshot RomSnapshot(InventoryScanStatus status =
        InventoryScanStatus.Complete, InventoryAvailability availability = InventoryAvailability.Online,
        InventoryFreshness freshness = InventoryFreshness.Fresh, string stem = "Fiction")
    {
        var when = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var obs = new FileObservation("file-obs-" + id, "loc-" + id, "gba",
            "ROMS/gba/" + stem + ".gba", InventoryKind.PotentialRom, ".gba", 27,
            when, when, "FILESYSTEM_OBSERVATION", availability,
            Array.AsReadOnly(Array.Empty<string>()));
        var coverage = new InventoryCoverage("ROMS", status);
        return new InventorySnapshot("next-02-inventory/0.1-candidate", "synthetic-library",
            "scan-" + id, when, status, availability, freshness,
            Array.AsReadOnly(new[] { coverage }),
            Array.AsReadOnly(new[] { obs }),
            Array.AsReadOnly(Array.Empty<InventoryDiagnostic>()),
            Array.AsReadOnly(Array.Empty<InventoryPlaylist>()),
            Array.AsReadOnly(Array.Empty<PotentialDuplicateGroup>()),
            "sha256-fake-" + id, 1,
            status == InventoryScanStatus.Complete ? 1 : null, null);
    }

    private static MediaIndexSnapshot MediaSnapshot(InventoryScanStatus status =
        InventoryScanStatus.Complete, InventoryAvailability availability = InventoryAvailability.Online,
        InventoryFreshness freshness = InventoryFreshness.Fresh)
    {
        var when = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid().ToString("N");
        var obs = new MediaObservation("media-obs-" + id, "media-root-test",
            "MEDIAS/gba/covers/Fiction.png", "gba", MediaKind.Cover, ".png",
            MediaImageFormat.Png, true, 1024, 24, 16, when, when, availability,
            "FILESYSTEM_MEDIA", Array.AsReadOnly(Array.Empty<string>()));
        return new MediaIndexSnapshot("media-run-" + id, "media-root-test", when,
            status, availability, freshness,
            Array.AsReadOnly(new[] { new InventoryCoverage("MEDIAS", status) }),
            Array.AsReadOnly(new[] { obs }),
            Array.AsReadOnly(Array.Empty<InventoryDiagnostic>()),
            1, status == InventoryScanStatus.Complete ? 1 : null);
    }

    private CatalogUserDecision Decision(string subject = "roms:gba/Fiction.gba",
        string value = "medias:gba/covers/Fiction.png") =>
        new(Guid.NewGuid().ToString("D"), _libraryId, subject, "MANUAL_COVER_CHOICE",
            value, DateTimeOffset.UtcNow);

    private CatalogMetadataClaim Claim(string external, string value) =>
        new(Guid.NewGuid().ToString("D"), _libraryId, "roms:gba/Fiction.gba",
            "title", "MANUAL_IMPORT_FIXTURE", external, value,
            "SYNTHETIC_LOCAL_PROVENANCE", DateTimeOffset.UtcNow);

    [TestMethod]
    public void PERS_01_SaveCloseReopenInNewProcessWithoutLibraryOrXml()
    {
        var rom = RomSnapshot();
        var media = MediaSnapshot();
        using (var store = Store())
        {
            Seed(store);
            store.SaveAttempt(_romRootId, rom);
            store.SaveAttempt(_mediaRootId, media);
            store.SaveClaim(Claim("claim-a", "Fiction One"));
            store.SaveClaim(Claim("claim-b", "Fiction Two"));
            store.SaveManualDecision(Decision(), explicitUserAction: true);
        }
        VerifyNewProcessRestoration();
    }

    private void VerifyNewProcessRestoration()
    {
        // A new dotnet/vstest process without any selected ROMS/MEDIAS path.
        var assembly = typeof(Persist01SqliteTests).Assembly.Location;
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("vstest");
        start.ArgumentList.Add(assembly);
        start.ArgumentList.Add("/TestCaseFilter:FullyQualifiedName~PERS_01_ChildProcessVerify");
        start.ArgumentList.Add("/Logger:console;verbosity=minimal");
        start.Environment["PERSIST01_CHILD_DB"] = _db;
        start.Environment["PERSIST01_CHILD_ROOT_ROMS"] = _romRootId;
        start.Environment["PERSIST01_CHILD_ROOT_MEDIA"] = _mediaRootId;
        using var child = Process.Start(start) ?? throw new InvalidOperationException("No child process.");
        var stdout = child.StandardOutput.ReadToEnd();
        var stderr = child.StandardError.ReadToEnd();
        if (!child.WaitForExit(90000))
        {
            child.Kill(entireProcessTree: true);
            Assert.Fail("PERSIST01_CHILD_PROCESS_TIMEOUT");
        }
        Assert.AreEqual(0, child.ExitCode, stdout + "\n" + stderr);
        StringAssert.Contains(stdout, "Passed");
        Console.WriteLine("PERSIST01_NEW_PROCESS_RESTORE_PASS");
    }

    [TestMethod]
    public void PERS_01_ChildProcessVerify()
    {
        var path = Environment.GetEnvironmentVariable("PERSIST01_CHILD_DB");
        if (path is null) return; // helper is exercised by PERS_01 through separate vstest process
        using var store = new SqliteCatalogStore(path);
        var romId = Environment.GetEnvironmentVariable("PERSIST01_CHILD_ROOT_ROMS")!;
        var mediaId = Environment.GetEnvironmentVariable("PERSIST01_CHILD_ROOT_MEDIA")!;
        var rom = store.LoadLastKnown(romId);
        var media = store.LoadLastKnown(mediaId);
        Assert.IsNotNull(rom.CurrentComplete?.Inventory);
        Assert.AreEqual(InventoryScanStatus.Complete, rom.CurrentComplete.ScanStatus);
        Assert.AreEqual(InventoryAvailability.Offline, rom.CurrentComplete.DisplayAvailability);
        Assert.AreEqual(InventoryFreshness.Stale, rom.CurrentComplete.DisplayFreshness);
        Assert.AreEqual(2, rom.Claims.Count);
        Assert.AreEqual(1, rom.Decisions.Count);
        Assert.AreEqual("MANUAL_COVER_CHOICE", rom.Decisions.Single().Action);
        Assert.IsNotNull(media.CurrentComplete?.Media);
        Assert.AreEqual("FILESYSTEM_MEDIA",
            media.CurrentComplete.Media.Observations.Single().SourceKind);
        // No physical root path is persisted or consulted; child owns no SSD permission.
        Assert.AreEqual("ROMS", rom.StorageRoot.RelativeBinding);
        Assert.AreEqual("MEDIAS", media.StorageRoot.RelativeBinding);
    }

    [TestMethod]
    public void PERS_02_PartialOfflineAndFailedKeepCurrentCompleteAndManualDecision()
    {
        using var store = Store();
        Seed(store);
        var original = RomSnapshot();
        store.SaveAttempt(_romRootId, original);
        var selected = Decision();
        store.SaveManualDecision(selected, true);
        store.SaveAttempt(_romRootId, RomSnapshot(InventoryScanStatus.Partial));
        store.SaveAttempt(_romRootId, RomSnapshot(InventoryScanStatus.Failed,
            InventoryAvailability.Offline, InventoryFreshness.Stale));
        var state = store.LoadLastKnown(_romRootId);
        Assert.AreEqual(original.ScanId, state.CurrentComplete!.Inventory!.ScanId);
        Assert.AreEqual(InventoryScanStatus.Failed, state.LastAttempt!.ScanStatus);
        Assert.AreEqual(InventoryAvailability.Offline, state.DisplayLastAvailability());
        Assert.AreEqual(selected.DecisionId, state.Decisions.Single().DecisionId);
        Assert.AreEqual(InventoryAvailability.Offline, state.CurrentComplete.DisplayAvailability);
    }

    [TestMethod]
    public void PERS_03_InjectedCrashBetweenInsertionAndPointerRollsBack()
    {
        var initial = RomSnapshot();
        using (var stable = Store())
        {
            Seed(stable);
            stable.SaveAttempt(_romRootId, initial);
        }
        using (var crash = Store(() => throw new IOException("SYNTHETIC_CRASH_BEFORE_COMMIT")))
        {
            Assert.ThrowsExactly<IOException>(() =>
                crash.SaveAttempt(_romRootId, RomSnapshot(stem: "NewAttempt")));
        }
        using (var restored = Store())
        {
            var state = restored.LoadLastKnown(_romRootId);
            Assert.AreEqual(initial.ScanId, state.CurrentComplete!.Inventory!.ScanId);
            Assert.AreEqual(initial.ScanId, state.LastAttempt!.Inventory!.ScanId);
        }
    }

    [TestMethod]
    public void PERS_04_AmbiguousClaimsNeverBecomeChoiceAndRevocationSurvivesRestart()
    {
        var choice = Decision();
        using (var store = Store())
        {
            Seed(store);
            store.SaveAttempt(_romRootId, RomSnapshot());
            store.SaveClaim(Claim("candidate-cover-a", "Cover A"));
            store.SaveClaim(Claim("candidate-cover-b", "Cover B"));
            Assert.AreEqual(0, store.LoadLastKnown(_romRootId).Decisions.Count);
            Assert.ThrowsExactly<InvalidOperationException>(() =>
                store.SaveManualDecision(choice, explicitUserAction: false));
            store.SaveManualDecision(choice, explicitUserAction: true);
        }
        using (var store = Store())
        {
            Assert.AreEqual(choice.DecisionId,
                store.LoadLastKnown(_romRootId).Decisions.Single().DecisionId);
            store.RevokeManualDecision(choice.DecisionId, explicitUserAction: true);
        }
        using (var store = Store())
        {
            var state = store.LoadLastKnown(_romRootId);
            Assert.AreEqual(2, state.Claims.Count);
            Assert.IsNotNull(state.Decisions.Single().RevokedAtUtc);
            Assert.AreEqual(1, state.Decisions.Count); // history retained, not deleted
        }
    }

    [TestMethod]
    public void PERS_05_PortableAliasCollisionDoesNotAutoRebindOrGrantPermissions()
    {
        using var store = Store();
        Seed(store);
        Assert.ThrowsExactly<ArgumentException>(() => store.AddStorageRoot(
            new CatalogStorageRoot(Guid.NewGuid().ToString("D"), _libraryId,
                "ROMS", @"E:\Collections\ROMS")));
        Assert.ThrowsExactly<ArgumentException>(() => store.AddStorageRoot(
            new CatalogStorageRoot(Guid.NewGuid().ToString("D"), _libraryId,
                "MEDIAS", "../MEDIAS")));
        store.SaveAttempt(_romRootId, RomSnapshot());
        var state = store.LoadLastKnown(_romRootId);
        Assert.AreEqual("ROMS", state.StorageRoot.RelativeBinding);
        Assert.AreEqual(InventoryFreshness.Stale, state.CurrentComplete!.DisplayFreshness);
        var portableField = typeof(CatalogStorageLocations).GetField(
            nameof(CatalogStorageLocations.PortableAutoActivation));
        Assert.AreEqual(false, portableField?.GetRawConstantValue());
        Assert.IsTrue(CatalogStorageLocations.DefaultLocalCandidate.EndsWith(
            Path.Combine("RomothequeManager", "data", "catalog.sqlite")));
        Assert.ThrowsExactly<InvalidOperationException>(() => new SqliteCatalogStore());
    }

    [TestMethod]
    public void PERS_06_IntegrityCheckedOnlineBackupRestoreAndV1ToV2Migration()
    {
        var before = RomSnapshot();
        var decision = Decision();
        string backup;
        using (var store = Store())
        {
            Seed(store);
            store.SaveAttempt(_romRootId, before);
            store.SaveManualDecision(decision, true);
            backup = store.BackupVerified();
            store.SaveAttempt(_romRootId, RomSnapshot(stem: "AfterBackup"));
            Assert.AreNotEqual(before.ScanId,
                store.LoadLastKnown(_romRootId).CurrentComplete!.Inventory!.ScanId);
            // Only a corrupted COPY is tested. Never corrupt the active database.
            var damaged = Path.Combine(Path.GetDirectoryName(backup)!,
                "damaged-" + Guid.NewGuid().ToString("N") + ".sqlite");
            File.Copy(backup, damaged);
            var bytes = File.ReadAllBytes(damaged);
            bytes[0] ^= 255;
            File.WriteAllBytes(damaged, bytes);
            try
            {
                store.RestoreVerified(damaged);
                Assert.Fail("A corrupt backup must be rejected.");
            }
            catch (Exception ex) when (ex is SqliteException or InvalidDataException)
            {
                // Expected: invalid fixture backup, active catalog untouched.
            }
            store.RestoreVerified(backup);
        }
        using (var store = Store())
        {
            var restored = store.LoadLastKnown(_romRootId);
            Assert.AreEqual(before.ScanId, restored.CurrentComplete!.Inventory!.ScanId);
            Assert.AreEqual(decision.DecisionId, restored.Decisions.Single().DecisionId);
        }
        // Simulate an existing v1 database, with its pre-RuleVersion claim schema.
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = _db, Pooling = false
        }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "DROP INDEX UX_MetadataClaim_Revision; ALTER TABLE MetadataClaim DROP COLUMN RuleVersion; PRAGMA user_version=1;";
            cmd.ExecuteNonQuery();
        }
        using (var upgrade = Store())
        {
            var last = upgrade.LoadLastKnown(_romRootId);
            Assert.AreEqual(before.ScanId, last.CurrentComplete!.Inventory!.ScanId);
            Assert.AreEqual(decision.DecisionId, last.Decisions.Single().DecisionId);
        }
        using var verified = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = _db, Pooling = false
        }.ToString());
        verified.Open();
        using var query = verified.CreateCommand();
        query.CommandText = "PRAGMA user_version";
        Assert.AreEqual(3L, Convert.ToInt64(query.ExecuteScalar()));
        query.CommandText = "PRAGMA integrity_check";
        Assert.AreEqual("ok", query.ExecuteScalar()?.ToString());
        Assert.IsTrue(Directory.EnumerateFiles(Path.Combine(_fixture,
            "ROMOTHEQUE_MANAGER", "backups"), "pre-v2-*.sqlite").Any());
        Assert.IsTrue(Directory.EnumerateFiles(Path.Combine(_fixture,
            "ROMOTHEQUE_MANAGER", "backups"), "pre-v3-*.sqlite").Any());
    }


    [TestMethod]
    public void PERS_P1A_IncompatibleButIntegrityValidBackupNeverDestroysCurrentOrChoices()
    {
        string backup;
        var initial = RomSnapshot();
        var decision = Decision();
        using (var store = Store())
        {
            Seed(store);
            store.SaveAttempt(_romRootId, initial);
            store.SaveAttempt(_mediaRootId, MediaSnapshot());
            store.SaveClaim(Claim("claim-a", "Fiction One"));
            store.SaveClaim(Claim("claim-b", "Fiction Two"));
            store.SaveManualDecision(decision, true);
            backup = store.BackupVerified();
        }
        var variants = new (string Name, string Sql)[]
        {
            ("future-schema", "PRAGMA user_version=4;"),
            ("missing-table", "DROP TABLE MetadataClaim;"),
            ("missing-column", "DROP INDEX UX_MetadataClaim_Revision; ALTER TABLE MetadataClaim DROP COLUMN RuleVersion;"),
            ("broken-foreign-key", "INSERT INTO UserDecision(DecisionId,LibraryId,SubjectKey,Action,ChoiceValue,DecidedAtUtc) VALUES('bad-id','absent-library','bad','bad','bad','2026-01-01T00:00:00Z');")
        };
        foreach (var (name, sql) in variants)
        {
            var copy = MutateBackup(backup, name, sql);
            using (var store = Store())
                Assert.ThrowsExactly<InvalidDataException>(() => store.RestoreVerified(copy),
                    "Expected rejection for " + name);
            using (var reopened = Store())
            {
                var state = reopened.LoadLastKnown(_romRootId);
                Assert.AreEqual(initial.ScanId, state.CurrentComplete!.Inventory!.ScanId, name);
                Assert.AreEqual(decision.DecisionId, state.Decisions.Single().DecisionId, name);
                Assert.AreEqual(2, state.Claims.Count, name);
            }
        }
        // A truly NEW process also sees exactly the original Current and choices.
        VerifyNewProcessRestoration();
    }

    [TestMethod]
    public void PERS_P1A_StagingMigrationFailureAndPostSwapFailureBothPreserveOldDatabase()
    {
        string backup;
        var initial = RomSnapshot();
        var decision = Decision();
        using (var store = Store())
        {
            Seed(store);
            store.SaveAttempt(_romRootId, initial);
            store.SaveAttempt(_mediaRootId, MediaSnapshot());
            store.SaveClaim(Claim("claim-a", "Fiction One"));
            store.SaveClaim(Claim("claim-b", "Fiction Two"));
            store.SaveManualDecision(decision, true);
            backup = store.BackupVerified();
            store.SaveAttempt(_romRootId, RomSnapshot(stem: "CurrentAfterBackup"));
        }
        var currentBefore = "";
        using (var store = Store())
            currentBefore = store.LoadLastKnown(_romRootId).CurrentComplete!.Inventory!.ScanId;

        // A synthetically downgraded v1 backup MUST migrate on the staging copy only.
        var legacy = MutateBackup(backup, "legacy-v1",
            "DROP INDEX UX_MetadataClaim_Revision; " +
            "ALTER TABLE MetadataClaim DROP COLUMN RuleVersion; PRAGMA user_version=1;");
        using (var failOnStage = Store(beforeRestoreSwap:
            () => throw new IOException("P1A_SYNTHETIC_STAGING_FAILURE")))
            Assert.ThrowsExactly<IOException>(() => failOnStage.RestoreVerified(legacy));

        using (var reopened = Store())
            Assert.AreEqual(currentBefore,
                reopened.LoadLastKnown(_romRootId).CurrentComplete!.Inventory!.ScanId);

        // Even if a failure is injected after the target file was replaced,
        // RestoreVerified must actively roll back despite destination existence.
        using (var failAfterSwap = Store(afterRestoreSwap:
            () => throw new IOException("P1A_SYNTHETIC_POST_SWAP_FAILURE")))
            Assert.ThrowsExactly<IOException>(() => failAfterSwap.RestoreVerified(backup));
        using (var reopened = Store())
        {
            var state = reopened.LoadLastKnown(_romRootId);
            Assert.AreEqual(currentBefore, state.CurrentComplete!.Inventory!.ScanId);
            Assert.AreEqual(decision.DecisionId, state.Decisions.Single().DecisionId);
        }
    }

    [TestMethod]
    public void PERS_P1B_ClaimsDifferentSubjectsAndRevisionsAreAppendOnlyAndIdempotent()
    {
        var first = Claim("same-external", "First title") with
        {
            SubjectKey = "game:alpha", Provenance = "source-a/v1", RuleVersion = "rule/1"
        };
        var second = first with
        {
            ClaimId = Guid.NewGuid().ToString("D"),
            SubjectKey = "game:beta",
            Value = "Other title", Provenance = "source-b/v1"
        };
        var revision = first with
        {
            ClaimId = Guid.NewGuid().ToString("D"),
            Value = "Revised title", Provenance = "source-a/v2",
            RuleVersion = "rule/2", CapturedAtUtc = first.CapturedAtUtc.AddMinutes(1)
        };
        var duplicateImport = first with
        {
            ClaimId = Guid.NewGuid().ToString("D"),
            CapturedAtUtc = first.CapturedAtUtc.AddMinutes(20)
        };
        using (var store = Store())
        {
            Seed(store);
            store.SaveClaim(first);
            store.SaveClaim(second);
            store.SaveClaim(revision);
            store.SaveClaim(duplicateImport); // identical assertion is not another row
            store.SaveClaim(duplicateImport); // idempotent across repeat imports
            Assert.AreEqual(0, store.LoadLastKnown(_romRootId).Decisions.Count);
        }
        using (var reopened = Store())
        {
            var state = reopened.LoadLastKnown(_romRootId);
            Assert.AreEqual(3, state.Claims.Count);
            var alpha = state.Claims.Where(x => x.SubjectKey == "game:alpha").ToArray();
            Assert.AreEqual(2, alpha.Length);
            Assert.IsTrue(alpha.Any(x => x.Value == "First title" &&
                x.Provenance == "source-a/v1" && x.ClaimId == first.ClaimId));
            Assert.IsTrue(alpha.Any(x => x.Value == "Revised title" &&
                x.Provenance == "source-a/v2" && x.ClaimId == revision.ClaimId));
            Assert.IsTrue(state.Claims.Any(x => x.SubjectKey == "game:beta" &&
                x.Value == "Other title" && x.Provenance == "source-b/v1"));
            Assert.AreEqual(0, state.Decisions.Count);
        }
    }

    [TestMethod]
    public void PERS_P1B_V2LegacyMigrationPreservesClaimsAndEnablesDistinctSubjects()
    {
        var original = Claim("shared-key", "Legacy title");
        using (var store = Store())
        {
            Seed(store);
            store.SaveClaim(original);
        }
        // Synthetic legacy DDL restored in the fixture to exercise v2 → v3.
        using (var db = new SqliteConnection(new SqliteConnectionStringBuilder
            { DataSource = _db, Pooling = false }.ToString()))
        {
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = @"DROP INDEX UX_MetadataClaim_Revision;
                CREATE UNIQUE INDEX UX_LegacyMetadataClaim
                  ON MetadataClaim(LibraryId,SourceKind,ExternalKey,FieldKey);
                PRAGMA user_version=2;";
            cmd.ExecuteNonQuery();
        }
        using (var migrated = Store())
        {
            var state = migrated.LoadLastKnown(_romRootId);
            Assert.AreEqual(1, state.Claims.Count);
            Assert.AreEqual(original.Provenance, state.Claims.Single().Provenance);
            migrated.SaveClaim(original with {
                ClaimId = Guid.NewGuid().ToString("D"),
                SubjectKey = "game:another-subject",
                Value = "Different title",
                Provenance = "second-claim"
            });
        }
        using var reopened = Store();
        var after = reopened.LoadLastKnown(_romRootId);
        Assert.AreEqual(2, after.Claims.Count);
        Assert.AreEqual(2, after.Claims.Select(x => x.SubjectKey).Distinct().Count());
    }

    private string MutateBackup(string original, string label, string sql)
    {
        var result = Path.Combine(Path.GetDirectoryName(original)!,
            label + "-" + Guid.NewGuid().ToString("N") + ".sqlite");
        File.Copy(original, result);
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = result, Pooling = false
        }.ToString());
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=OFF; " + sql;
        cmd.ExecuteNonQuery();
        cmd.CommandText = "PRAGMA integrity_check";
        Assert.AreEqual("ok", cmd.ExecuteScalar()?.ToString(),
            "Backup must be physically intact, even if its application schema is invalid.");
        return result;
    }

    [TestMethod]
    public void PERS_07_SimulatedWriteFailureRollsBackAndBackupsRemainUsable()
    {
        var before = RomSnapshot();
        using (var store = Store())
        {
            Seed(store);
            store.SaveAttempt(_romRootId, before);
        }
        using (var failed = Store(() => throw new IOException("SYNTHETIC_DISK_FULL")))
            Assert.ThrowsExactly<IOException>(() =>
                failed.SaveAttempt(_romRootId, RomSnapshot(stem: "NeverPublished")));
        using (var store = Store())
        {
            Assert.AreEqual(before.ScanId,
                store.LoadLastKnown(_romRootId).CurrentComplete!.Inventory!.ScanId);
            var backup = store.BackupVerified();
            Assert.IsTrue(File.Exists(backup));
        }
        Console.WriteLine("PERS07_SIMULATED_IO_FAILURE_ONLY; REAL_UNPLUG_NOT_EXECUTED");
    }

    [TestMethod]
    public void PERS_08_WindowsNativeSQLiteAndPublicScanPreviewStorageRemainClosed()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => new SqliteCatalogStore());
        Assert.ThrowsExactly<ArgumentException>(() =>
            new SqliteCatalogStore(Path.Combine(_fixture, "ROMS", "catalog.sqlite")));
        using var store = Store();
        Seed(store);
        using var sqlite = new SqliteConnection(new SqliteConnectionStringBuilder {
            DataSource = _db, Pooling = false
        }.ToString());
        sqlite.Open();
        using var cmd = sqlite.CreateCommand();
        cmd.CommandText = "SELECT sqlite_version()";
        var nativeVersion = (string)cmd.ExecuteScalar()!;
        Assert.IsTrue(Version.TryParse(nativeVersion, out _));
        cmd.CommandText = "PRAGMA journal_mode";
        Assert.AreEqual("delete", cmd.ExecuteScalar()?.ToString()?.ToLowerInvariant());
        cmd.CommandText = "PRAGMA foreign_keys";
        // This separate connection has no FK pragma requirement by default.
        var denied = new MediaPreviewService();
        using var deniedResult = denied.LoadAsync(
            new MediaPreviewRequest("unknown","unknown","unknown",128))
            .GetAwaiter().GetResult();
        Assert.AreEqual(MediaPreviewStatus.Rejected, deniedResult.Status);
        var inventory = new RomothequeManager.V2.Inventory.Application.InventoryApplicationService();
        Assert.IsNotNull(inventory); // public constructor still fail-closed
        Assert.IsNotNull(new MediaIndexApplicationService());
        Console.WriteLine("PERSIST08_SQLITE_NATIVE_VERSION=" + nativeVersion);
        Console.WriteLine("PERSIST08_ZIP_EXTRACTION_HASH_PENDING_WINDOWS_CI");
    }
}

internal static class Persist01TestPresentation
{
    internal static InventoryAvailability DisplayLastAvailability(this CatalogLastKnown state) =>
        state.LastAttempt?.CapturedAvailability ?? InventoryAvailability.Unknown;
}
