using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace RomothequeManager.V2;

/// <summary>
/// ESDE-FP-V2 (#142) binary fingerprint contract. This serializer is deliberately
/// separate from the legacy Digest(string) used for GameId and VariantId.
/// Only explicitly imported ES-DE projections are accepted; no filesystem access.
/// </summary>
internal static class EsDeFingerprintV2
{
    /// <summary>Versioned identifier written before the complete lowercase 64-digit SHA-256 digest; never used for GameId or VariantId.</summary>
    internal const string Prefix = "esde-fp-v2:sha256:";
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    // EXACT contract magic including the trailing NUL; no BOM or culture formatting.
    private static ReadOnlySpan<byte> Magic => "ROMOTHEQUE:ESDE-FP:V2\0"u8;

    /// <summary>Hash the full canonical ES-DE projection without I/O, migration or changes to historical identity digests.</summary>
    internal static string Compute(IReadOnlyList<EsDeGame> games, string sourceKind = "ES_DE_READ_ONLY")
    {
        var bytes = CanonicalBytes(games, sourceKind);
        return Prefix + Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>Accept only an exact V2 prefix and full lowercase hexadecimal SHA-256; legacy V1 and unknown versions fail closed.</summary>
    internal static bool IsSupportedV2Fingerprint(string? fingerprint)
    {
        if (fingerprint is null || fingerprint.Length != Prefix.Length + 64 ||
            !fingerprint.StartsWith(Prefix, StringComparison.Ordinal))
            return false;
        foreach (var c in fingerprint.AsSpan(Prefix.Length))
        {
            if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f'))) return false;
        }
        return true;
    }

    // Exposed internally only so independent golden vectors can pin exact bytes,
    // not merely duplicate the product's SHA256 output.
    /// <summary>Serialize typed TLV fields with strict UTF-8, stable game/media ordering and ordered disc paths for independent byte-vector verification.</summary>
    internal static byte[] CanonicalBytes(IReadOnlyList<EsDeGame> games, string sourceKind = "ES_DE_READ_ONLY")
    {
        if (games is null) throw new InvalidDataException("ESDE_FPV2_GAMES_NULL");
        if (games.Any(g => g is null)) throw new InvalidDataException("ESDE_FPV2_GAME_NULL");
        using var stream = new MemoryStream();
        stream.Write(Magic);
        WriteStringField(stream, 1, sourceKind, required: true);
        var ordered = games.OrderBy(g => g.Platform, StringComparer.Ordinal)
            .ThenBy(g => g.RomRelativePath, StringComparer.Ordinal)
            .ThenBy(g => g.GameId, StringComparer.Ordinal).ToArray();
        WriteRecordListField(stream, 2, ordered, WriteGame);
        return stream.ToArray();
    }

    private static void WriteGame(Stream stream, EsDeGame game)
    {
        WriteStringField(stream, 1, game.GameId, required: true);
        WriteStringField(stream, 2, game.VariantId, required: true);
        WriteStringField(stream, 3, game.Platform, required: true);
        WriteStringField(stream, 4, game.Title, required: true);
        WriteStringField(stream, 5, game.RomRelativePath, required: true);
        WriteBooleanField(stream, 6, game.RomExists);
        WriteStringField(stream, 7, game.Description);
        WriteStringField(stream, 8, game.Genre);
        WriteStringField(stream, 9, game.Developer);
        WriteStringField(stream, 10, game.ReleaseDate);
        if (game.Media is null || game.Media.Any(m => m is null))
            throw new InvalidDataException("ESDE_FPV2_MEDIA_NULL");
        var orderedMedia = game.Media.OrderBy(m => m.Kind, StringComparer.Ordinal)
            .ThenBy(m => m.RelativePath, StringComparer.Ordinal)
            .ThenBy(m => m.Exists).ToArray();
        WriteRecordListField(stream, 11, orderedMedia, WriteMedia);
        WriteStringListField(stream, 12, game.DiscPaths);
    }

    private static void WriteMedia(Stream stream, EsDeMedia media)
    {
        WriteStringField(stream, 1, media.Kind, required: true);
        WriteStringField(stream, 2, media.RelativePath, required: true);
        WriteBooleanField(stream, 3, media.Exists);
    }

    private static void WriteStringField(Stream stream, ushort id, string? value, bool required = false)
    {
        WriteHeader(stream, id, 0x01);
        WriteStringValue(stream, value, required);
    }

    private static void WriteStringValue(Stream stream, string? value, bool required = false)
    {
        if (value is null)
        {
            if (required) throw new InvalidDataException("ESDE_FPV2_REQUIRED_FIELD_NULL");
            stream.WriteByte(0x00);
            return;
        }
        byte[] encoded;
        try { encoded = StrictUtf8.GetBytes(value); }
        catch (EncoderFallbackException error)
        {
            throw new InvalidDataException("ESDE_FPV2_INVALID_UTF8", error);
        }
        stream.WriteByte(0x01);
        WriteUInt32(stream, checked((uint)encoded.Length));
        stream.Write(encoded);
    }

    private static void WriteBooleanField(Stream stream, ushort id, bool value)
    {
        WriteHeader(stream, id, 0x02);
        stream.WriteByte(value ? (byte)0x01 : (byte)0x00);
    }

    private static void WriteStringListField(Stream stream, ushort id, IReadOnlyList<string> items)
    {
        if (items is null) throw new InvalidDataException("ESDE_FPV2_DISCS_NULL");
        WriteHeader(stream, id, 0x03);
        WriteUInt32(stream, checked((uint)items.Count));
        foreach (var item in items) WriteStringValue(stream, item, required: true);
    }

    private static void WriteRecordListField<T>(
        Stream stream, ushort id, IReadOnlyList<T> items, Action<Stream, T> writeRecord)
    {
        WriteHeader(stream, id, 0x03);
        WriteUInt32(stream, checked((uint)items.Count));
        foreach (var item in items)
        {
            using var record = new MemoryStream();
            writeRecord(record, item);
            WriteUInt32(stream, checked((uint)record.Length));
            record.Position = 0;
            record.CopyTo(stream);
        }
    }

    private static void WriteHeader(Stream stream, ushort fieldId, byte typeTag)
    {
        Span<byte> bytes = stackalloc byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, fieldId);
        stream.Write(bytes);
        stream.WriteByte(typeTag);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        stream.Write(bytes);
    }
}
