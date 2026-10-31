using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using RomothequeManager.V2.Inventory.RsA;

namespace RomothequeManager.V2.Inventory.Media.MiA;

/// <summary>MI-A is an internal synthetic-fixture capability, not a production permission.
/// It is never transferable to RomsInventory or MediaPreview.</summary>
internal enum MiAOperation { MediaIndex }

internal sealed class MiAReadGrant
{
    private int _revoked;
    private MiAReadGrant(string rootId, long version, long generation,
        DateTimeOffset expiresUtc, RsAHandleIdentity identity, MiABudget budget)
    {
        RootId = rootId;
        SelectionVersion = version;
        Generation = generation;
        ExpiresUtc = expiresUtc;
        RootIdentity = identity;
        Budget = budget;
    }

    internal static MiAReadGrant Issue(string rootId, long version, long generation,
        DateTimeOffset expiresUtc, RsAHandleIdentity identity, MiABudget budget) =>
        new(rootId, version, generation, expiresUtc, identity, budget);

    internal MiAOperation Operation => MiAOperation.MediaIndex;
    internal string RootId { get; }
    internal long SelectionVersion { get; }
    internal long Generation { get; }
    internal DateTimeOffset ExpiresUtc { get; }
    internal RsAHandleIdentity RootIdentity { get; }
    internal MiABudget Budget { get; }
    internal void Revoke() => Interlocked.Exchange(ref _revoked, 1);
    internal bool Valid(string rootId, long version, long generation) =>
        Volatile.Read(ref _revoked) == 0 &&
        DateTimeOffset.UtcNow < ExpiresUtc &&
        RootId == rootId && SelectionVersion == version && Generation == generation;
}

/// <summary>Absolute ceilings for the fixture operation; callers cannot increase them.
/// Timeout is cooperative: blocking kernel I/O is not preempted by CancellationToken.</summary>
internal sealed record MiABudget(
    int MaxDepth = 8, int MaxFiles = 5000, int MaxDirectories = 10000,
    TimeSpan? TimeLimit = null, int MaxHeaderBytes = 262144,
    long MaxTotalHeaderBytes = 8388608, long MaxImageBytes = 67108864,
    int MaxWidth = 8192, int MaxHeight = 8192, long MaxPixels = 32000000)
{
    internal TimeSpan EffectiveTimeLimit => TimeLimit ?? TimeSpan.FromSeconds(30);

    internal void Validate()
    {
        if (MaxDepth is < 1 or > 8 || MaxFiles is < 1 or > 5000 ||
            MaxDirectories is < 1 or > 10000 ||
            EffectiveTimeLimit <= TimeSpan.Zero ||
            EffectiveTimeLimit > TimeSpan.FromSeconds(30) ||
            MaxHeaderBytes is < 33 or > 262144 ||
            MaxTotalHeaderBytes is < 33 or > 8388608 ||
            MaxImageBytes is < 33 or > 67108864 ||
            MaxWidth is < 1 or > 8192 || MaxHeight is < 1 or > 8192 ||
            MaxPixels is < 1 or > 32000000)
            throw new ArgumentOutOfRangeException(nameof(MiABudget), "MI_A_HARD_CAPS_REQUIRED");
    }
}

/// <summary>Independent, handle-confined MEDIAS index on generated Windows fixtures only.
/// Does not call the legacy path-based MediaIndexReader or any Skia decoder.</summary>
internal sealed class MiAFixtureMediaSession
{
    private static readonly ConcurrentDictionary<string, byte> ActiveScopes =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly MiABudget _budget;
    private readonly string _mediaRootRef = "mia-media-" + Guid.NewGuid().ToString("N");
    private string? _root;
    private string? _rootId;
    private long _version;
    private long _generation;
    private MiAReadGrant? _grant;
    internal MediaIndexSnapshot? Current { get; private set; }
    internal MediaIndexSnapshot? LastAttempt { get; private set; }
    internal long LastHeaderBytesRead { get; private set; }
    internal long SelectionVersion => _version;

    internal MiAFixtureMediaSession(MiABudget? budget = null)
    {
        _budget = budget ?? new();
        _budget.Validate();
    }

