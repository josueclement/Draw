using System.Threading.Tasks;
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
