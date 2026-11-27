using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using SkiaSharp;
using Chronikologio.Core;

namespace Chronikologio.UI;

/// <summary>
/// Loads the photographs and icons of the database: WebP (the format of the files since 0.5.0, decoded here because GDI+ does not read it) and, for
/// older or hand-added files, JPEG and PNG. The file is read completely first, so it is not locked while the image is shown. Failures are
/// reported as <see cref="ArgumentException"/> (what the callers already catch for a damaged file).
/// </summary>
public static class ImageLoader
{
    private const int MaxFileBytes = 32 * 1024 * 1024;
    private const long MaxPixels = 16_000_000;

    private static void CheckDimensions(int width, int height)
    {
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
            throw new ArgumentException("Image dimensions exceed the decoding limit.");
    }

    public static Image Load(string path)
    {
        byte[] bytes;
        try
        {
            using var stream = File.OpenRead(path);
            if (stream.Length > MaxFileBytes) throw new ArgumentException("Image exceeds the file size limit.");
            bytes = new byte[(int)stream.Length];
            stream.ReadExactly(bytes);
        }
        catch (UnauthorizedAccessException ex) { throw new IOException(ex.Message, ex); }
        return IsWebp(bytes) ? DecodeWebp(bytes) : DecodeGdi(bytes);
    }

    public static Image LoadFromStore(DayStore store, ImageRef image)
    {
        try
        {
            var bytes = store.ReadImageBytes(image);
            return IsWebp(bytes) ? DecodeWebp(bytes) : DecodeGdi(bytes);
        }
        catch (UnauthorizedAccessException ex) { throw new IOException(ex.Message, ex); }
    }

    private static bool IsWebp(byte[] b) => b.Length > 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P';

    private static Image DecodeGdi(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var original = Image.FromStream(stream, useEmbeddedColorManagement: false, validateImageData: true);
        CheckDimensions(original.Width, original.Height);
        return new Bitmap(original);   // an independent copy: nothing keeps the stream
    }

    private static Image DecodeWebp(byte[] bytes)
    {
        try
        {
            using var skData = SKData.CreateCopy(bytes);
            using var codec = SKCodec.Create(skData) ?? throw new ArgumentException("damaged WebP image");
            CheckDimensions(codec.Info.Width, codec.Info.Height);
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var skia = SKBitmap.Decode(codec, info) ?? throw new ArgumentException("damaged WebP image");
            var bitmap = new Bitmap(info.Width, info.Height, PixelFormat.Format32bppArgb);
            var data = bitmap.LockBits(new Rectangle(0, 0, info.Width, info.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                var source = skia.GetPixels();
                var row = new byte[info.Width * 4];
                for (var y = 0; y < info.Height; y++)
                {
                    Marshal.Copy(source + y * skia.RowBytes, row, 0, row.Length);
                    Marshal.Copy(row, 0, data.Scan0 + y * data.Stride, row.Length);
                }
            }
            finally { bitmap.UnlockBits(data); }
            return bitmap;
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException or IOException)
        {
            throw new ArgumentException("damaged WebP image: " + ex.Message, ex);
        }
    }
}