    internal void SelectFixture(string root, string storageRootId)
    {
        _grant?.Revoke();
        _grant = null;
        _root = null;
        _rootId = null;
        Interlocked.Increment(ref _version);
        Interlocked.Increment(ref _generation);
        if (!Guid.TryParse(storageRootId, out _))
            throw new ArgumentException("MI_A_STORAGE_ROOT_ID_REQUIRED");
        _root = MiAAnchoredMediaReader.ValidateFixturePath(root);
        _rootId = storageRootId;
        // Selection has no filesystem side effects.
    }

    internal MiAReadGrant AuthorizeFixture(bool explicitUserAction, DateTimeOffset expiresUtc)
    {
        if (!explicitUserAction || _root is null || _rootId is null ||
            expiresUtc <= DateTimeOffset.UtcNow ||
            expiresUtc > DateTimeOffset.UtcNow.AddMinutes(5))
            throw new UnauthorizedAccessException("MI_A_EXPLICIT_CONSENT_REQUIRED");
        using var root = MiAAnchoredMediaReader.OpenFixtureRoot(_root);
        var identity = RsAAnchoredReader.Identity(root);
        _grant?.Revoke();
        return _grant = MiAReadGrant.Issue(_rootId, _version,
            Interlocked.Increment(ref _generation), expiresUtc, identity, _budget);
    }

    internal void Revoke() => _grant?.Revoke();

    internal MediaIndexSnapshot Scan(MiAReadGrant? supplied,
        CancellationToken cancellationToken = default,
        Action<string>? beforeChildOpenForTest = null,
        Action<int>? afterHeaderReadForTest = null)
    {
        var when = DateTimeOffset.UtcNow;
        var runId = "mia-" + Guid.NewGuid().ToString("N");
        var observations = new List<MediaObservation>();
        var diagnostics = new List<InventoryDiagnostic>();
        var stopwatch = Stopwatch.StartNew();
        var availability = InventoryAvailability.Unknown;
        var status = InventoryScanStatus.Failed;
        var bytesRead = 0L;
        var files = 0;
        var dirs = 0;
        var root = _root;
        var acquired = false;

        void Check()
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (supplied is null || _grant is null ||
                !ReferenceEquals(supplied, _grant) || _rootId is null ||
                !supplied.Valid(_rootId, _version, _generation))
                throw new UnauthorizedAccessException("MI_A_GRANT_INVALID");
            if (stopwatch.Elapsed >= supplied.Budget.EffectiveTimeLimit)
                throw new MiAStopException("MI_A_TIMEOUT");
        }

