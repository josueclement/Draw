using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace Draw.App.Services;

/// <summary>Encodes a pre-rendered diagram bitmap to a file or the clipboard. The caller owns the
/// rendering (zoom-independent, grid-free) via <c>DiagramView.RenderContentBitmap</c>; this service only
/// encodes.</summary>
public interface IImageExportService
{
    Task SaveAsync(RenderTargetBitmap bitmap, string path, ImageExportFormat format);

    Task CopyToClipboardAsync(RenderTargetBitmap bitmap);
}
