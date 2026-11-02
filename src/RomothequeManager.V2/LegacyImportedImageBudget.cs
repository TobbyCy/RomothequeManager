using System.Buffers.Binary;
using System.IO;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

[assembly: InternalsVisibleTo("RomothequeManager.V2.Tests")]

namespace RomothequeManager.V2;

// #141: scoped legacy ES-DE decode helper. Not a Preview service or a new grant.
// Caller MUST use the original EsDeImportSession.TryOpenMedia authorization first.
/// <summary>Presentation destination used to select a strict decoded-image dimension limit.</summary>
internal enum LegacyImageSlot
{
    /// <summary>Small collection card with a strictly reduced image size.</summary>
    Card,
    /// <summary>Dashboard preview constrained to the small card memory budget.</summary>
    Dashboard,
    /// <summary>Selected game detail with a larger but explicitly capped bitmap.</summary>
    Detail,
    /// <summary>Paginated gallery preview, still subject to single-decoder execution.</summary>
    Gallery
}

/// <summary>Bounded bitmap result plus refusal reason, source dimensions and retained-byte estimate; the caller owns a successful Bitmap.</summary>
internal readonly record struct LegacyDecodeResult(Bitmap? Bitmap, string Reason,
    int SourceWidth, int SourceHeight, long RetainedBytes)
{
    /// <summary>Produces a refusal result without retaining any native bitmap or reporting dimensions.</summary>
    internal static LegacyDecodeResult Refused(string reason) => new(null, reason, 0, 0, 0);
}

// All dimensions/limits below are the exact DEV 2 contract in Issue #141.
/// <summary>Applies the legacy media preflight and decode budgets after separate Current-membership path authorization.</summary>
/// <remarks>Neither this utility nor a header match grants access to a personal MEDIAS root.</remarks>
internal static class LegacyImportedImageBudget
{
    /// <summary>Maximum compressed source size admitted before native decoding: four MiB.</summary>
    internal const int MaxCompressedBytes = 4 * 1024 * 1024;
    /// <summary>Maximum JPEG header search extent, bounding metadata parsing work.</summary>
    internal const int MaxHeaderBytes = 256 * 1024;
    /// <summary>Maximum source image width or height before any bitmap decoder is entered.</summary>
    internal const int MaxSidePixels = 2048;
    /// <summary>Maximum permitted source pixel area; wide images are rejected even below side limits.</summary>
    internal const long MaxSourcePixels = 2_097_152;
    /// <summary>Maximum retained per-view RGBA size accepted after decoding and scaling.</summary>
    internal const long MaxViewBytes = 12L * 1024 * 1024;

    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    private static bool ValidIhdrCrc(ReadOnlySpan<byte> png)
    {
        // PNG IHDR type (4 bytes) + data (13 bytes), followed by stored CRC32.
        uint crc = 0xFFFFFFFFu;
        foreach (var b in png.Slice(12, 17))
        {
            crc ^= b;
            for (var i = 0; i < 8; i++)
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xEDB88320u;
        }
        return ~crc == BinaryPrimitives.ReadUInt32BigEndian(png.Slice(29, 4));
    }

    /// <summary>Checks a complete PNG/JPEG byte sequence, dimensions, framing and size before native decode.</summary>
    /// <param name="bytes">Bounded content read from an already authorized stream.</param>
    /// <param name="reason">Explicit code explaining admission or rejection without leaking source paths.</param>
    /// <returns>True only when the header and size constraints admit a later decoder call.</returns>
    internal static bool Preflight(byte[] bytes, out string reason) =>
        TryProbe(bytes.AsSpan(), out _, out _, out reason);