        void Visit(SafeFileHandle directory, string prefix, int depth, ulong volume)
        {
            Check();
            if (++dirs > _budget.MaxDirectories || depth > _budget.MaxDepth)
                throw new MiAStopException("MI_A_DIRECTORY_BUDGET");
            foreach (var entry in RsAAnchoredReader.Enumerate(directory))
            {
                Check();
                if (entry.Name is "." or "..") continue;
                MiAAnchoredMediaReader.ValidateComponent(entry.Name);
                if (MiAAnchoredMediaReader.Excluded(entry.Name)) continue;
                var relative = prefix.Length == 0 ? entry.Name : prefix + "/" + entry.Name;
                if (entry.Directory)
                {
                    beforeChildOpenForTest?.Invoke(relative);
                    Check();
                    using var child = RsAAnchoredReader.OpenChild(directory, entry.Name, true);
                    if (RsAAnchoredReader.Identity(child).VolumeSerial != volume)
                        throw new UnauthorizedAccessException("MI_A_CROSS_VOLUME_DENIED");
                    Visit(child, relative, depth + 1, volume);
                    continue;
                }
                if (++files > _budget.MaxFiles)
                    throw new MiAStopException("MI_A_FILE_BUDGET");
                var extension = Path.GetExtension(entry.Name).ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg" or ".webp")) continue;
                beforeChildOpenForTest?.Invoke(relative);
                Check();
                using var file = MiAAnchoredMediaReader.OpenMediaFile(directory, entry.Name, volume);
                Check();
                var size = RsAAnchoredReader.Size(file);
                if (size < 0 || size > _budget.MaxImageBytes)
                    throw new MiAStopException("MI_A_IMAGE_SIZE_LIMIT");
                if (bytesRead >= _budget.MaxTotalHeaderBytes)
                    throw new MiAStopException("MI_A_TOTAL_HEADER_BUDGET");
                var limit = checked((int)Math.Min(size, Math.Min(_budget.MaxHeaderBytes,
                    _budget.MaxTotalHeaderBytes - bytesRead)));
                if (limit < 33)
                    throw new MiAStopException("MI_A_HEADER_BUDGET");
                var header = new byte[limit];
                var actual = 0;
                while (actual < limit)
                {
                    Check();
                    var n = RandomAccess.Read(file, header.AsSpan(actual,
                        Math.Min(4096, limit - actual)), actual);
                    if (n == 0) break;
                    actual += n;
                    bytesRead += n;
                    afterHeaderReadForTest?.Invoke(n);
                    Check();
                }
                if (actual == 0)
                    throw new MiAStopException("MI_A_HEADER_NOT_READ");
                var info = MiAHeaderClassifier.Classify(header.AsSpan(0, actual),
                    extension, _budget);
                if (actual < limit) throw new MiAStopException("MI_A_SHORT_READ");
                var diagnosticRefs = info.Diagnostic is null
                    ? Array.Empty<string>() : new[] { info.Diagnostic };
                var pieces = relative.Split('/');
                var platform = pieces.Length > 1 ? pieces[0].ToLowerInvariant() : "UNKNOWN";
                observations.Add(new MediaObservation(
                    "mia-observation-" + runId + "-" + observations.Count, _mediaRootRef,
                    "MEDIAS/" + relative, platform, MediaKind.Unknown, extension,
                    info.Format, info.Valid, size, info.Width, info.Height,
                    MiAAnchoredMediaReader.ModifiedUtc(file), when,
                    InventoryAvailability.Online, "MI_A_HANDLE_HEADER_FIXTURE",
                    Array.AsReadOnly(diagnosticRefs)));
            }
        }

        try
        {
            Check(); // No filesystem access without a live, exact capability.
            if (root is null) throw new UnauthorizedAccessException("MI_A_NO_SELECTION");
            if (!ActiveScopes.TryAdd(root, 0))
                throw new UnauthorizedAccessException("MI_A_ALREADY_SCANNING");
            acquired = true;
            using var openedRoot = MiAAnchoredMediaReader.OpenFixtureRoot(root);
            if (supplied is null ||
                !RsAAnchoredReader.Identity(openedRoot).Equals(supplied.RootIdentity))
                throw new UnauthorizedAccessException("MI_A_ROOT_CHANGED");
            availability = InventoryAvailability.Online;
            Visit(openedRoot, "", 0, supplied.RootIdentity.VolumeSerial);
            Check();
            status = InventoryScanStatus.Complete;
        }
        catch (OperationCanceledException)
        {
            status = InventoryScanStatus.Partial;
            diagnostics.Add(Diagnostic("MI_A_CANCELLED", when));
        }
        catch (MiAStopException ex)
        {
            status = InventoryScanStatus.Partial;
            diagnostics.Add(Diagnostic(ex.Message, when));
        }
        catch (UnauthorizedAccessException)
        {
            status = observations.Count > 0 ? InventoryScanStatus.Partial : InventoryScanStatus.Failed;
            diagnostics.Add(Diagnostic("MI_A_DENIED", when));
        }
        catch (Exception ex) when (ex is IOException or
            System.ComponentModel.Win32Exception or PlatformNotSupportedException or
            EntryPointNotFoundException or DllNotFoundException)
        {
            status = observations.Count > 0 ? InventoryScanStatus.Partial : InventoryScanStatus.Failed;
            if (availability == InventoryAvailability.Unknown)
                availability = InventoryAvailability.Offline;
            diagnostics.Add(Diagnostic("MI_A_NATIVE_IO_FAILED", when));
        }
        finally
        {
            if (acquired && root is not null) ActiveScopes.TryRemove(root, out _);
            LastHeaderBytesRead = bytesRead;
        }

