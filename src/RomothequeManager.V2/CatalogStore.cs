using System.Text.Json;
using Microsoft.Data.Sqlite;
using RomothequeManager.V2.Inventory;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.Persistence;

/// <summary>Logical library identity; never a game inferred from a filename or XML.</summary>
public sealed record CatalogLibrary(string LibraryId, string Label);

/// <summary>Durable root identity without any drive letter, absolute path or scan permission.</summary>
public sealed record CatalogStorageRoot(
    string StorageRootId, string LibraryId, string Role, string RelativeBinding);

/// <summary>One sourced metadata assertion; never overrides a manual decision.</summary>
public sealed record CatalogMetadataClaim(
    string ClaimId, string LibraryId, string SubjectKey, string FieldKey,
    string SourceKind, string ExternalKey, string Value, string Provenance,
    DateTimeOffset CapturedAtUtc, string RuleVersion = "manual/1");

/// <summary>Manual-only revisionable decision, including history of revocation.</summary>
/// <remarks>A media candidate or EXACT_REFERENCE alone must never create this record.</remarks>
public sealed record CatalogUserDecision(
    string DecisionId, string LibraryId, string SubjectKey, string Action,
    string Value, DateTimeOffset DecidedAtUtc, string? SupersedesId = null,
    DateTimeOffset? RevokedAtUtc = null);

/// <summary>A saved fact with explicitly OFFLINE/STALE display state until source revalidation.</summary>
public sealed record CatalogSavedSnapshot(
    string SnapshotId, string StorageRootId, string SourceKind,
    string EphemeralRunId, DateTimeOffset CapturedAtUtc, InventoryScanStatus ScanStatus,
    InventoryAvailability CapturedAvailability, InventoryFreshness CapturedFreshness,
    InventorySnapshot? Inventory, MediaIndexSnapshot? Media)
{
    /// <summary>Never infer current physical presence from a restored database row.</summary>
    public InventoryAvailability DisplayAvailability => InventoryAvailability.Offline;
    /// <summary>Never claim a restored record is a fresh filesystem observation.</summary>
    public InventoryFreshness DisplayFreshness => InventoryFreshness.Stale;
}

/// <summary>Restored catalogue state without touching any ROM/MEDIAS filesystem.</summary>
public sealed record CatalogLastKnown(
    CatalogStorageRoot StorageRoot, CatalogSavedSnapshot? CurrentComplete,
    CatalogSavedSnapshot? LastAttempt,
    IReadOnlyList<CatalogMetadataClaim> Claims,
    IReadOnlyList<CatalogUserDecision> Decisions);

/// <summary>Minimal persisted application memory. Does not enable scanning or previewing.</summary>
public interface ICatalogStore : IDisposable
{
    /// <summary>Register one logical library, never infer a commercial game.</summary>
    void AddLibrary(CatalogLibrary library);

    /// <summary>Register a logical role and root, without binding a physical volume automatically.</summary>
    void AddStorageRoot(CatalogStorageRoot root);

    /// <summary>Atomically save a ROMS observation attempt and advance Current only if Complete.</summary>
    void SaveAttempt(string storageRootId, InventorySnapshot snapshot);

    /// <summary>Atomically save a MEDIAS observation attempt and advance Current only if Complete.</summary>
    void SaveAttempt(string storageRootId, MediaIndexSnapshot snapshot);

    /// <summary>Restore the last known state from SQLite only, without scanning or reading roots.</summary>
    CatalogLastKnown LoadLastKnown(string storageRootId);

    /// <summary>Idempotently record a sourced claim, never creating a user choice.</summary>
    void SaveClaim(CatalogMetadataClaim claim);

    /// <summary>Record a decision only on an explicit, separately confirmed user command.</summary>
    void SaveManualDecision(CatalogUserDecision decision, bool explicitUserAction);

    /// <summary>Revoke a decision without deleting its historical provenance.</summary>
    void RevokeManualDecision(string decisionId, bool explicitUserAction);

    /// <summary>Create a consistent, integrity-checked SQLite backup under the fixture data area.</summary>
    string BackupVerified();

    /// <summary>Restore a verified backup without contacting the selected library volume.</summary>
    void RestoreVerified(string backupFile);
}

/// <summary>Non-executing path policy; portable storage is never selected implicitly.</summary>
public static class CatalogStorageLocations
{
    /// <summary>Candidate for a future, explicitly authorized local application data store.</summary>
    public static string DefaultLocalCandidate => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "RomothequeManager", "data", "catalog.sqlite");

    /// <summary>Portable storage requires a future explicit user choice and separate GO.</summary>
    public const bool PortableAutoActivation = false;
}

