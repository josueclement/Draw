using System.Threading.Tasks;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;

namespace Draw.App.Services;

public sealed class ClipboardService : IClipboardService
{
    // Application-specific clipboard format: only Draw (this or another instance) can read it, and it
    // does not pollute the plain-text clipboard. The identifier allows only ASCII letters/digits/.-.
    private static readonly DataFormat<string> DrawClip = DataFormat.CreateStringApplicationFormat("draw-clip");

    public async Task SetClipAsync(string drawJson, Bitmap? bitmap)
    {
        IClipboard? clipboard = DesktopHost.Clipboard;
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
        IClipboard? clipboard = DesktopHost.Clipboard;
        return clipboard is null ? null : await clipboard.TryGetValueAsync(DrawClip);
    }

    public async Task<Bitmap?> TryGetBitmapAsync()
    {
        IClipboard? clipboard = DesktopHost.Clipboard;
        return clipboard is null ? null : await clipboard.TryGetBitmapAsync();
    }
}