        var complete = status == InventoryScanStatus.Complete;
        var snapshot = new MediaIndexSnapshot(runId, _mediaRootRef, when,
            status, availability,
            complete ? InventoryFreshness.Fresh : InventoryFreshness.Unknown,
            new[] { new InventoryCoverage("MEDIAS", status) },
            observations.AsReadOnly(), diagnostics.AsReadOnly(),
            observations.Count, complete ? observations.Count : null);
        LastAttempt = snapshot;
        if (complete && availability == InventoryAvailability.Online)
            Current = snapshot;
        return snapshot;
    }

    private static InventoryDiagnostic Diagnostic(string code, DateTimeOffset at) =>
        new(code, InventorySeverity.Warning, "MEDIAS", code, "MI_A_FIXTURE", at);
}

internal sealed class MiAStopException(string code) : Exception(code);

/// <summary>Native file opens are relative to a pinned parent handle. Reads and file facts
/// come from that same handle; never re-open a checked file by path.</summary>
internal static class MiAAnchoredMediaReader
{
    private const uint ReadData = 1;
    private const uint ReadAttributes = 0x80;
    private const uint ListDirectory = 1;
    private const uint Synchronize = 0x100000;
    private const uint ShareReadWrite = 0x3;
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000;
    private const uint OpenReparsePoint = 0x00200000;
    private const uint NtNonDirectory = 0x40;
    private const uint NtSynchronous = 0x20;
    private const uint NtOpenReparse = 0x00200000;
    private const uint NtBackup = 0x00004000;
    private const uint ObjDontReparse = 0x1000;
    private const uint ObjCaseInsensitive = 0x40;
    private const uint FileOpen = 1;

