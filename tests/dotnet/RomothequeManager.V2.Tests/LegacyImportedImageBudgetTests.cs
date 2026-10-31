using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using RomothequeManager.V2;

namespace RomothequeManager.V2.Tests;

/// <summary>Only synthetic PNG bytes, no real ROMs, SSD, network or media grants.</summary>
[TestClass]
public sealed class LegacyImportedImageBudgetTests
{
    [TestMethod]
    public void LM_01_ValidCompressedPngAboveFourMiBMustBeRejectedBeforeDecode()
    {
        var image = FakePng(2048, 1024, randomPixels: true);
        Assert.IsTrue(image.Length > LegacyImportedImageBudget.MaxCompressedBytes,
            "LM01_REQUIRES_GENUINELY_OVERSIZED_DECODABLE_PNG");
        Assert.IsFalse(LegacyImportedImageBudget.Preflight(image, out _),
            "LM01_COMPRESSED_CAP_MUST_REJECT_BEFORE_BITMAP");
    }

    [TestMethod]
    public void LM_02_Genuine4096SquareSmallCompressedImageMustBeRejectedBeforeDecode()
    {
        var image = FakePng(4096, 4096);
        Assert.IsTrue(image.Length < LegacyImportedImageBudget.MaxCompressedBytes,
            "LM02_COMPRESSED_PNG_SMALL_ENOUGH_TO_BYPASS_BYTES_ONLY");
        Assert.IsFalse(LegacyImportedImageBudget.Preflight(image, out _),
            "LM02_PIXEL_DIMENSIONS_MUST_REJECT_BEFORE_BITMAP");
    }

    [TestMethod]
    public void LM_02_AxisOnly2049x512RealPngMustBeRefusedBeforeDecode()
    {
        // 2049 * 512 = 1,049,088 pixels: area and compressed size stay in budget.
        // Width alone violates the independent 2048-pixel-per-axis limit.
        const int width = 2049, height = 512;
        var image = FakePng(width, height);
        Assert.IsTrue(image.Length < LegacyImportedImageBudget.MaxCompressedBytes,
            "LM02_AXIS_ONLY_COMPRESSED_BYTES_WITHIN_BUDGET");
        Assert.IsTrue((long)width * height < LegacyImportedImageBudget.MaxSourcePixels,
            "LM02_AXIS_ONLY_SURFACE_WITHIN_BUDGET");

        // Separate reference decode proves this is a genuine decodable PNG,
        // not a header-only fixture. This is NOT the guarded Decode call.
        using (var reference = new MemoryStream(image, writable: false))
        using (var control = new Avalonia.Media.Imaging.Bitmap(reference))
        {
            Assert.AreEqual(width, control.PixelSize.Width, "LM02_REFERENCE_WIDTH");
            Assert.AreEqual(height, control.PixelSize.Height, "LM02_REFERENCE_HEIGHT");
        }

        var decoderEntered = 0;
        using var stream = new MemoryStream(image, writable: false);
        var result = LegacyImportedImageBudget.Decode(stream, LegacyImageSlot.Card,
            CancellationToken.None, () => Interlocked.Increment(ref decoderEntered));
        try
        {
            Assert.AreEqual("MEDIA_DIMENSIONS_EXCEEDED", result.Reason,
                "LM02_AXIS_ONLY_MUST_REJECT_WITH_EXACT_REASON");
            Assert.AreEqual(0, decoderEntered,
                "LM02_AXIS_ONLY_NATIVE_DECODER_MUST_NOT_BE_ENTERED");
            Assert.IsNull(result.Bitmap, "LM02_AXIS_ONLY_NO_BITMAP_ADOPTED");
        }
        finally { result.Bitmap?.Dispose(); }
    }

    [TestMethod]
    public void LM_02_Square2048WithinAxisButExceedingAreaMustBeRejected()
    {
        var image = FakePng(2048, 2048);
        Assert.IsFalse(LegacyImportedImageBudget.Preflight(image, out _),
            "LM02_AREA_GUARD_MUST_REJECT_EVEN_WITH_ACCEPTABLE_AXES");
    }

    [TestMethod]
    public void LM_02_2048x1024BoundaryIsValidUnderBothAxisAndAreaCaps()
    {
        var image = FakePng(2048, 1024);
        Assert.IsTrue(LegacyImportedImageBudget.Preflight(image, out _),
            "LM02_EXACT_TWO_MIP_BOUNDARY_MUST_BE_ACCEPTED");
    }

    [TestMethod]
    public void LM_02_CorruptedPngHeaderCrcMustBeRejectedBeforeNativeDecode()
    {
        var image = FakePng(128, 128);
        image[29] ^= 0x80; // mutate IHDR CRC, preserve all dimensions/pixels.
        Assert.IsFalse(LegacyImportedImageBudget.Preflight(image, out _),
            "LM02_MALFORMED_IHDR_MUST_NOT_REACH_BITMAP");
    }

    [TestMethod]
    public void LM_03_SmallValidStaticPngRemainsAccepted()
    {
        var image = FakePng(96, 64);
        Assert.IsTrue(LegacyImportedImageBudget.Preflight(image, out _),
            "LM03_VALID_SMALL_PNG_MUST_NOT_BE_LOST");
    }

    // A complete valid PNG (CRC, IHDR, IDAT using zlib and IEND), NOT a fake
    // header. Random RGBA rows yield a truly decodable >4 MiB compressed PNG.
    internal static byte[] FakePng(int width, int height, bool randomPixels = false)
    {
        using var body = new MemoryStream();
        using (var zlib = new ZLibStream(body, CompressionLevel.Fastest, leaveOpen: true))
        {
            var row = new byte[checked(width * 4 + 1)];
            var random = new Random(14012026);
            for (var y = 0; y < height; y++)
            {
                row[0] = 0; // PNG filter None.
                if (randomPixels) random.NextBytes(row.AsSpan(1));
                zlib.Write(row);
            }
        }
        using var png = new MemoryStream();
        png.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var header = new byte[13];
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(0, 4), width);
        BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4, 4), height);
        header[8] = 8;
        header[9] = 6; // RGBA, 8 bits per channel.
        WriteChunk(png, "IHDR", header);
        WriteChunk(png, "IDAT", body.ToArray());
        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream output, string tag, byte[] data)
    {
        var n = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(n, data.Length);
        output.Write(n);
        var type = Encoding.ASCII.GetBytes(tag);
        output.Write(type);
        output.Write(data);
        uint crc = 0xFFFFFFFFu;
        foreach (var b in type) crc = CrcByte(crc, b);
        foreach (var b in data) crc = CrcByte(crc, b);
        BinaryPrimitives.WriteUInt32BigEndian(n, ~crc);
        output.Write(n);
    }

    private static uint CrcByte(uint crc, byte value)
    {
        crc ^= value;
        for (var i = 0; i < 8; i++)
            crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        return crc;
    }
}
