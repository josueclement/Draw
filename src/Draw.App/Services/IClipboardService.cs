using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace Draw.App.Services;

/// <summary>
/// Reads/writes diagram clipboard content through the platform clipboard, keeping view models free of
/// any direct <c>Avalonia.Controls</c> dependency. Diagram content travels under a custom application
/// format; a bitmap is attached alongside it for interop with external apps.
/// </summary>
public interface IClipboardService
{
    /// <summary>Writes a Draw clipboard payload; when <paramref name="bitmap"/> is non-null it is also
    /// placed on the clipboard so other applications can paste the image.</summary>
    Task SetClipAsync(string drawJson, Bitmap? bitmap);

    /// <summary>Returns the Draw clipboard payload, or <c>null</c> when the clipboard holds none.</summary>
    Task<string?> TryGetClipAsync();

    /// <summary>Returns a bitmap from the clipboard (e.g. an external screenshot), or <c>null</c>.</summary>
    Task<Bitmap?> TryGetBitmapAsync();
}

public sealed class ClipboardService : IClipboardService
{
    // Application-specific clipboard format: only Draw (this or another instance) can read it, and it
    // does not pollute the plain-text clipboard. The identifier allows only ASCII letters/digits/.-.
    private static readonly DataFormat<string> DrawClip = DataFormat.CreateStringApplicationFormat("draw-clip");

    public async Task SetClipAsync(string drawJson, Bitmap? bitmap)
    {
        IClipboard? clipboard = GetClipboard();
        if (clipboard is null)
        {
            return;
        }

        if (bitmap is null)
        {
            await clipboard.SetValueAsync(DrawClip, drawJson);
            return;
        }

        // One transfer item carrying both representations, so the custom payload and the bitmap are
        // available simultaneously (a second SetData call would overwrite the first).
        DataTransferItem item = DataTransferItem.Create(DrawClip, drawJson);
        item.SetBitmap(bitmap);
        DataTransfer transfer = new();
        transfer.Add(item);
        await clipboard.SetDataAsync(transfer);
    }

    public async Task<string?> TryGetClipAsync()
    {
        IClipboard? clipboard = GetClipboard();
        return clipboard is null ? null : await clipboard.TryGetValueAsync(DrawClip);
    }

    public async Task<Bitmap?> TryGetBitmapAsync()
    {
        IClipboard? clipboard = GetClipboard();
        return clipboard is null ? null : await clipboard.TryGetBitmapAsync();
    }

    private static IClipboard? GetClipboard()
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.Clipboard;
}