    internal static string ValidateFixturePath(string path)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("MI_A_WINDOWS_ONLY");
        if (string.IsNullOrWhiteSpace(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
            path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains('/') || path.Contains("://", StringComparison.Ordinal) ||
            !Path.IsPathFullyQualified(path))
            throw new ArgumentException("MI_A_LOCAL_ONLY");
        var parts = path.Split('\\');
        if (parts.Any(x => x is "." or ".."))
            throw new ArgumentException("MI_A_TRAVERSAL_DENIED");
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var parent = Path.GetDirectoryName(full) ??
            throw new ArgumentException("MI_A_ROOT_REQUIRED");
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
        var fixture = Path.GetFileName(parent);
        if (!string.Equals(Path.GetFileName(full), "MEDIAS", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(Path.GetDirectoryName(parent), temp, StringComparison.OrdinalIgnoreCase) ||
            !fixture.StartsWith("rm-mia-fixture-", StringComparison.Ordinal) ||
            fixture.Length != "rm-mia-fixture-".Length + 32 ||
            !Guid.TryParseExact(fixture["rm-mia-fixture-".Length..], "N", out _))
            throw new UnauthorizedAccessException("MI_A_FIXTURE_ONLY");
        return full;
    }

    internal static bool Excluded(string name) =>
        RsAAnchoredReader.Excluded(name) ||
        name.Equals("ROMS", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("MEDIAS", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("sqlite", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("catalog.sqlite", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("bin", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("obj", StringComparison.OrdinalIgnoreCase);

    internal static void ValidateComponent(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.Contains('/') || name.Contains('\\') || name.Contains(':') ||
            name.EndsWith('.') || name.EndsWith(' ') ||
            name.Contains('~') || name.IndexOf('\0') >= 0 ||
            name.Length > 255 || name != name.Normalize(System.Text.NormalizationForm.FormC))
            throw new UnauthorizedAccessException("MI_A_ALIAS_OR_COMPONENT_DENIED");
        var stem = name.Split('.')[0].ToUpperInvariant();
        if (stem is "CON" or "PRN" or "AUX" or "NUL" ||
            (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) ||
                                  stem.StartsWith("LPT", StringComparison.Ordinal)) &&
             stem[3] is >= '1' and <= '9'))
            throw new UnauthorizedAccessException("MI_A_DEVICE_ALIAS_DENIED");
    }

    internal static SafeFileHandle OpenFixtureRoot(string path)
    {
        var full = ValidateFixturePath(path);
        var volume = Path.GetPathRoot(full) ?? throw new IOException("MI_A_VOLUME_MISSING");
        var handle = CreateFileW(volume, ListDirectory | ReadAttributes | Synchronize,
            ShareReadWrite, IntPtr.Zero, OpenExisting,
            BackupSemantics | OpenReparsePoint, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new IOException("MI_A_VOLUME_OPEN_FAILED");
        }
        try
        {
            foreach (var name in full[volume.Length..].Split('\\',
                         StringSplitOptions.RemoveEmptyEntries))
            {
                ValidateComponent(name);
                var child = RsAAnchoredReader.OpenChild(handle, name, true);
                handle.Dispose();
                handle = child;
            }
            return handle;
        }
        catch { handle.Dispose(); throw; }
    }

    internal static SafeFileHandle OpenMediaFile(SafeFileHandle parent, string name,
        ulong expectedVolume)
    {
        ValidateComponent(name);
        var chars = Marshal.StringToHGlobalUni(name);
        IntPtr pointer = IntPtr.Zero;
        try
        {
            var unicode = new UnicodeString
            {
                Length = checked((ushort)(name.Length * 2)),
                MaximumLength = checked((ushort)((name.Length + 1) * 2)),
                Buffer = chars
            };
            pointer = Marshal.AllocHGlobal(Marshal.SizeOf<UnicodeString>());
            Marshal.StructureToPtr(unicode, pointer, false);
            var attributes = new ObjectAttributes
            {
                Length = Marshal.SizeOf<ObjectAttributes>(),
                RootDirectory = parent.DangerousGetHandle(), ObjectName = pointer,
                Attributes = ObjCaseInsensitive | ObjDontReparse
            };
            var status = NtCreateFile(out var file,
                ReadData | ReadAttributes | Synchronize, ref attributes,
                out _, IntPtr.Zero, 0, ShareReadWrite, FileOpen,
                NtNonDirectory | NtSynchronous | NtOpenReparse | NtBackup,
                IntPtr.Zero, 0);
            if (status < 0 || file.IsInvalid)
            {
                file?.Dispose();
                throw new IOException("MI_A_FILE_OPEN_REFUSED_0x" + status.ToString("X8"));
            }
            try
            {
                CheckFile(file, expectedVolume);
                return file;
            }
            catch { file.Dispose(); throw; }
        }
        finally
        {
            if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer);
            Marshal.FreeHGlobal(chars);
        }
    }

    private static void CheckFile(SafeFileHandle file, ulong expectedVolume)
    {
        var attr = Marshal.AllocHGlobal(8);
        var stat = Marshal.AllocHGlobal(24);
        try
        {
            if (!GetFileInformationByHandleEx(file, 9, attr, 8) ||
                !GetFileInformationByHandleEx(file, 1, stat, 24))
                throw new IOException("MI_A_FILE_FACTS_UNAVAILABLE");
            var flags = unchecked((uint)Marshal.ReadInt32(attr));
            if ((flags & 0x400) != 0 || (flags & 0x10) != 0)
                throw new UnauthorizedAccessException("MI_A_REPARSE_OR_DIRECTORY_DENIED");
            // FILE_STANDARD_INFO.NumberOfLinks. Hard links can point to content
            // outside the chosen MEDIAS tree even when no reparse flag exists.
            var links = Marshal.ReadInt32(stat, 16);
            if (links != 1)
                throw new UnauthorizedAccessException("MI_A_HARDLINK_UNPROVEN_SCOPE");
            if (RsAAnchoredReader.Identity(file).VolumeSerial != expectedVolume)
                throw new UnauthorizedAccessException("MI_A_CROSS_VOLUME_DENIED");
        }
        finally
        {
            Marshal.FreeHGlobal(attr);
            Marshal.FreeHGlobal(stat);
        }
    }

    internal static DateTimeOffset? ModifiedUtc(SafeFileHandle file)
    {
        var buffer = Marshal.AllocHGlobal(40);
        try
        {
            if (!GetFileInformationByHandleEx(file, 0, buffer, 40)) return null;
            var ticks = Marshal.ReadInt64(buffer, 16); // FILE_BASIC_INFO.LastWriteTime
            return ticks > 0 ? DateTimeOffset.FromFileTime(ticks) : null;
        }
        catch (ArgumentOutOfRangeException) { return null; }
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
    private static extern SafeFileHandle CreateFileW(string path, uint desiredAccess,
        uint shareMode, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("ntdll.dll", ExactSpelling = true)]
    private static extern int NtCreateFile(out SafeFileHandle file, uint access,
        ref ObjectAttributes attributes, out IoStatusBlock ioStatus,
        IntPtr allocationSize, uint fileAttributes, uint share,
        uint disposition, uint options, IntPtr ea, uint eaLength);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle handle,
        int infoClass, IntPtr buffer, int bufferLength);
}

internal static class MiAHeaderClassifier
{
    internal readonly record struct HeaderResult(MediaImageFormat Format,
        bool Valid, int? Width, int? Height, string? Diagnostic);

    internal static HeaderResult Classify(ReadOnlySpan<byte> b, string extension, MiABudget cap)
    {
        bool png = b.Length >= 8 && b[..8].SequenceEqual(
            new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        bool jpg = b.Length >= 2 && b[0] == 255 && b[1] == 216;
        bool webp = b.Length >= 12 && b[..4].SequenceEqual("RIFF"u8) &&
                    b.Slice(8, 4).SequenceEqual("WEBP"u8);
        var format = png ? MediaImageFormat.Png : jpg ? MediaImageFormat.Jpeg :
                     webp ? MediaImageFormat.Webp : MediaImageFormat.Unknown;
        if (webp) return new(format, false, null, null, "UNSUPPORTED_FORMAT");
        if (!(png && extension == ".png") &&
            !(jpg && extension is ".jpg" or ".jpeg"))
            return new(format, false, null, null, "MI_A_SIGNATURE_OR_EXTENSION_MISMATCH");
        int width, height;
        if (png)
        {
            if (b.Length < 33 || BinaryPrimitives.ReadUInt32BigEndian(b.Slice(8, 4)) != 13 ||
                !b.Slice(12, 4).SequenceEqual("IHDR"u8) ||
                b[24] is not (1 or 2 or 4 or 8 or 16) ||
                b[26] != 0 || b[27] != 0 || b[28] > 1 ||
                Crc(b.Slice(12, 17)) != BinaryPrimitives.ReadUInt32BigEndian(b.Slice(29, 4)))
                return new(format, false, null, null, "MI_A_INVALID_PNG_HEADER");
            width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(16, 4)));
            height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(b.Slice(20, 4)));
        }
        else if (!JpegDimensions(b, out width, out height))
            return new(format, false, null, null, "MI_A_INVALID_JPEG_HEADER");
        if (width < 1 || height < 1 || width > cap.MaxWidth ||
            height > cap.MaxHeight || (long)width * height > cap.MaxPixels)
            return new(format, false, null, null, "MI_A_DIMENSION_LIMIT");
        return new(format, true, width, height, null);
    }