    private static bool TryProbe(ReadOnlySpan<byte> bytes,
        out int width, out int height, out string reason)
    {
        width = 0;
        height = 0;
        reason = "MEDIA_UNSUPPORTED_OR_UNVERIFIED";
        if (bytes.Length > MaxCompressedBytes)
        {
            reason = "MEDIA_TOO_LARGE_COMPRESSED";
            return false;
        }
        if (bytes.Length < 24) return false;
        if (bytes.StartsWith(PngSignature))
        {
            if (bytes.Length < 33 ||
                BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(8, 4)) != 13 ||
                !bytes.Slice(12, 4).SequenceEqual("IHDR"u8) ||
                !ValidIhdrCrc(bytes))
                return false;
            width = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(16, 4));
            height = BinaryPrimitives.ReadInt32BigEndian(bytes.Slice(20, 4));
            // 16-bit-per-channel source expands beyond the 4 bytes/pixel
            // admission model; fail closed until a separate contract exists.
            if (bytes[24] is not (1 or 2 or 4 or 8) ||
                bytes[25] is not (0 or 2 or 3 or 4 or 6) ||
                bytes[26] != 0 || bytes[27] != 0 || bytes[28] is not (0 or 1))
                return false;
            // Reject animation chunks, ambiguous/incomplete chunk framing and
            // content that is not actually a complete PNG. The decoder verifies
            // compressed data itself after the bounds gate.
            var pos = 8;
            bool end = false, image = false;
            while (pos + 12 <= bytes.Length)
            {
                var len = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(pos, 4));
                if (len > (uint)(bytes.Length - pos - 12)) return false;
                var name = bytes.Slice(pos + 4, 4);
                if (name.SequenceEqual("acTL"u8) || name.SequenceEqual("fcTL"u8) ||
                    name.SequenceEqual("fdAT"u8))
                    return false;
                if (name.SequenceEqual("IDAT"u8)) image = true;
                pos = checked(pos + (int)len + 12);
                if (name.SequenceEqual("IEND"u8))
                {
                    end = true;
                    break;
                }
            }
            if (!image || !end || pos != bytes.Length) return false;
        }
        else if (bytes[0] == 0xFF && bytes[1] == 0xD8)
        {
            // JPEG SOF0/1/2: no unbounded EXIF/metadata search; no extension
            // trust. Header needs to appear within 256 KiB.
            var pos = 2;
            var end = Math.Min(bytes.Length, MaxHeaderBytes);
            var found = false;
            while (pos + 3 < end)
            {
                if (bytes[pos++] != 0xFF) return false;
                while (pos < end && bytes[pos] == 0xFF) pos++;
                if (pos >= end) return false;
                var marker = bytes[pos++];
                if (marker == 0xD9 || marker == 0xDA) break;
                if (marker is 0x01 or >= 0xD0 and <= 0xD7) continue;
                if (pos + 2 > end) return false;
                var size = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pos, 2));
                if (size < 2 || pos + size > end) return false;
                if (marker is 0xC0 or 0xC1 or 0xC2)
                {
                    if (size < 8 || bytes[pos + 2] != 8) return false;
                    height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pos + 3, 2));
                    width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(pos + 5, 2));
                    found = true;
                    break;
                }
                pos += size;
            }
            if (!found) return false;
        }
        else return false;

        if (width < 1 || height < 1 || width > MaxSidePixels || height > MaxSidePixels)
        {
            reason = "MEDIA_DIMENSIONS_EXCEEDED";
            return false;
        }
        if (checked((long)width * height) > MaxSourcePixels)
        {
            reason = "MEDIA_PIXELS_EXCEEDED";
            return false;
        }
        reason = "OK";
        return true;
    }

    private static (int Width, int Height) SlotLimits(LegacyImageSlot slot) => slot switch
    {
        LegacyImageSlot.Card or LegacyImageSlot.Dashboard => (192, 256),
        LegacyImageSlot.Gallery => (256, 320),
        LegacyImageSlot.Detail => (384, 512),
        _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };

    // Invoked by a SERIAL worker away from the UI thread. Even the read of an
    // unknown-length stream is capped to MaxCompressedBytes+1, not 50 streams.
    // decodeEntered counts the *actual native bitmap call*, never OPEN_OK.
    /// <summary>Reads one previously authorized stream under byte/time cancellation and bitmap dimension budgets.</summary>
    /// <remarks>Must run in a serialized worker; rejects stale generations and disposes every non-adopted bitmap. No extra preview permission is granted here.</remarks>
    /// <param name="source">Read-only stream opened by EsDeImportSession membership and confinement checks.</param>
    /// <param name="slot">Destination determining strict output dimensions.</param>
    /// <param name="token">Cancellation token used before read and native decode adoption.</param>
    /// <param name="decodeEntered">Optional test observer invoked only on actual decoder entry.</param>
    /// <returns>Adoptable bitmap with retained-byte evidence, or a refusal without bitmap ownership.</returns>
    internal static LegacyDecodeResult Decode(Stream source, LegacyImageSlot slot,
        CancellationToken token, Action? decodeEntered = null)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(source);
            token.ThrowIfCancellationRequested();
            if (source.CanSeek)
            {
                try
                {
                    if (source.Length - source.Position > MaxCompressedBytes)
                        return LegacyDecodeResult.Refused("MEDIA_TOO_LARGE_COMPRESSED");
                }
                catch (NotSupportedException) { /* streaming input is bounded below */ }
            }
            var bytes = new byte[MaxCompressedBytes + 1];
            var read = 0;
            while (read <= MaxCompressedBytes)
            {
                token.ThrowIfCancellationRequested();
                var count = source.Read(bytes, read, bytes.Length - read);
                if (count == 0) break;
                read += count;
                if (read > MaxCompressedBytes)
                    return LegacyDecodeResult.Refused("MEDIA_TOO_LARGE_COMPRESSED");
            }
            if (!TryProbe(bytes.AsSpan(0, read), out var width, out var height,
                out var reason))
                return LegacyDecodeResult.Refused(reason);
            var cap = SlotLimits(slot);
            var scale = Math.Min(1d, Math.Min(cap.Width / (double)width,
                cap.Height / (double)height));
            var targetWidth = Math.Max(1, (int)Math.Floor(width * scale));
            var targetHeight = Math.Max(1, (int)Math.Floor(height * scale));
            token.ThrowIfCancellationRequested();
            using var data = new MemoryStream(bytes, 0, read, writable: false);
            decodeEntered?.Invoke();
            Bitmap? bitmap = null;
            try
            {
                // Decode to the output size where possible; unlike Stretch this
                // requests decoding at a reduced size, but NO native-RSS claim.
                bitmap = targetWidth < width ?
                    Bitmap.DecodeToWidth(data, targetWidth) :
                    targetHeight < height ? Bitmap.DecodeToHeight(data, targetHeight) :
                    new Bitmap(data);
                var actual = bitmap.PixelSize;
                if (actual.Width < 1 || actual.Height < 1 ||
                    actual.Width > cap.Width || actual.Height > cap.Height)
                    return LegacyDecodeResult.Refused("MEDIA_DECODE_OUTPUT_EXCEEDED");
                var retained = checked((long)actual.Width * actual.Height * 4);
                if (retained > MaxViewBytes)
                    return LegacyDecodeResult.Refused("MEDIA_BUDGET_EXHAUSTED");
                if (token.IsCancellationRequested)
                    return LegacyDecodeResult.Refused("MEDIA_STALE_GENERATION");
                var accepted = bitmap;
                bitmap = null; // transfer ownership to the caller.
                return new LegacyDecodeResult(accepted, "DECODE_OK",
                    width, height, retained);
            }
            finally { bitmap?.Dispose(); }
        }
        catch (OperationCanceledException) { return LegacyDecodeResult.Refused("MEDIA_STALE_GENERATION"); }
        catch (Exception ex) when (ex is IOException or ArgumentException or
            InvalidDataException or InvalidOperationException or NotSupportedException or
            OverflowException or System.Runtime.InteropServices.COMException or
            OutOfMemoryException)
        {
            return LegacyDecodeResult.Refused("MEDIA_DECODE_FAILED");
        }
    }
}
