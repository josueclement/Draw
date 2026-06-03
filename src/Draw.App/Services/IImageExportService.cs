using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace Draw.App.Services;

/// <summary>Renders a control to a raster image for file export or the clipboard.</summary>
public interface IImageExportService
{
    Task ExportPngAsync(Control target, string path, double scale = 1d);

    Task CopyPngToClipboardAsync(Control target);
}

public sealed class ImageExportService : IImageExportService
{
    public async Task ExportPngAsync(Control target, string path, double scale = 1d)
    {
        using RenderTargetBitmap bitmap = Render(target, scale);
        await using FileStream stream = File.Create(path);
        bitmap.Save(stream);
    }

    public async Task CopyPngToClipboardAsync(Control target)
    {
        IClipboard? clipboard = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        // Clipboard image support is platform-dependent; Avalonia 12's SetBitmapAsync is the
        // portable entry point (backed by the platform clipboard).
        using RenderTargetBitmap bitmap = Render(target, 1d);
        await clipboard.SetBitmapAsync(bitmap);
    }

    private static RenderTargetBitmap Render(Control target, double scale)
    {
        double width = Math.Max(1d, target.Bounds.Width);
        double height = Math.Max(1d, target.Bounds.Height);
        PixelSize pixelSize = new(
            (int)Math.Ceiling(width * scale),
            (int)Math.Ceiling(height * scale));

        RenderTargetBitmap bitmap = new(pixelSize, new Vector(96d * scale, 96d * scale));
        bitmap.Render(target);
        return bitmap;
    }
}
