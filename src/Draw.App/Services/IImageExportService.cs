using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Draw.App.Services;

/// <summary>Raster formats the image export can write.</summary>
public enum ImageExportFormat
{
    Png,
    Jpeg,
}

/// <summary>Encodes a pre-rendered diagram bitmap to a file or the clipboard. The caller owns the
/// rendering (zoom-independent, grid-free) via <c>DiagramView.RenderContentBitmap</c>; this service only
/// encodes.</summary>
public interface IImageExportService
{
    Task SaveAsync(RenderTargetBitmap bitmap, string path, ImageExportFormat format);

    Task CopyToClipboardAsync(RenderTargetBitmap bitmap);
}

public sealed class ImageExportService : IImageExportService
{
    private const int JpegQuality = 92;

    public async Task SaveAsync(RenderTargetBitmap bitmap, string path, ImageExportFormat format)
    {
        await using FileStream stream = File.Create(path);
        if (format == ImageExportFormat.Jpeg)
        {
            EncodeJpeg(bitmap, stream);
        }
        else
        {
            // Avalonia's Bitmap.Save writes PNG (with alpha).
            bitmap.Save(stream);
        }
    }

    public async Task CopyToClipboardAsync(RenderTargetBitmap bitmap)
    {
        IClipboard? clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        // Clipboard image support is platform-dependent; Avalonia 12's SetBitmapAsync is the portable
        // entry point (backed by the platform clipboard).
        await clipboard.SetBitmapAsync(bitmap);
    }

    // JPEG has no alpha, so flatten the (possibly transparent) diagram onto white. Round-tripping through
    // Avalonia's PNG encoder sidesteps pixel-format/stride/premultiplication matching between the two
    // bitmap libraries; the PNG step is lossless and this runs once per export.
    private static void EncodeJpeg(RenderTargetBitmap bitmap, Stream output)
    {
        using MemoryStream png = new();
        bitmap.Save(png);
        png.Position = 0;

        using SKBitmap decoded = SKBitmap.Decode(png);
        SKImageInfo info = new(decoded.Width, decoded.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
        using SKBitmap opaque = new(info);
        using (SKCanvas canvas = new(opaque))
        {
            canvas.Clear(SKColors.White);
            canvas.DrawBitmap(decoded, 0, 0);
        }

        using SKImage image = SKImage.FromBitmap(opaque);
        using SKData data = image.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        data.SaveTo(output);
    }
}