    private static uint Crc(ReadOnlySpan<byte> input)
    {
        uint crc = 0xffffffff;
        foreach (byte b in input)
        {
            crc ^= b;
            for (int n = 0; n < 8; n++)
                crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320u : 0u);
        }
        return ~crc;
    }

    private static bool JpegDimensions(ReadOnlySpan<byte> input, out int w, out int h)
    {
        w = h = 0;
        int cursor = 2;
        while (cursor + 4 <= input.Length)
        {
            if (input[cursor++] != 255) return false;
            while (cursor < input.Length && input[cursor] == 255) cursor++;
            if (cursor >= input.Length) return false;
            byte marker = input[cursor++];
            if (marker is 0 or 0xd8 or 0xd9 or 0xda) return false;
            if (marker is >= 0xd0 and <= 0xd7 or 0x01) continue;
            if (cursor + 2 > input.Length) return false;
            int len = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(cursor, 2));
            if (len < 2 || cursor + len > input.Length) return false;
            if (marker is 0xc0 or 0xc1 or 0xc2 or 0xc3)
            {
                if (len < 8 || input[cursor + 2] != 8) return false;
                h = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(cursor + 3, 2));
                w = BinaryPrimitives.ReadUInt16BigEndian(input.Slice(cursor + 5, 2));
                return true;
            }
            cursor += len;
        }
        return false;
    }
}