/// <summary>SQLite-based fixture-only PERSIST-01 store with versioned manifests and atomic adoption.</summary>
/// <remarks>The public constructor rejects all storage I/O. Only the friend test assembly can
/// bind a generated rm-persist01-fixture-* directory under the OS temporary directory.
/// This component stores application facts; it cannot authorize REAL_* scans, previews or SSD access.</remarks>
public sealed class SqliteCatalogStore : ICatalogStore
{
    private const int SchemaVersion = 3;
    private const int ManifestVersion = 1;
    private const int MaxManifestBytes = 16 * 1024 * 1024;
    private const int MaxObservations = 30000;
    private readonly string _databasePath;
    private readonly string _fixtureRoot;
    private readonly object _gate = new();
    private readonly Action? _testBeforeCommit;
    private readonly Action? _testBeforeRestoreSwap;
    private readonly Action? _testAfterRestoreSwap;
    private bool _disposed;

    /// <summary>Fail closed: public application storage is not activated by this backend PR.</summary>
    public SqliteCatalogStore() => throw new InvalidOperationException("REAL_CATALOG_STORAGE_HOLD");

    /// <summary>Test-only path to a synthetic catalog under a specifically named temp fixture.</summary>
    internal SqliteCatalogStore(string databasePath, Action? testBeforeCommit = null,
        Action? testBeforeRestoreSwap = null, Action? testAfterRestoreSwap = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
        var full = Path.GetFullPath(databasePath);
        var folder = Path.GetDirectoryName(full)!;
        var application = Path.GetDirectoryName(folder)!;
        var fixture = Path.GetDirectoryName(application)!;
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var comparer = OperatingSystem.IsWindows() ?
            StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!string.Equals(Path.GetFileName(full), "catalog.sqlite", comparer) ||
            !string.Equals(Path.GetFileName(folder), "data", comparer) ||
            !string.Equals(Path.GetFileName(application), "ROMOTHEQUE_MANAGER", comparer) ||
            !Path.GetFileName(fixture).StartsWith("rm-persist01-fixture-", StringComparison.Ordinal) ||
            !string.Equals(Path.GetDirectoryName(fixture)?.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), temp, comparer))
            throw new ArgumentException("Only test-owned temporary SQLite catalog paths are allowed.");
        _databasePath = full;
        _fixtureRoot = fixture;
        _testBeforeCommit = testBeforeCommit;
        _testBeforeRestoreSwap = testBeforeRestoreSwap;
        _testAfterRestoreSwap = testAfterRestoreSwap;
        Directory.CreateDirectory(folder);
        if (AnyReparse(full, allowMissingFinal: true))
            throw new InvalidOperationException("PERSIST01_REPARSE_HOLD");
        lock (_gate) EnsureSchema();
    }

    /// <inheritdoc/>
    public void AddLibrary(CatalogLibrary library)
    {
        ArgumentNullException.ThrowIfNull(library);
        RequireUuid(library.LibraryId);
        if (string.IsNullOrWhiteSpace(library.Label) || library.Label.Length > 256)
            throw new ArgumentException("Invalid library label.");
        ExecuteWrite((db, tx) =>
            Exec(db, tx, "INSERT INTO Library(LibraryId,Label) VALUES(@id,@label)",
                ("@id", library.LibraryId), ("@label", library.Label)));
    }

    /// <inheritdoc/>
    public void AddStorageRoot(CatalogStorageRoot root)
    {
        ArgumentNullException.ThrowIfNull(root);
        RequireUuid(root.StorageRootId);
        RequireUuid(root.LibraryId);
        if (root.Role is not ("ROMS" or "MEDIAS") ||
            !string.Equals(root.RelativeBinding, root.Role, StringComparison.Ordinal))
            throw new ArgumentException("Only role-relative roots may be stored.");
        ExecuteWrite((db, tx) =>
            Exec(db, tx, "INSERT INTO StorageRoot(StorageRootId,LibraryId,Role,RelativeBinding) VALUES(@id,@lib,@role,@binding)",
                ("@id", root.StorageRootId), ("@lib", root.LibraryId),
                ("@role", root.Role), ("@binding", root.RelativeBinding)));
    }

    /// <inheritdoc/>
    public void SaveAttempt(string storageRootId, InventorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateRelativePaths(snapshot.Observations.Select(o => o.RelativePath));
        if (snapshot.Observations.Count > MaxObservations)
            throw new InvalidDataException("PERSIST01_OBSERVATION_LIMIT");
        SaveAttemptCore(storageRootId, "ROMS", snapshot.ScanId, snapshot.CapturedAtUtc,
            snapshot.ScanStatus, snapshot.Availability, snapshot.Freshness,
            snapshot.CoverageProof, snapshot.ObservedFileCount, snapshot.TotalFileCount,
            JsonSerializer.Serialize(new Manifest(ManifestVersion, "ROMS",
                JsonSerializer.Serialize(snapshot))));
    }

    /// <inheritdoc/>
    public void SaveAttempt(string storageRootId, MediaIndexSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateRelativePaths(snapshot.Observations.Select(o => o.RelativePath));
        if (snapshot.Observations.Count > MaxObservations)
            throw new InvalidDataException("PERSIST01_OBSERVATION_LIMIT");
        SaveAttemptCore(storageRootId, "MEDIAS", snapshot.RunId, snapshot.CapturedAtUtc,
            snapshot.ScanStatus, snapshot.Availability, snapshot.Freshness,
            snapshot.CoverageProof, snapshot.ObservedFileCount, snapshot.TotalFileCount,
            JsonSerializer.Serialize(new Manifest(ManifestVersion, "MEDIAS",
                JsonSerializer.Serialize(snapshot))));
    }

    private sealed record Manifest(int Version, string SourceKind, string SnapshotJson);

    private void SaveAttemptCore(string rootId, string role, string run,
        DateTimeOffset timestamp, InventoryScanStatus status, InventoryAvailability availability,
        InventoryFreshness freshness, IReadOnlyList<InventoryCoverage> coverage, int count,
        int? total, string payload)
    {
        RequireUuid(rootId);
        if (string.IsNullOrWhiteSpace(run) || run.Length > 256)
            throw new ArgumentException("Invalid transient run ID.");
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaxManifestBytes)
            throw new InvalidDataException("PERSIST01_MANIFEST_LIMIT");
        if (count < 0 || (total is < 0) || coverage.Count > 10000)
            throw new ArgumentException("Invalid coverage/count.");
        var complete = status == InventoryScanStatus.Complete &&
            availability == InventoryAvailability.Online &&
            freshness == InventoryFreshness.Fresh &&
            total.HasValue && total.Value == count &&
            coverage.Count > 0 && coverage.All(c => c.Status == InventoryScanStatus.Complete);
        // A Complete-labelled attempt with Offline/Stale or incomplete coverage
        // is retained as LastAttempt only; it must never overwrite Current.
        var id = Guid.NewGuid().ToString("D");
        ExecuteWrite((db, tx) =>
        {
            var actualRole = Scalar(db, tx,
                "SELECT Role FROM StorageRoot WHERE StorageRootId=@root", ("@root", rootId)) as string;
            if (actualRole != role) throw new InvalidOperationException("PERSIST01_ROOT_ROLE_MISMATCH");
            Exec(db, tx, @"INSERT INTO ScanSnapshot(
              SnapshotId,StorageRootId,SourceKind,RunRef,ScanStatus,Availability,Freshness,
              CapturedAtUtc,ManifestVersion,ManifestJson,ObservedCount,TotalCount)
              VALUES(@id,@root,@source,@run,@status,@avail,@fresh,@time,@version,@manifest,@count,@total)",
                ("@id", id), ("@root", rootId), ("@source", role), ("@run", run),
                ("@status", status.ToString()), ("@avail", availability.ToString()),
                ("@fresh", freshness.ToString()), ("@time", timestamp.ToString("O")),
                ("@version", ManifestVersion), ("@manifest", payload), ("@count", count),
                ("@total", (object?)total ?? DBNull.Value));
            Exec(db, tx, "UPDATE StorageRoot SET LastAttemptSnapshotId=@last WHERE StorageRootId=@root",
                ("@last", id), ("@root", rootId));
            if (complete)
                Exec(db, tx, "UPDATE StorageRoot SET CurrentCompleteSnapshotId=@current WHERE StorageRootId=@root",
                    ("@current", id), ("@root", rootId));
            _testBeforeCommit?.Invoke(); // deterministic exception rolls back both pointer and row
        });
    }

    /// <inheritdoc/>
    public CatalogLastKnown LoadLastKnown(string storageRootId)
    {
        RequireUuid(storageRootId);
        lock (_gate)
        {
            CheckDisposed();
            using var db = Open();
            using var q = Command(db, null,
                "SELECT StorageRootId,LibraryId,Role,RelativeBinding,CurrentCompleteSnapshotId,LastAttemptSnapshotId FROM StorageRoot WHERE StorageRootId=@id",
                ("@id", storageRootId));
            string? currentId, lastId;
            CatalogStorageRoot root;
            using (var reader = q.ExecuteReader())
            {
                if (!reader.Read()) throw new KeyNotFoundException("Unknown storage root.");
                root = new CatalogStorageRoot(reader.GetString(0), reader.GetString(1),
                    reader.GetString(2), reader.GetString(3));
                currentId = reader.IsDBNull(4) ? null : reader.GetString(4);
                lastId = reader.IsDBNull(5) ? null : reader.GetString(5);
            }
            var current = currentId is null ? null : ReadSnapshot(db, currentId, storageRootId);
            var last = lastId is null ? null : ReadSnapshot(db, lastId, storageRootId);
            if (current is { ScanStatus: not InventoryScanStatus.Complete })
                throw new InvalidDataException("PERSIST01_CURRENT_NOT_COMPLETE");
            var claims = new List<CatalogMetadataClaim>();
            using (var c = Command(db, null, @"SELECT ClaimId,LibraryId,SubjectKey,FieldKey,
                 SourceKind,ExternalKey,ClaimedValue,Provenance,CapturedAtUtc,RuleVersion
                 FROM MetadataClaim WHERE LibraryId=@id ORDER BY CapturedAtUtc,ClaimId", ("@id", root.LibraryId)))
            using (var rows = c.ExecuteReader())
                while (rows.Read())
                    claims.Add(new CatalogMetadataClaim(
                        rows.GetString(0), rows.GetString(1), rows.GetString(2),
                        rows.GetString(3), rows.GetString(4), rows.GetString(5),
                        rows.GetString(6), rows.GetString(7),
                        DateTimeOffset.Parse(rows.GetString(8),
                            System.Globalization.CultureInfo.InvariantCulture),
                        rows.GetString(9)));
            var decisions = new List<CatalogUserDecision>();
            using (var c = Command(db, null, @"SELECT DecisionId,LibraryId,SubjectKey,Action,
                 ChoiceValue,DecidedAtUtc,SupersedesId,RevokedAtUtc
                 FROM UserDecision WHERE LibraryId=@id ORDER BY DecidedAtUtc,DecisionId",
                 ("@id", root.LibraryId)))
            using (var rows = c.ExecuteReader())
                while (rows.Read())
                    decisions.Add(new CatalogUserDecision(
                        rows.GetString(0), rows.GetString(1), rows.GetString(2),
                        rows.GetString(3), rows.GetString(4),
                        DateTimeOffset.Parse(rows.GetString(5),
                            System.Globalization.CultureInfo.InvariantCulture),
                        rows.IsDBNull(6) ? null : rows.GetString(6),
                        rows.IsDBNull(7) ? null :
                            DateTimeOffset.Parse(rows.GetString(7),
                                System.Globalization.CultureInfo.InvariantCulture)));
            return new CatalogLastKnown(root, current, last,
                Array.AsReadOnly(claims.ToArray()), Array.AsReadOnly(decisions.ToArray()));
        }
    }

    private static CatalogSavedSnapshot ReadSnapshot(SqliteConnection db, string id, string rootId)
    {
        using var cmd = Command(db, null, @"SELECT SourceKind,RunRef,ScanStatus,Availability,Freshness,
            CapturedAtUtc,ManifestVersion,ManifestJson FROM ScanSnapshot
            WHERE SnapshotId=@id AND StorageRootId=@root", ("@id", id), ("@root", rootId));
        using var row = cmd.ExecuteReader();
        if (!row.Read()) throw new InvalidDataException("PERSIST01_MISSING_SNAPSHOT");
        var kind = row.GetString(0);
        var run = row.GetString(1);
        var status = Enum.Parse<InventoryScanStatus>(row.GetString(2), true);
        var availability = Enum.Parse<InventoryAvailability>(row.GetString(3), true);
        var freshness = Enum.Parse<InventoryFreshness>(row.GetString(4), true);
        var captured = DateTimeOffset.Parse(row.GetString(5),
            System.Globalization.CultureInfo.InvariantCulture);
        if (row.GetInt32(6) != ManifestVersion) throw new InvalidDataException("Unsupported manifest version.");
        var raw = row.GetString(7);
        if (System.Text.Encoding.UTF8.GetByteCount(raw) > MaxManifestBytes)
            throw new InvalidDataException("Oversized stored manifest.");
        var envelope = JsonSerializer.Deserialize<Manifest>(raw)
            ?? throw new InvalidDataException("PERSIST01_INVALID_MANIFEST");
        if (envelope.Version != ManifestVersion || envelope.SourceKind != kind)
            throw new InvalidDataException("PERSIST01_MANIFEST_KIND_MISMATCH");
        var inventory = kind == "ROMS" ?
            JsonSerializer.Deserialize<InventorySnapshot>(envelope.SnapshotJson) : null;
        var media = kind == "MEDIAS" ?
            JsonSerializer.Deserialize<MediaIndexSnapshot>(envelope.SnapshotJson) : null;
        if ((inventory is null) == (media is null))
            throw new InvalidDataException("PERSIST01_INVALID_SNAPSHOT_TYPE");
        // No source enumeration, XML, preview, volume binding or real filesystem access here.
        return new CatalogSavedSnapshot(id, rootId, kind, run, captured,
            status, availability, freshness, inventory, media);
    }

    /// <inheritdoc/>
    public void SaveClaim(CatalogMetadataClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        RequireUuid(claim.ClaimId);
        RequireUuid(claim.LibraryId);
        RequireText(claim.SubjectKey, 512);
        RequireText(claim.FieldKey, 128);
        RequireText(claim.SourceKind, 128);
        RequireText(claim.ExternalKey, 512);
        RequireText(claim.Value, 8192);
        RequireText(claim.Provenance, 512);
        RequireText(claim.RuleVersion, 128);
        // Immutable claims: exact reimports are idempotent; distinct subjects,
        // values, provenance or rule revisions retain separate historical rows.
        // CapturedAtUtc of the first identical assertion is retained.
        ExecuteWrite((db, tx) => Exec(db, tx, @"INSERT INTO MetadataClaim(
            ClaimId,LibraryId,SubjectKey,FieldKey,SourceKind,ExternalKey,ClaimedValue,
            Provenance,CapturedAtUtc,RuleVersion)
            VALUES(@id,@lib,@subject,@field,@source,@external,@value,@prov,@time,@rule)
            ON CONFLICT(LibraryId,SubjectKey,SourceKind,ExternalKey,FieldKey,
                        ClaimedValue,Provenance,RuleVersion) DO NOTHING",
            ("@id", claim.ClaimId), ("@lib", claim.LibraryId),
            ("@subject", claim.SubjectKey), ("@field", claim.FieldKey),
            ("@source", claim.SourceKind), ("@external", claim.ExternalKey),
            ("@value", claim.Value), ("@prov", claim.Provenance),
            ("@time", claim.CapturedAtUtc.ToString("O")), ("@rule", claim.RuleVersion)));
    }

    /// <inheritdoc/>
    public void SaveManualDecision(CatalogUserDecision decision, bool explicitUserAction)
    {
        if (!explicitUserAction)
            throw new InvalidOperationException("PERSIST01_EXPLICIT_USER_ACTION_REQUIRED");
        ArgumentNullException.ThrowIfNull(decision);
        RequireUuid(decision.DecisionId);
        RequireUuid(decision.LibraryId);
        RequireText(decision.SubjectKey, 512);
        RequireText(decision.Action, 128);
        RequireText(decision.Value, 2048);
        if (decision.RevokedAtUtc is not null)
            throw new ArgumentException("A new decision may not be pre-revoked.");
        if (decision.SupersedesId is not null) RequireUuid(decision.SupersedesId);
        ExecuteWrite((db, tx) =>
            Exec(db, tx, @"INSERT INTO UserDecision(DecisionId,LibraryId,SubjectKey,
              Action,ChoiceValue,DecidedAtUtc,SupersedesId) VALUES(
              @id,@lib,@subject,@action,@value,@time,@prev)",
                ("@id", decision.DecisionId), ("@lib", decision.LibraryId),
                ("@subject", decision.SubjectKey), ("@action", decision.Action),
                ("@value", decision.Value), ("@time", decision.DecidedAtUtc.ToString("O")),
                ("@prev", (object?)decision.SupersedesId ?? DBNull.Value)));
    }

    /// <inheritdoc/>
    public void RevokeManualDecision(string decisionId, bool explicitUserAction)
    {
        if (!explicitUserAction)
            throw new InvalidOperationException("PERSIST01_EXPLICIT_USER_ACTION_REQUIRED");
        RequireUuid(decisionId);
        ExecuteWrite((db, tx) =>
        {
            using var cmd = Command(db, tx,
                "UPDATE UserDecision SET RevokedAtUtc=@at WHERE DecisionId=@id AND RevokedAtUtc IS NULL",
                ("@at", DateTimeOffset.UtcNow.ToString("O")), ("@id", decisionId));
            if (cmd.ExecuteNonQuery() != 1)
                throw new InvalidOperationException("PERSIST01_DECISION_NOT_ACTIVE");
        });
    }

    /// <inheritdoc/>
    public string BackupVerified()
    {
        lock (_gate)
        {
            CheckDisposed();
            var folder = Path.Combine(_fixtureRoot, "ROMOTHEQUE_MANAGER", "backups");
            Directory.CreateDirectory(folder);
            var output = Path.Combine(folder, "catalog-" + Guid.NewGuid().ToString("N") + ".sqlite");
            CopyOnlineBackup(_databasePath, output);
            return output;
        }
    }

    /// <inheritdoc/>
    public void RestoreVerified(string backupFile)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(backupFile);
        lock (_gate)
        {
            CheckDisposed();
            var full = Path.GetFullPath(backupFile);
            var allowed = Path.Combine(_fixtureRoot, "ROMOTHEQUE_MANAGER", "backups") +
                Path.DirectorySeparatorChar;
            if (!full.StartsWith(allowed, OperatingSystem.IsWindows() ?
                StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                !string.Equals(Path.GetExtension(full), ".sqlite", StringComparison.OrdinalIgnoreCase) ||
                AnyReparse(full, false))
                throw new ArgumentException("PERSIST01_BACKUP_OUTSIDE_FIXTURE");

            // The active database is NEVER replaced until an incoming backup passes
            // integrity, FK, version, required-table/column and migration checks.
            CheckIntegrity(full);
            var staged = _databasePath + ".restoring";
            var safety = BackupVerified(); // coherent SQLite backup, not a WAL file copy
            bool swapped = false;
            try
            {
                if (File.Exists(staged)) File.Delete(staged);
                File.Copy(full, staged);
                ValidateCompatibleDatabase(staged, allowOlder: true);
                EnsureSchema(staged); // migrations run ONLY on the disposable staging copy
                ValidateCompatibleDatabase(staged, allowOlder: false);
                _testBeforeRestoreSwap?.Invoke();
                File.Move(staged, _databasePath, true);
                swapped = true;
                _testAfterRestoreSwap?.Invoke(); // synthetic failure proves post-swap rollback
                ValidateCompatibleDatabase(_databasePath, allowOlder: false);
            }
            catch (Exception restoreError)
            {
                try
                {
                    if (swapped)
                    {
                        // Restore even if the destination EXISTS: previous code silently
                        // left a damaged replacement at this point.
                        var rollback = _databasePath + ".rollback";
                        if (File.Exists(rollback)) File.Delete(rollback);
                        File.Copy(safety, rollback);
                        ValidateCompatibleDatabase(rollback, allowOlder: false);
                        File.Move(rollback, _databasePath, true);
                        ValidateCompatibleDatabase(_databasePath, allowOlder: false);
                    }
                }
                catch (Exception rollbackError)
                {
                    throw new AggregateException("PERSIST01_RESTORE_ROLLBACK_FAILED: safety backup retained",
                        restoreError, rollbackError);
                }
                finally
                {
                    if (File.Exists(staged)) File.Delete(staged);
                }
                throw;
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
            }
        }
    }

    private void EnsureSchema(string? overridePath = null)
    {
        var database = overridePath ?? _databasePath;
        using var db = OpenConnection(database, create: true);
        var version = Convert.ToInt32(Scalar(db, null, "PRAGMA user_version"));
        if (version < 0 || version > SchemaVersion)
            throw new InvalidDataException("PERSIST01_UNSUPPORTED_SCHEMA");
        if (version == 0)
        {
            using var tx = db.BeginTransaction();
            Exec(db, tx, @"CREATE TABLE Library(
                 LibraryId TEXT PRIMARY KEY, Label TEXT NOT NULL);
                 CREATE TABLE StorageRoot(
                 StorageRootId TEXT PRIMARY KEY,
                 LibraryId TEXT NOT NULL REFERENCES Library(LibraryId),
                 Role TEXT NOT NULL CHECK(Role IN ('ROMS','MEDIAS')),
                 RelativeBinding TEXT NOT NULL,
                 CurrentCompleteSnapshotId TEXT,
                 LastAttemptSnapshotId TEXT);
                 CREATE TABLE ScanSnapshot(
                 SnapshotId TEXT PRIMARY KEY,
                 StorageRootId TEXT NOT NULL REFERENCES StorageRoot(StorageRootId),
                 SourceKind TEXT NOT NULL CHECK(SourceKind IN ('ROMS','MEDIAS')),
                 RunRef TEXT NOT NULL, ScanStatus TEXT NOT NULL,
                 Availability TEXT NOT NULL, Freshness TEXT NOT NULL,
                 CapturedAtUtc TEXT NOT NULL, ManifestVersion INTEGER NOT NULL,
                 ManifestJson TEXT NOT NULL, ObservedCount INTEGER NOT NULL,
                 TotalCount INTEGER);
                 CREATE TABLE MetadataClaim(
                 ClaimId TEXT PRIMARY KEY,
                 LibraryId TEXT NOT NULL REFERENCES Library(LibraryId),
                 SubjectKey TEXT NOT NULL, FieldKey TEXT NOT NULL,
                 SourceKind TEXT NOT NULL, ExternalKey TEXT NOT NULL,
                 ClaimedValue TEXT NOT NULL, Provenance TEXT NOT NULL,
                 CapturedAtUtc TEXT NOT NULL,
                 UNIQUE(LibraryId,SourceKind,ExternalKey,FieldKey));
                 CREATE TABLE UserDecision(
                 DecisionId TEXT PRIMARY KEY,
                 LibraryId TEXT NOT NULL REFERENCES Library(LibraryId),
                 SubjectKey TEXT NOT NULL, Action TEXT NOT NULL,
                 ChoiceValue TEXT NOT NULL, DecidedAtUtc TEXT NOT NULL,
                 SupersedesId TEXT REFERENCES UserDecision(DecisionId),
                 RevokedAtUtc TEXT);
                 CREATE INDEX IX_ScanSnapshot_Root ON ScanSnapshot(StorageRootId,CapturedAtUtc);
                 PRAGMA user_version=1;");
            tx.Commit();
            version = 1;
        }
        if (version > 0) ValidateSchemaStructure(db, version);
        if (version == 1)
        {
            var before = Path.Combine(_fixtureRoot, "ROMOTHEQUE_MANAGER", "backups",
                "pre-v2-" + Guid.NewGuid().ToString("N") + ".sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(before)!);
            CopyOnlineBackup(database, before);
            using var tx = db.BeginTransaction();
            Exec(db, tx, @"ALTER TABLE MetadataClaim
                ADD COLUMN RuleVersion TEXT NOT NULL DEFAULT 'legacy/1';
                PRAGMA user_version=2;");
            tx.Commit();
            version = 2;
        }
        if (version == 2)
        {
            var before = Path.Combine(_fixtureRoot, "ROMOTHEQUE_MANAGER", "backups",
                "pre-v3-" + Guid.NewGuid().ToString("N") + ".sqlite");
            Directory.CreateDirectory(Path.GetDirectoryName(before)!);
            CopyOnlineBackup(database, before);
            using var tx = db.BeginTransaction();
            // Replace the old UNIQUE key that ignored SubjectKey and overwrote
            // provenance. Preserve each legacy claim by ID and source values.
            Exec(db, tx, @"CREATE TABLE MetadataClaim_v3(
                ClaimId TEXT PRIMARY KEY,
                LibraryId TEXT NOT NULL REFERENCES Library(LibraryId),
                SubjectKey TEXT NOT NULL, FieldKey TEXT NOT NULL,
                SourceKind TEXT NOT NULL, ExternalKey TEXT NOT NULL,
                ClaimedValue TEXT NOT NULL, Provenance TEXT NOT NULL,
                CapturedAtUtc TEXT NOT NULL, RuleVersion TEXT NOT NULL);
                INSERT INTO MetadataClaim_v3(
                    ClaimId,LibraryId,SubjectKey,FieldKey,SourceKind,ExternalKey,
                    ClaimedValue,Provenance,CapturedAtUtc,RuleVersion)
                SELECT ClaimId,LibraryId,SubjectKey,FieldKey,SourceKind,ExternalKey,
                    ClaimedValue,Provenance,CapturedAtUtc,RuleVersion FROM MetadataClaim;
                DROP TABLE MetadataClaim;
                ALTER TABLE MetadataClaim_v3 RENAME TO MetadataClaim;
                CREATE UNIQUE INDEX UX_MetadataClaim_Revision ON MetadataClaim(
                    LibraryId,SubjectKey,SourceKind,ExternalKey,FieldKey,
                    ClaimedValue,Provenance,RuleVersion);
                PRAGMA user_version=3;");
            tx.Commit();
            version = 3;
        }
        ValidateSchemaStructure(db, SchemaVersion);
        ValidateDatabaseIntegrity(db);
    }

    private static void CopyOnlineBackup(string source, string target)
    {
        var staged = target + ".part";
        if (File.Exists(staged)) File.Delete(staged);
        using (var from = OpenConnection(source, create: false))
        using (var to = OpenConnection(staged, create: true))
            from.BackupDatabase(to);
        try
        {
            CheckIntegrity(staged);
            File.Move(staged, target, true);
        }
        finally
        {
            if (File.Exists(staged)) File.Delete(staged);
        }
    }

    private static void CheckIntegrity(string path)
    {
        using var db = OpenConnection(path, create: false);
        ValidateDatabaseIntegrity(db);
    }

    private static void ValidateCompatibleDatabase(string path, bool allowOlder)
    {
        using var db = OpenConnection(path, create: false);
        ValidateDatabaseIntegrity(db);
        var version = Convert.ToInt32(Scalar(db, null, "PRAGMA user_version"));
        if (version < (allowOlder ? 1 : SchemaVersion) || version > SchemaVersion)
            throw new InvalidDataException("PERSIST01_UNSUPPORTED_BACKUP_SCHEMA");
        ValidateSchemaStructure(db, version);
    }

    private static void ValidateDatabaseIntegrity(SqliteConnection db)
    {
        if ((Scalar(db, null, "PRAGMA integrity_check") as string) != "ok")
            throw new InvalidDataException("PERSIST01_BACKUP_INTEGRITY_FAILED");
        using var foreignCheck = Command(db, null, "PRAGMA foreign_key_check");
        using var broken = foreignCheck.ExecuteReader();
        if (broken.Read()) throw new InvalidDataException("PERSIST01_FOREIGN_KEY_FAILED");
    }

    private static void ValidateSchemaStructure(SqliteConnection db, int version)
    {
        // Schema/version alone is not sufficient: reject a structurally valid
        // SQLite file that lacks the application tables or required columns.
        var required = new (string Table, string[] Columns)[]
        {
            ("Library", ["LibraryId", "Label"]),
            ("StorageRoot", ["StorageRootId", "LibraryId", "Role", "RelativeBinding",
                             "CurrentCompleteSnapshotId", "LastAttemptSnapshotId"]),
            ("ScanSnapshot", ["SnapshotId", "StorageRootId", "SourceKind", "RunRef",
                              "ScanStatus", "Availability", "Freshness", "CapturedAtUtc",
                              "ManifestVersion", "ManifestJson", "ObservedCount", "TotalCount"]),
            ("MetadataClaim", version == 1 ?
                ["ClaimId","LibraryId","SubjectKey","FieldKey","SourceKind","ExternalKey",
                 "ClaimedValue","Provenance","CapturedAtUtc"] :
                ["ClaimId","LibraryId","SubjectKey","FieldKey","SourceKind","ExternalKey",
                 "ClaimedValue","Provenance","CapturedAtUtc","RuleVersion"]),
            ("UserDecision", ["DecisionId","LibraryId","SubjectKey","Action","ChoiceValue",
                              "DecidedAtUtc","SupersedesId","RevokedAtUtc"])
        };
        foreach (var (table, columns) in required)
        {
            if (Convert.ToInt64(Scalar(db, null,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name",
                ("@name", table))) != 1)
                throw new InvalidDataException("PERSIST01_BACKUP_REQUIRED_TABLE_MISSING");
            using var q = Command(db, null, "SELECT name FROM pragma_table_info(@name)",
                ("@name", table));
            using var reader = q.ExecuteReader();
            var actual = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read()) actual.Add(reader.GetString(0));
            if (columns.Any(column => !actual.Contains(column)))
                throw new InvalidDataException("PERSIST01_BACKUP_REQUIRED_COLUMN_MISSING");
        }
        if (version >= 3 &&
            Convert.ToInt64(Scalar(db, null,
                "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='UX_MetadataClaim_Revision'")) != 1)
            throw new InvalidDataException("PERSIST01_BACKUP_CLAIM_IDENTITY_MISSING");
    }

    private SqliteConnection Open() => OpenConnection(_databasePath, create: true);

    private static SqliteConnection OpenConnection(string path, bool create)
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadOnly,
            Pooling = false, Cache = SqliteCacheMode.Private
        };
        var db = new SqliteConnection(cs.ToString());
        db.Open();
        using var pragma = db.CreateCommand();
        pragma.CommandText = "PRAGMA foreign_keys=ON; PRAGMA busy_timeout=5000;";
        pragma.ExecuteNonQuery();
        if (create)
        {
            pragma.CommandText = "PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL;";
            pragma.ExecuteNonQuery();
        }
        return db;
    }

    private void ExecuteWrite(Action<SqliteConnection, SqliteTransaction> action)
    {
        lock (_gate)
        {
            CheckDisposed();
            using var db = Open();
            using var tx = db.BeginTransaction();
            action(db, tx);
            tx.Commit();
        }
    }

    private static void Exec(SqliteConnection db, SqliteTransaction? tx, string sql,
        params (string key, object value)[] args)
    {
        using var cmd = Command(db, tx, sql, args);
        cmd.ExecuteNonQuery();
    }

    private static object? Scalar(SqliteConnection db, SqliteTransaction? tx, string sql,
        params (string key, object value)[] args)
    {
        using var cmd = Command(db, tx, sql, args);
        return cmd.ExecuteScalar();
    }

    private static SqliteCommand Command(SqliteConnection db, SqliteTransaction? tx, string sql,
        params (string key, object value)[] args)
    {
        var cmd = db.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (key, value) in args) cmd.Parameters.AddWithValue(key, value);
        return cmd;
    }

    private static void RequireUuid(string value)
    {
        if (!Guid.TryParse(value, out _)) throw new ArgumentException("Durable UUID required.");
    }

    private static void RequireText(string value, int limit)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > limit)
            throw new ArgumentException("PERSIST01_FIELD_LIMIT");
    }

    private static void ValidateRelativePaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (string.IsNullOrWhiteSpace(path) || path.Length > 2048 ||
                path.StartsWith('/') || path.StartsWith('\\') || path.Contains('\\') ||
                path.Contains("://", StringComparison.Ordinal) || path.Contains(':') ||
                path.Split('/').Any(x => x.Length == 0 || x is "." or ".."))
                throw new InvalidDataException("PERSIST01_RELATIVE_PATH_REQUIRED");
        }
    }

    private static bool AnyReparse(string path, bool allowMissingFinal)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        var parts = full[root.Length..].Split(Path.DirectorySeparatorChar,
            StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            current = Path.Combine(current, parts[i]);
            if (allowMissingFinal && i == parts.Length - 1 && !File.Exists(current))
                break;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;
        }
        return false;
    }

    private void CheckDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(SqliteCatalogStore));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_gate) _disposed = true;
    }
}
