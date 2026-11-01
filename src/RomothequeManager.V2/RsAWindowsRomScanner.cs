using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace RomothequeManager.V2.Inventory.RsA;

/// <summary>The only production-visible RS-A entry point; no caller can enable it.
/// This file ships no real ROM access permission, not even with explicitConsent=true.</summary>
public static class RealRomInventoryGate
{
/// <summary>Unconditionally reject production ROM inventory: explicit UI consent does not clear REAL_LIBRARY_SCAN_HOLD.</summary>
    public static InventorySnapshot Scan() =>
        throw new UnauthorizedAccessException("REAL_LIBRARY_SCAN_HOLD");
}

/// <summary>Internal fixture-only operation. Grants are volatile, one scope and one selection.
/// This is NOT a production filesystem permission or a substitute for an OS sandbox.</summary>
internal enum RsAOperation { RomsInventory }

/// <summary>Short-lived, one-use fixture-specific ROM read capability bound to selected scope, version and native root identity.</summary>
internal sealed class RsAReadGrant
{
/// <summary>Bind the temporary grant to a durable scope reference, selection generation, expiry and opened native handle identity.</summary>
    internal RsAReadGrant(string rootId, long selectionVersion, DateTimeOffset expiresUtc,
        RsAHandleIdentity rootIdentity)
    {
        StorageRootId = rootId;
        SelectionVersion = selectionVersion;
        ExpiresUtc = expiresUtc;
        RootIdentity = rootIdentity;
    }

/// <summary>Read-only operation type; cannot be exchanged for indexing MEDIAS or previewing images.</summary>
    internal RsAOperation Operation => RsAOperation.RomsInventory;
/// <summary>Durable identifier of the selected fixture root, independent of drive-letter changes.</summary>
    internal string StorageRootId { get; }
/// <summary>Selection generation that invalidates consent after changing the selected fixture.</summary>
    internal long SelectionVersion { get; }
/// <summary>Absolute expiration after which child reads are denied, even during a started scan.</summary>
    internal DateTimeOffset ExpiresUtc { get; }
/// <summary>Native volume/file identity captured at consent time to prevent replacement of the approved root.</summary>
    internal RsAHandleIdentity RootIdentity { get; }
    private int _revoked;
    private int _claimed;
/// <summary>Whether the consent was revoked before or during the active metadata-only operation.</summary>
    internal bool Revoked => Volatile.Read(ref _revoked) != 0;
    // A grant admits exactly ONE Scan invocation. Claim is never reset;
    // Valid intentionally remains true for the active run's repeated checks.
/// <summary>Claim the grant atomically once before opening root handles; concurrent or replayed invocations are refused.</summary>
    internal bool TryClaim(string rootId, long version) =>
        Valid(rootId, version) &&
        Interlocked.CompareExchange(ref _claimed, 1, 0) == 0 &&
        Valid(rootId, version);
/// <summary>Revoke the volatile fixture capability immediately, including future in-run permission checks.</summary>
    internal void Revoke() => Interlocked.Exchange(ref _revoked, 1);
/// <summary>Check exact scope, version, expiration and revocation without resetting the one-shot claim.</summary>
    internal bool Valid(string rootId, long version) =>
        !Revoked && DateTimeOffset.UtcNow < ExpiresUtc &&
        SelectionVersion == version && StorageRootId == rootId;
}

/// <summary>Hard limits on fixture ROM metadata enumeration, including zero file-header bytes.</summary>
internal sealed record RsABudget(
    int MaxDepth = 8, int MaxFiles = 5000, int MaxDirectories = 10000,
    TimeSpan? TimeLimit = null, int MaxHeaderBytes = 0)
{
/// <summary>Reject unbounded or unsafe scan limits; ROM contents and header reads are always prohibited.</summary>
    internal void Validate()
    {
        if (MaxDepth is < 1 or > 16 || MaxFiles is < 1 or > 100000 ||
            MaxDirectories is < 1 or > 100000 || MaxHeaderBytes != 0 ||
            TimeLimit is { Ticks: <= 0 } || TimeLimit > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(RsABudget),
                "RS_A_METADATA_ONLY_BUDGET_REQUIRED");
    }
}

