using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using RomothequeManager.V2.Inventory.Media;

namespace RomothequeManager.V2.MvvmFoundation;

/// <summary>Converts only a backend-owned RGBA memory buffer into an Avalonia
/// bitmap. Does not accept, reconstruct or open local paths or URIs.</summary>
internal static class IdealMediaBitmapAdapter
{
    internal static WriteableBitmap? Create(MediaPreviewResult? preview)
    {
        if (preview is not { Status: MediaPreviewStatus.Ready, Image: { } image })
            return null;
        if (image.Width is < 1 or > 512 || image.Height is < 1 or > 512)
            return null;
        var stride = checked(image.Width * 4);
        var expected = checked(stride * image.Height);
        if (expected > 512 * 512 * 4 || image.Pixels.Length != expected)
            return null;

        // Native Skia PREMUL RGBA8888; match Avalonia AlphaFormat.Premul.
        var bitmap = new WriteableBitmap(new PixelSize(image.Width, image.Height),
            new Vector(96, 96), PixelFormat.Rgba8888, AlphaFormat.Premul);
        try
        {
            var bytes = image.Pixels.ToArray();
            using (var frame = bitmap.Lock())
            {
                if (frame.RowBytes < stride)
                    throw new InvalidDataException("MEDIA_RENDER_STRIDE_INVALID");
                for (var row = 0; row < image.Height; ++row)
                    Marshal.Copy(bytes, row * stride,
                        IntPtr.Add(frame.Address, checked(row * frame.RowBytes)), stride);
            }
            Array.Clear(bytes);
            return bitmap;
        }
        catch
        {
            bitmap.Dispose();
            return null; // never guess a path-based cover on render failure
        }
    }
}
