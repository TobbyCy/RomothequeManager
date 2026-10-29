using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace RomothequeManager.V2;

// NEXT-01-A: staging-only ES-DE reader. The application must opt in to a user-selected root.
// No writes, recursive ROM scan, network access, or synthetic snapshot provenance.
public sealed record EsDeDiagnostic(string Code, string Platform, string Entry, string Message);
public sealed record EsDeMedia(string Kind, string RelativePath, bool Exists);
// Keep the historical nine-parameter constructor and Deconstruct contract.
// Optional ES-DE metadata is added only as nullable init-only properties.
/// <summary>An ES-DE game reference, not proof of a playable or verified ROM.</summary>
/// <remarks>The nine positional parameters and deconstruction order are preserved.
/// Optional genre, developer and release date are read only from declared XML.</remarks>
public sealed record EsDeGame(
    string GameId, string VariantId, string Platform, string Title, string RomRelativePath,
    bool RomExists, string? Description, IReadOnlyList<EsDeMedia> Media,
    IReadOnlyList<string> DiscPaths)
{
    public string? Genre { get; init; }
    public string? Developer { get; init; }
    public string? ReleaseDate { get; init; }
}
public sealed record EsDeCatalogueSnapshot(
    string LibraryId, string SourceKind, string ContentFingerprint,
    IReadOnlyList<EsDeGame> Games, IReadOnlyList<EsDeDiagnostic> Diagnostics);

/// <summary>Maintains the last successfully imported ES-DE catalogue within a session.</summary>
/// <remarks>No files are modified and no background scan is started; errors preserve
/// <see cref="Current"/> and the previously selected source root.</remarks>
public sealed class EsDeImportSession
{
    private string _root;
    // Session-scoped identity: persists through reimports and verified relocation.
    // A new process/session needs explicit future persistence or re-association.
    private readonly string _libraryId = "library-" + Guid.NewGuid().ToString("N");
    public EsDeCatalogueSnapshot? Current { get; private set; }

    public EsDeImportSession(string explicitlySelectedLibraryRoot)
    {
        if (string.IsNullOrWhiteSpace(explicitlySelectedLibraryRoot))
            throw new ArgumentException("Explicit library root is required.", nameof(explicitlySelectedLibraryRoot));
        _root = Path.GetFullPath(explicitlySelectedLibraryRoot);
    }

    // Build fully in temporary memory; failure leaves Current untouched.
    /// <summary>Explicitly imports from the selected root into a staged in-memory catalogue.</summary>
    /// <returns>The complete accepted catalogue; an exception never replaces Current.</returns>
    public EsDeCatalogueSnapshot Import()
    {
        var staged = EsDeCatalogueImporter.Read(_root, _libraryId);
        Current = staged;
        return staged;
    }

    // A new drive letter/path must be explicitly supplied and have matching content.
    // Failed re-association preserves both the current root and last valid snapshot.
    /// <summary>Re-associates a new user-selected root only if its catalogue fingerprint matches.</summary>
    /// <param name="explicitlySelectedNewRoot">New explicit library root; no automatic discovery.</param>
    /// <returns>A verified catalogue under the existing session identity.</returns>
    public EsDeCatalogueSnapshot RelocateVerified(string explicitlySelectedNewRoot)
    {
        if (Current is null) throw new InvalidOperationException("Import before relocation.");
        var newRoot = Path.GetFullPath(explicitlySelectedNewRoot);
        var staged = EsDeCatalogueImporter.Read(newRoot, _libraryId);
        if (!string.Equals(staged.ContentFingerprint, Current.ContentFingerprint, StringComparison.Ordinal))
            throw new InvalidDataException("Relocation not verified: catalogue fingerprint differs.");
        _root = newRoot;
        Current = staged;
        return staged;
    }

    // Never trust Exists from a previous import: enforce snapshot membership and
    // path/reparse restrictions again immediately before giving the UI a read handle.
    // Caller owns and must dispose the returned stream. No network, writes or upload.
    /// <summary>Opens a media reference from Current read-only after revalidating its path.</summary>
    /// <param name="media">A media reference actually present in Current.</param>
    /// <param name="stream">Read-only stream owned by the caller, or null on refusal.</param>
    /// <returns>True only if the authorized file could be opened; no download or copy.</returns>
    public bool TryOpenMedia(EsDeMedia media, out Stream? stream)
    {
        stream = null;
        if (media is null || Current is null) return false;
        if (!Current.Games.SelectMany(game => game.Media).Contains(media)) return false;
        try
        {
            var path = EsDeCatalogueImporter.ResolveSnapshotMedia(_root, media.RelativePath);
            stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidDataException or ArgumentException or System.Security.SecurityException
            or NotSupportedException)
        {
            return false;
        }
    }
}