/// <summary>Does not call InventoryReader.Scan: its legacy path-based traversal is unsafe for real grants.
/// Returns the shared InventorySnapshot contract using anchored-handle metadata enumeration only.</summary>
internal sealed class RsAFixtureRomSession
{
    private readonly string _sessionId = "rsa-fixture-" + Guid.NewGuid().ToString("N");
    private string? _root;
    private string? _storageRootId;
    private long _version;
    private RsAReadGrant? _grant;
    private readonly RsABudget _budget;
/// <summary>Last fully completed fixture ROM inventory; interrupted attempts cannot overwrite it.</summary>
    internal InventorySnapshot? Current { get; private set; }
/// <summary>Most recent attempt retained with diagnostic evidence even when incomplete or cancelled.</summary>
    internal InventorySnapshot? LastAttempt { get; private set; }
/// <summary>Current generation of the selected fixture, used to reject stale consent grants.</summary>
    internal long SelectionVersion => _version;

/// <summary>Create a fixture-only session with validated metadata limits and no implicit scan permission.</summary>
    internal RsAFixtureRomSession(RsABudget? budget = null)
    {
        _budget = budget ?? new();
        _budget.Validate();
    }

/// <summary>Replace the selected fixture, revoking prior consent and changing the generation without opening files.</summary>
    internal void SelectFixture(string selectedRoot, string storageRootId)
    {
        _grant?.Revoke();
        _grant = null;
        _root = null;
        _storageRootId = null;
        _version++;
        if (!Guid.TryParse(storageRootId, out _))
            throw new ArgumentException("RS_A_DURABLE_ROOT_ID_REQUIRED");
        var full = RsAAnchoredReader.ValidateFixturePath(selectedRoot);
        _storageRootId = storageRootId;
        _root = full;
        // No enumeration/opening occurs at selection.
    }

/// <summary>Issue a short-lived one-shot grant only after explicit consent and native root-identity verification.</summary>
    internal RsAReadGrant AuthorizeFixture(bool explicitUserAction,
        DateTimeOffset expiresUtc)
    {
        if (!explicitUserAction || _root is null || _storageRootId is null ||
            expiresUtc <= DateTimeOffset.UtcNow ||
            expiresUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new UnauthorizedAccessException("RS_A_EXPLICIT_FRESH_CONSENT_REQUIRED");
        // A consent is bound to an OPENED root's native file identity, never
        // just a drive letter, directory name or cached SQLite StorageRootId.
        using var opened = RsAAnchoredReader.OpenRoot(_root);
        var identity = RsAAnchoredReader.Identity(opened);
        _grant?.Revoke();
        return _grant = new RsAReadGrant(_storageRootId, _version, expiresUtc, identity);
    }

/// <summary>Invalidate the current fixture grant without modifying previously recorded observations.</summary>
    internal void Revoke() { _grant?.Revoke(); }

/// <summary>Traverse metadata through anchored handles only after a fresh one-shot claim; preserve Current on denial or partial coverage.</summary>
    internal InventorySnapshot Scan(RsAReadGrant? supplied, CancellationToken token = default,
        Action<string>? beforeChildOpenForTest = null)
    {
        var when = DateTimeOffset.UtcNow;
        var scanId = "rsa-" + Guid.NewGuid().ToString("N");
        var observations = new List<FileObservation>();
        var diagnostics = new List<InventoryDiagnostic>();
        var elapsed = Stopwatch.StartNew();
        var availability = InventoryAvailability.Unknown;
        var freshness = InventoryFreshness.Unknown;
        var status = InventoryScanStatus.Failed;
        var files = 0;
        var dirs = 0;
        var rootId = _storageRootId ?? "not-selected";

        void CheckPermission()
        {
            token.ThrowIfCancellationRequested();
            if (_grant is null || !ReferenceEquals(supplied, _grant) ||
                _storageRootId is null || !_grant.Valid(_storageRootId, _version))
                throw new UnauthorizedAccessException("RS_A_GRANT_INVALID");
            if (elapsed.Elapsed > (_budget.TimeLimit ?? TimeSpan.FromMinutes(2)))
                throw new RsAStopException("RS_A_TIME_BUDGET");
        }

        try
        {
            // Claim once before opening any root handle. Concurrent/replayed
            // calls cannot enter I/O, but the running Scan retains live checks.
            CheckPermission();
            if (supplied is null || !supplied.TryClaim(rootId, _version))
                throw new UnauthorizedAccessException("RS_A_GRANT_INVALID");
            CheckPermission();
            var root = _root ?? throw new UnauthorizedAccessException("RS_A_NO_SELECTION");
            using var pinnedRoot = RsAAnchoredReader.OpenRoot(root);
            if (supplied is null ||
                !RsAAnchoredReader.Identity(pinnedRoot).Equals(supplied.RootIdentity))
                throw new UnauthorizedAccessException("RS_A_ROOT_IDENTITY_CHANGED");
            availability = InventoryAvailability.Online;
            void Visit(SafeFileHandle directory, string prefix, int depth)
            {
                CheckPermission();
                if (++dirs > _budget.MaxDirectories || depth > _budget.MaxDepth)
                    throw new RsAStopException("RS_A_DIRECTORY_BUDGET");
                foreach (var entry in RsAAnchoredReader.Enumerate(directory))
                {
                    CheckPermission();
                    if (entry.Name is "." or "..") continue;
                    if (RsAAnchoredReader.Excluded(entry.Name)) continue;
                    var relative = prefix.Length == 0 ? entry.Name : prefix + "/" + entry.Name;
                    beforeChildOpenForTest?.Invoke(relative);
                    CheckPermission();
                    // Relative NtCreateFile is rooted in the OPEN parent handle; it must not
                    // be replaced by a FileStream/path-based open after the callback.
                    using var child = RsAAnchoredReader.OpenChild(directory, entry.Name, entry.Directory);
                    if (entry.Directory)
                    {
                        Visit(child, relative, depth + 1);
                    }
                    else
                    {
                        if (++files > _budget.MaxFiles)
                            throw new RsAStopException("RS_A_FILE_BUDGET");
                        var size = RsAAnchoredReader.Size(child);
                        var ext = Path.GetExtension(entry.Name);
                        var kind = ext.ToLowerInvariant() switch
                        {
                            ".gba" or ".gb" or ".gbc" or ".nes" or ".sfc" or ".smc" or
                            ".n64" or ".z64" or ".nds" or ".chd" or ".cue" or ".cso"
                                => InventoryKind.PotentialRom,
                            ".zip" or ".7z" or ".iso" or ".bin" => InventoryKind.ArchiveCandidate,
                            ".m3u" or ".m3u8" => InventoryKind.Playlist,
                            _ => InventoryKind.Unknown
                        };
                        var platform = relative.Contains('/')
                            ? relative.Split('/')[0].ToLowerInvariant() : "UNKNOWN";
                        observations.Add(new FileObservation(
                            Guid.NewGuid().ToString("D"), rootId + ":" + relative,
                            platform, relative, kind, ext, size, null, when,
                            "RS_A_HANDLE_METADATA_FIXTURE", InventoryAvailability.Online,
                            Array.Empty<string>()));
                    }
                }
            }
            Visit(pinnedRoot, "", 0);
            CheckPermission();
            status = InventoryScanStatus.Complete;
            freshness = InventoryFreshness.Fresh;
        }
        catch (OperationCanceledException)
        {
            status = observations.Count > 0 ? InventoryScanStatus.Partial : InventoryScanStatus.Failed;
            diagnostics.Add(Diagnostic("RS_A_CANCELLED", when));
        }
        catch (RsAStopException ex)
        {
            status = InventoryScanStatus.Partial;
            diagnostics.Add(Diagnostic(ex.Message, when));
        }
        catch (UnauthorizedAccessException)
        {
            status = observations.Count > 0 ? InventoryScanStatus.Partial : InventoryScanStatus.Failed;
            diagnostics.Add(Diagnostic("RS_A_DENIED", when));
        }
        catch (Exception ex) when (ex is IOException or System.ComponentModel.Win32Exception or
                                   DllNotFoundException or EntryPointNotFoundException or
                                   PlatformNotSupportedException)
        {
            status = observations.Count > 0 ? InventoryScanStatus.Partial : InventoryScanStatus.Failed;
            if (availability == InventoryAvailability.Unknown)
                availability = InventoryAvailability.Offline;
            diagnostics.Add(Diagnostic("RS_A_NATIVE_OPEN_FAILED", when));
        }

        var complete = status == InventoryScanStatus.Complete;
        var snapshot = new InventorySnapshot(
            "rsa-windows-metadata-fixture/1", _sessionId, scanId, when,
            status, availability, freshness,
            new[] { new InventoryCoverage("ROMS", status) },
            observations.AsReadOnly(), diagnostics.AsReadOnly(),
            Array.Empty<InventoryPlaylist>(), Array.Empty<PotentialDuplicateGroup>(),
            "RS_A_NOT_A_CONTENT_HASH", observations.Count,
            complete ? observations.Count : null, null);
        LastAttempt = snapshot;
        if (complete && availability == InventoryAvailability.Online && freshness == InventoryFreshness.Fresh)
            Current = snapshot;
        return snapshot;
    }

    private InventoryDiagnostic Diagnostic(string code, DateTimeOffset at) =>
        new(code, InventorySeverity.Warning, "ROMS", code,
            "RS_A_FIXTURE", at);
}

/// <summary>Internal sentinel for bounded enumeration termination; represented as safe diagnostics, not a leak of private paths.</summary>
internal sealed class RsAStopException(string message) : Exception(message);

/// <summary>Native volume serial and file identifier used to detect swapped or redirected roots.</summary>
internal readonly record struct RsAHandleIdentity(ulong VolumeSerial, string FileIdHex);

/// <summary>Win32 directory enumeration and NtCreateFile relative to a pinned parent handle.
/// No Directory.EnumerateFiles(path) or FileStream(path) may be used inside this reader.
/// Reject every reparse point; deny DELETE sharing so a held directory cannot be moved.</summary>
/// <summary>Windows handle-confined helper for fixture-only metadata enumeration; excludes reparse traversal.</summary>
internal static class RsAAnchoredReader
{
    private const uint ReadAttributes = 0x80;
    private const uint ListDirectory = 0x1;
    private const uint Synchronize = 0x100000;
    private const uint ShareReadWrite = 0x3; // deliberately excludes FILE_SHARE_DELETE
    private const uint OpenExisting = 3;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint BackupSemantics = 0x02000000;
    private const uint NtDirectoryFile = 0x1;
    private const uint NtNonDirectoryFile = 0x40;
    private const uint NtSynchronous = 0x20;
    private const uint NtOpenReparsePoint = 0x00200000;
    private const uint NtOpenForBackup = 0x00004000;
    private const uint ObjCaseInsensitive = 0x40;
    private const uint ObjDontReparse = 0x1000;
    private const uint FileOpen = 1;
    private const uint ReparsePoint = 0x400;
    private const uint DirectoryFlag = 0x10;
    private const int TagInfo = 9;
    private const int StandardInfo = 1;
    private const int FileIdInfo = 18;
    private const int BothDirInfo = 10;
    private const int BothDirRestart = 11;

/// <summary>One directory entry name and directory flag returned from a validated native handle.</summary>
    internal readonly record struct Entry(string Name, bool Directory);

/// <summary>Refuse unsupported device, network or nonfixture roots before any metadata enumeration.</summary>
    internal static string ValidateFixturePath(string path)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("RS_A_WINDOWS_ONLY");
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal) || path.Contains("://", StringComparison.Ordinal) ||
            path.Contains('/', StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(path))
            throw new ArgumentException("RS_A_LOCAL_ABSOLUTE_ONLY");
        var pieces = path.Replace('/', '\\').Split('\\');
        if (pieces.Any(x => x is "." or "..")) throw new ArgumentException("RS_A_TRAVERSAL_DENIED");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Path.GetDirectoryName(full) ??
            throw new ArgumentException("RS_A_ROOT_REQUIRED");
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var fixtureName = Path.GetFileName(parent);
        if (!string.Equals(Path.GetFileName(full), "ROMS", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(parent), temp, StringComparison.OrdinalIgnoreCase) ||
            !fixtureName.StartsWith("rm-rsa-fixture-", StringComparison.Ordinal) ||
            fixtureName.Length != "rm-rsa-fixture-".Length + 32 ||
            !Guid.TryParseExact(fixtureName["rm-rsa-fixture-".Length..], "N", out _))
            throw new UnauthorizedAccessException("RS_A_FIXTURE_ONLY");
        return full;
    }