// XML reader failures are distinct from an otherwise well-formed XML whose
// layout, paths or duplicated required fields are unsupported. Never expose
// the source XML, exception details or absolute filesystem path in the UI.
internal sealed class EsDeXmlReadException : IOException
{
    internal EsDeXmlReadException(string platform, string sourceConvention, XmlException cause)
        : base("ES-DE XML reader rejected the gamelist for one platform.", cause)
    {
        Platform = platform;
        SourceConvention = sourceConvention;
        LineNumber = cause.LineNumber;
        LinePosition = cause.LinePosition;
    }

    public string Platform { get; }
    // Fixed identifier only; never include an absolute path or XML text in public diagnostics.
    public string SourceConvention { get; }
    public int LineNumber { get; }
    public int LinePosition { get; }
}

/// <summary>Reads the three supported ES-DE gamelist conventions under a selected ROMS root.</summary>
/// <remarks>Uses bounded XML/playlist reads and refuses concurrent sources, DTDs,
/// escaped paths and reparse points. Does not scan or identify ROMs independently.</remarks>
public static class EsDeCatalogueImporter
{
    private const int XmlLimit = 8 * 1024 * 1024;
    private const int PlaylistByteLimit = 64 * 1024;
    private const int PlaylistLineLimit = 256;
    private static readonly string[] MediaFields = ["image", "thumbnail", "marquee", "video"];

    /// <summary>Reads an ES-DE catalogue from a root explicitly supplied by the caller.</summary>
    /// <param name="explicitlySelectedLibraryRoot">Folder containing a ROMS child, not the ES-DE folder alone.</param>
    /// <returns>In-memory ES-DE references with source provenance and diagnostics.</returns>
    public static EsDeCatalogueSnapshot Read(string explicitlySelectedLibraryRoot) =>
        Read(explicitlySelectedLibraryRoot, "library-" + Guid.NewGuid().ToString("N"));

    internal static EsDeCatalogueSnapshot Read(string explicitlySelectedLibraryRoot, string libraryId)
    {
        if (string.IsNullOrWhiteSpace(explicitlySelectedLibraryRoot))
            throw new ArgumentException("Explicit library root is required.", nameof(explicitlySelectedLibraryRoot));

        var root = Path.GetFullPath(explicitlySelectedLibraryRoot);
        RequireOrdinaryDirectory(root);
        var romRoot = Path.Combine(root, "ROMS");
        var mediaRoot = Path.Combine(root, "MEDIAS");
        // ES-DE >= 2.0 stores gamelists separately from ROMs. Inspect only
        // this known path below the user-selected root; no global discovery.
        var esdeRoot = Path.Combine(root, "ES-DE");
        var esdeGamelists = Path.Combine(esdeRoot, "gamelists");
        var esdeMediaRoot = Path.Combine(esdeRoot, "downloaded_media");
        RequireOrdinaryDirectory(romRoot);
        if (Directory.Exists(mediaRoot)) RequireOrdinaryDirectory(mediaRoot);
        if (Directory.Exists(esdeGamelists)) RequireOrdinaryDirectory(esdeGamelists);

        var games = new List<EsDeGame>();
        var diagnostics = new List<EsDeDiagnostic>();
        // Windows libraries cannot distinguish paths that differ only by casing.
        // Reject ambiguous portable identities even if a test host has a case-sensitive FS.
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var platforms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var xmlFound = false;
        foreach (var platformRoot in Directory.GetDirectories(romRoot).OrderBy(x => x, StringComparer.Ordinal))
        {
            RequireOrdinaryDirectory(platformRoot);
            var platform = Path.GetFileName(platformRoot);
            if (string.IsNullOrWhiteSpace(platform)) continue;
            if (!platforms.Add(platform))
                throw new InvalidDataException("ESDE_PLATFORM_CASE_COLLISION");

            var mediaPlatform = Path.Combine(mediaRoot, platform);
            var historicXml = Path.Combine(mediaPlatform, "metadata", "es-de-gamelist.xml");
            var localXml = Path.Combine(platformRoot, "gamelist.xml");
            var esdeXml = Path.Combine(esdeGamelists, platform, "gamelist.xml");
            var hasHistoric = File.Exists(historicXml);
            var hasLocal = File.Exists(localXml);
            var hasEsde = File.Exists(esdeXml);
            if ((hasHistoric ? 1 : 0) + (hasLocal ? 1 : 0) + (hasEsde ? 1 : 0) > 1)
                throw new InvalidDataException("Ambiguous ES-DE source for platform " + platform);
            if (!hasHistoric && !hasLocal && !hasEsde)
            {
                diagnostics.Add(new("XML_MISSING", platform, "", "No supported gamelist found."));
                continue;
            }

            var xmlFile = hasHistoric ? historicXml : hasLocal ? localXml : esdeXml;
            var xmlDomain = hasHistoric ? mediaRoot : hasLocal ? romRoot : esdeGamelists;
            // A fixed, harmless convention identifier enables local error triage.
            var sourceConvention = hasHistoric ? "MEDIAS_METADATA" :
                hasLocal ? "ROMS_GAMELIST" : "ESDE_GAMELISTS";
            AssertWithin(xmlFile, xmlDomain);
            AssertNoReparsePoints(xmlFile);
            xmlFound = true;
            XDocument doc;
            try { doc = LoadXml(xmlFile); }
            catch (XmlException error)
            {
                throw new EsDeXmlReadException(platform, sourceConvention, error);
            }
            if (doc.Root?.Name != "gameList")
                throw new InvalidDataException("Expected unnamespaced gameList: " + platform);
            foreach (var entry in doc.Root.Elements())
            {
                if (entry.Name == "folder")
                {
                    diagnostics.Add(new("FOLDER_ENTRY", platform, "", "Folder metadata is not a game."));
                    continue;
                }
                if (entry.Name != "game")
                {
                    diagnostics.Add(new("IGNORED_ENTRY", platform, entry.Name.LocalName, "Unsupported ES-DE entry."));
                    continue;
                }
                var rawPath = SingleField(entry, "path");
                if (string.IsNullOrWhiteSpace(rawPath))
                    throw new InvalidDataException("Missing or repeated game path in " + platform);
                var romFile = ResolveRelative(platformRoot, rawPath, romRoot);
                var romRelative = NormalizeRelative(Path.GetRelativePath(root, romFile));
                if (!identities.Add(platform + "\u001f" + romRelative))
                    throw new InvalidDataException("Duplicated ROM identity in " + platform);

                var id = Digest(platform + "\u001f" + romRelative);
                var title = SingleField(entry, "name");
                if (string.IsNullOrWhiteSpace(title))
                {
                    title = Path.GetFileNameWithoutExtension(romFile);
                    diagnostics.Add(new("TITLE_FALLBACK", platform, romRelative, "Name missing; filename shown."));
                }
                bool exists = File.Exists(romFile);
                if (!exists)
                    diagnostics.Add(new("ROM_MISSING", platform, romRelative, "Source unavailable; never treated as deleted."));

                var media = new List<EsDeMedia>();
                foreach (var field in MediaFields)
                {
                    var mediaValue = SingleField(entry, field);
                    if (string.IsNullOrWhiteSpace(mediaValue)) continue;
                    // Only XML-declared media are resolved. For modern ES-DE XML,
                    // never traverse settings/themes/gamelists looking for artwork:
                    // the only additional allowed media domain is downloaded_media.
                    var permitted = hasHistoric ? mediaRoot :
                        hasLocal ? romRoot : esdeMediaRoot;
                    var resolved = ResolveRelative(Path.GetDirectoryName(xmlFile)!, mediaValue, permitted);
                    var relative = NormalizeRelative(Path.GetRelativePath(root, resolved));
                    var present = File.Exists(resolved);
                    if (!present)
                        diagnostics.Add(new("MEDIA_MISSING", platform, relative, "Media unavailable; no fetch."));
                    media.Add(new(field, relative, present));
                }

                var discs = new List<string>();
                if (exists && Path.GetExtension(romFile).Equals(".m3u", StringComparison.OrdinalIgnoreCase))
                {
                    // Do not read a large or changing playlist into unbounded memory.
                    // StreamReader detects UTF-8/UTF-16 BOMs, preserving historical imports.
                    var data = ReadBoundedBytes(romFile, PlaylistByteLimit,
                        "ESDE_PLAYLIST_BYTES_LIMIT");
                    using var playlist = new StreamReader(new MemoryStream(data, writable: false),
                        new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true);
                    string? line;
                    var lineCount = 0;
                    try
                    {
                        while ((line = playlist.ReadLine()) is not null)
                        {
                            if (++lineCount > PlaylistLineLimit)
                                throw new InvalidDataException("ESDE_PLAYLIST_LINES_LIMIT");
                            var disc = line.Trim();
                            if (disc.Length == 0 || disc.StartsWith('#')) continue;
                            var discFile = ResolveRelative(Path.GetDirectoryName(romFile)!, disc, romRoot);
                            var discRelative = NormalizeRelative(Path.GetRelativePath(root, discFile));
                            discs.Add(discRelative);
                            if (!File.Exists(discFile))
                                diagnostics.Add(new("DISC_MISSING", platform, discRelative, "Playlist member unavailable."));
                        }
                    }
                    catch (DecoderFallbackException)
                    {
                        throw new InvalidDataException("ESDE_PLAYLIST_ENCODING_UNSUPPORTED");
                    }
                }
                var genre = OptionalMetadataField(entry, "genre", platform, romRelative, diagnostics);
                var developer = OptionalMetadataField(entry, "developer", platform, romRelative, diagnostics);
                var dateText = OptionalMetadataField(entry, "releasedate", platform, romRelative, diagnostics);
                var releaseDate = AcceptedReleaseDate(dateText, platform, romRelative, diagnostics);
                games.Add(new EsDeGame("esde-" + id, "variant-" + id, platform, title.Trim(),
                    romRelative, exists, SingleField(entry, "desc"), media.ToArray(), discs.ToArray())
                {
                    Genre = genre,
                    Developer = developer,
                    ReleaseDate = releaseDate
                });
            }
        }

        if (!xmlFound) throw new InvalidDataException("No supported ES-DE gamelist in the selected root.");
        var sorted = games.OrderBy(x => x.Platform, StringComparer.Ordinal)
            .ThenBy(x => x.RomRelativePath, StringComparer.Ordinal).ToArray();
        var evidence = string.Join("\n", sorted.Select(g =>
            g.GameId + "|" + g.Title + "|" + g.Description + "|" + g.RomExists + "|" +
            string.Join(",", g.Media.Select(m => m.Kind + ":" + m.RelativePath + ":" + m.Exists)) + "|" +
            string.Join(",", g.DiscPaths) +
            // Do not change the legacy fingerprint when no new metadata exists.
            // Metadata-only edits must nonetheless change the projection fingerprint.
            (g.Genre is null && g.Developer is null && g.ReleaseDate is null ? "" :
                "|NEXT04|" + FingerprintField(g.Genre) + FingerprintField(g.Developer) +
                FingerprintField(g.ReleaseDate))));
        return new(libraryId, "ES_DE_READ_ONLY",
            Digest(evidence), sorted, diagnostics.ToArray());
    }