/// <summary>Reject names requiring special treatment, including reparse-sensitive or unsafe components.</summary>
    internal static bool Excluded(string name) =>
        name.Equals("ROMOTHEQUE_MANAGER", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("temp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("tmp", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("cache", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("backups", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("logs", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("data", StringComparison.OrdinalIgnoreCase) ||
        name.Equals(".git", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) ||
        (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase) ||
         name.EndsWith(".sqlite", StringComparison.OrdinalIgnoreCase));

/// <summary>Open a selected local directory as a native handle with the required confinement and sharing flags.</summary>
    internal static SafeFileHandle OpenRoot(string root)
    {
        root = ValidateFixturePath(root);
        var volume = Path.GetPathRoot(root) ??
            throw new IOException("RS_A_MISSING_VOLUME_ROOT");
        // A Windows volume root is the only name-based open; every path component
        // below that volume is resolved relative to its already-open parent handle.
        var first = CreateFileW(volume, ListDirectory | ReadAttributes | Synchronize,
            ShareReadWrite, IntPtr.Zero, OpenExisting, BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (first.IsInvalid) { first.Dispose(); throw new IOException("RS_A_VOLUME_OPEN_FAILED"); }
        SafeFileHandle current = first;
        try
        {
            CheckType(current, expectedDirectory: true);
            foreach (var part in root[volume.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
            {
                var child = OpenChild(current, part, isDirectory: true);
                current.Dispose();
                current = child;
            }
            return current;
        }
        catch { current.Dispose(); throw; }
    }

/// <summary>Resolve a child only relative to an already opened trusted parent handle, never by a rebuilt absolute path.</summary>
    internal static SafeFileHandle OpenChild(SafeFileHandle parent, string name, bool isDirectory)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.Contains('/') || name.Contains('\\') || name.Contains(':') ||
            name.IndexOf('\0') >= 0 || name.EndsWith('.') || name.EndsWith(' ') ||
            name.Length > 255)
            throw new UnauthorizedAccessException("RS_A_COMPONENT_REJECTED");
        var chars = Marshal.StringToHGlobalUni(name);
        IntPtr namePtr = IntPtr.Zero;
        try
        {
            var unicode = new UnicodeString
            {
                Length = checked((ushort)(name.Length * 2)),
                MaximumLength = checked((ushort)((name.Length + 1) * 2)),
                Buffer = chars
            };
            namePtr = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(unicode, namePtr, false);
            var attrs = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(), ObjectName = namePtr,
                Attributes = ObjCaseInsensitive | ObjDontReparse
            };
            var flags = (isDirectory ? NtDirectoryFile : NtNonDirectoryFile) |
                NtSynchronous | NtOpenReparsePoint | NtOpenForBackup;
            var access = ReadAttributes | Synchronize | (isDirectory ? ListDirectory : 0);
            var status = NtCreateFile(out var result, access, ref attrs, out _,
                IntPtr.Zero, 0, ShareReadWrite, FileOpen, flags, IntPtr.Zero, 0);
            if (status < 0 || result.IsInvalid)
            {
                result?.Dispose();
                throw new IOException("RS_A_NT_RELATIVE_OPEN_REJECTED_0x" + status.ToString("X8"));
            }
            try { CheckType(result, isDirectory); return result; }
            catch { result.Dispose(); throw; }
        }
        finally
        {
            if (namePtr != IntPtr.Zero) Marshal.FreeHGlobal(namePtr);
            Marshal.FreeHGlobal(chars);
        }
    }

    private static void CheckType(SafeFileHandle handle, bool expectedDirectory)
    {
        var memory = Marshal.AllocHGlobal(8);
        try
        {
            if (!GetFileInformationByHandleEx(handle, TagInfo, memory, 8))
                throw new IOException("RS_A_HANDLE_ATTRIBUTES_UNAVAILABLE");
            var attr = unchecked((uint)Marshal.ReadInt32(memory));
            if ((attr & ReparsePoint) != 0 || ((attr & DirectoryFlag) != 0) != expectedDirectory)
                throw new UnauthorizedAccessException("RS_A_REPARSE_OR_TYPE_REJECTED");
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

/// <summary>Read native identity from the opened handle to match the consent-bound root identity.</summary>
    internal static RsAHandleIdentity Identity(SafeFileHandle root)
    {
        // FILE_ID_INFO: ULONGLONG VolumeSerialNumber + FILE_ID_128 Identifier.
        // Compare both; a newly substituted directory at the SAME path must
        // not inherit an earlier grant, even when its label/contents match.
        var memory = Marshal.AllocHGlobal(24);
        try
        {
            if (!GetFileInformationByHandleEx(root, FileIdInfo, memory, 24))
                throw new IOException("RS_A_ROOT_FILE_ID_UNAVAILABLE");
            var id = new byte[16];
            Marshal.Copy(IntPtr.Add(memory, 8), id, 0, id.Length);
            return new RsAHandleIdentity(
                unchecked((ulong)Marshal.ReadInt64(memory)),
                Convert.ToHexString(id));
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

/// <summary>Inspect file size metadata without opening or hashing ROM contents.</summary>
    internal static long Size(SafeFileHandle file)
    {
        var memory = Marshal.AllocHGlobal(24);
        try
        {
            if (!GetFileInformationByHandleEx(file, StandardInfo, memory, 24))
                throw new IOException("RS_A_FILE_SIZE_UNAVAILABLE");
            return Marshal.ReadInt64(memory, 8); // FILE_STANDARD_INFO.EndOfFile
        }
        finally { Marshal.FreeHGlobal(memory); }
    }

/// <summary>Enumerate bounded child metadata from the passed directory handle; callers enforce budgets and consent.</summary>
    internal static IEnumerable<Entry> Enumerate(SafeFileHandle directory)
    {
        const int capacity = 65536;
        const int nameOffset = 104; // FILE_ID_BOTH_DIR_INFO.FileName on Windows x64
        var buffer = Marshal.AllocHGlobal(capacity);
        try
        {
            bool restart = true;
            while (true)
            {
                if (!GetFileInformationByHandleEx(directory,
                    restart ? BothDirRestart : BothDirInfo, buffer, capacity))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error is 18 or 38) yield break; // ERROR_NO_MORE_FILES / HANDLE_EOF
                    throw new IOException("RS_A_HANDLE_ENUMERATION_FAILED_" + error);
                }
                restart = false;
                int offset = 0;
                while (true)
                {
                    var record = IntPtr.Add(buffer, offset);
                    var next = Marshal.ReadInt32(record);
                    var attrs = unchecked((uint)Marshal.ReadInt32(record, 56));
                    var nameBytes = Marshal.ReadInt32(record, 60);
                    if (nameBytes < 0 || (nameBytes & 1) != 0 ||
                        nameBytes > capacity - offset - nameOffset ||
                        next < 0 || offset + next > capacity ||
                        (next != 0 && next < nameOffset))
                        throw new IOException("RS_A_BAD_DIRECTORY_RECORD");
                    var name = Marshal.PtrToStringUni(IntPtr.Add(record, nameOffset),
                        nameBytes / 2) ?? "";
                    if ((attrs & ReparsePoint) != 0)
                        throw new UnauthorizedAccessException("RS_A_REPARSE_ENTRY_DENIED");
                    yield return new Entry(name, (attrs & DirectoryFlag) != 0);
                    if (next == 0) break;
                    offset += next;
                }
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UnicodeString
    {
        public ushort Length;
        public ushort MaximumLength;
        public IntPtr Buffer;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ObjectAttributes
    {
        public int Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoStatusBlock
    {
        public IntPtr Status;
        public IntPtr Information;
    }

    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share,
        IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle handle, uint desiredAccess,
        ref ObjectAttributes objectAttributes, out IoStatusBlock ioStatus,
        IntPtr allocationSize, uint fileAttributes, uint shareAccess,
        uint createDisposition, uint createOptions, IntPtr eaBuffer, uint eaLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle handle, int infoClass, IntPtr info, int infoSize);
}