    internal static string ResolveSnapshotMedia(string selectedRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidDataException("Missing media path.");
        var normalized = relativePath.Replace('\\', '/');
        // A media reference belongs to exactly one explicitly selected domain.
        var parts = normalized.Split('/');
        if (parts.Length < 3 ||
            (parts[0] != "ROMS" && parts[0] != "MEDIAS" &&
             (parts[0] != "ES-DE" ||
              (parts.Length < 4 || parts[1] != "downloaded_media"))))
            throw new InvalidDataException("Media domain is not authorized.");
        var root = Path.GetFullPath(selectedRoot);
        RequireOrdinaryDirectory(root);
        var domain = parts[0] == "ES-DE"
            ? Path.Combine(root, "ES-DE", "downloaded_media")
            : Path.Combine(root, parts[0]);
        RequireOrdinaryDirectory(domain);
        return ResolveRelative(root, normalized, domain);
    }

    /// <summary>Loads one ES-DE gamelist in read-only mode with bounded XML bytes and characters.</summary>
    /// <remarks>The byte budget is enforced during the actual read, not only by a prior
    /// FileInfo length check. This prevents unbounded reads after concurrent growth.
    /// DTDs and external XML resources remain prohibited; no content is written.</remarks>
    private static XDocument LoadXml(string path)
    {
        var data = ReadBoundedBytes(path, XmlLimit, "ESDE_XML_BYTES_LIMIT");
        var options = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = XmlLimit
        };
        using var input = new MemoryStream(data, writable: false);
        using var reader = XmlReader.Create(input, options);
        return XDocument.Load(reader, LoadOptions.None);
    }

    /// <summary>Reads at most the specified number of bytes from a single opened file.</summary>
    /// <remarks>Rejects both oversized files and files growing concurrently. Does not
    /// follow symbolic links intentionally; callers validate reparse points and roots.</remarks>
    private static byte[] ReadBoundedBytes(string path, int maxBytes, string limitCode)
    {
        using var input = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > maxBytes) throw new InvalidDataException(limitCode);
        using var output = new MemoryStream();
        var buffer = new byte[32768];
        while (true)
        {
            var remaining = maxBytes + 1L - output.Length;
            if (remaining <= 0) throw new InvalidDataException(limitCode);
            var read = input.Read(buffer, 0, (int)Math.Min(buffer.Length, remaining));
            if (read == 0) break;
            output.Write(buffer, 0, read);
            if (output.Length > maxBytes) throw new InvalidDataException(limitCode);
        }
        return output.ToArray();
    }

    private static string? SingleField(XElement entry, string localName)
    {
        var values = entry.Elements().Where(x => x.Name == localName).ToArray();
        if (values.Length > 1) throw new InvalidDataException("Repeated ES-DE field: " + localName);
        return values.Length == 0 ? null : values[0].Value;
    }

    // A malformed optional metadata field must not discard an otherwise valid game.
    // Required path and identity fields keep their existing fail-closed policy.
    private static string? OptionalMetadataField(XElement entry, string name,
        string platform, string romRelative, List<EsDeDiagnostic> diagnostics)
    {
        var fields = entry.Elements().Where(x => x.Name == name).ToArray();
        if (fields.Length == 0) return null;
        if (fields.Length != 1)
        {
            diagnostics.Add(new("METADATA_FIELD_INVALID", platform, romRelative,
                "Repeated optional ES-DE field " + name + " ignored."));
            return null;
        }
        var value = fields[0].Value.Trim();
        if (value.Length == 0) return null;
        if (value.Length > 256)
        {
            diagnostics.Add(new("METADATA_FIELD_INVALID", platform, romRelative,
                "Oversized optional ES-DE field " + name + " ignored."));
            return null;
        }
        return value;
    }

    // ES-DE normally writes yyyyMMddTHHmmss. Preserve the original XML text
    // for a valid date; do not invent a date or silently interpret local culture.
    private static string? AcceptedReleaseDate(string? raw, string platform,
        string romRelative, List<EsDeDiagnostic> diagnostics)
    {
        if (raw is null) return null;
        if (DateTime.TryParseExact(raw,
                ["yyyyMMdd'T'HHmmss", "yyyyMMdd", "yyyy-MM-dd", "yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            return raw;
        diagnostics.Add(new("RELEASE_DATE_UNSUPPORTED", platform, romRelative,
            "Unrecognized ES-DE release date; the game was preserved without a date."));
        return null;
    }

    private static string FingerprintField(string? value) =>
        (value?.Length.ToString(CultureInfo.InvariantCulture) ?? "-1") + ":" + value + "|";

    private static string ResolveRelative(string baseDirectory, string raw, string permittedRoot)
    {
        var value = raw.Trim().Replace('\\', '/');
        if (value.Length == 0 || value[0] == '/' || value.Contains(':') || value.Contains('\0') ||
            Path.IsPathRooted(value))
            throw new InvalidDataException("Absolute, URI, or malformed ES-DE path refused.");
        var resolved = Path.GetFullPath(Path.Combine(baseDirectory, value.Replace('/', Path.DirectorySeparatorChar)));
        AssertWithin(resolved, permittedRoot);
        AssertNoReparsePoints(resolved);
        return resolved;
    }

    private static void AssertWithin(string path, string root)
    {
        var prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        // Linux filesystems are often case-sensitive: an IgnoreCase check could
        // mistake a differently cased sibling for an authorized directory.
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (!Path.GetFullPath(path).StartsWith(prefix, comparison))
            throw new InvalidDataException("ES-DE path escapes its explicitly authorized root.");
    }

    private static void RequireOrdinaryDirectory(string path)
    {
        if (!Directory.Exists(path)) throw new DirectoryNotFoundException("Required library directory unavailable.");
        AssertNoReparsePoints(path);
    }

    private static void AssertNoReparsePoints(string path)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var component in full[root.Length..].Split(Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, component);
            if ((File.Exists(current) || Directory.Exists(current)) &&
                (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Reparse point not followed: " + component);
        }
    }

    private static string NormalizeRelative(string relative) => relative.Replace('\\', '/');

    private static string Digest(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
}
