using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace Draw.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    private static readonly FilePickerFileType DrawFileType = new("Draw diagram")
    {
        Patterns = new[] { "*.draw" },
    };

    private static readonly FilePickerFileType PngFileType = new("PNG image")
    {
        Patterns = new[] { "*.png" },
    };

    private static readonly FilePickerFileType JpegFileType = new("JPEG image")
    {
        Patterns = new[] { "*.jpg", "*.jpeg" },
    };

    private static readonly FilePickerFileType SvgFileType = new("SVG image")
    {
        Patterns = new[] { "*.svg" },
    };

    private static readonly FilePickerFileType ImageFileType = new("Image")
    {
        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.bmp", "*.gif" },
    };

    public Task<string?> PickOpenAsync()
        => RunOpenPickerAsync(new FilePickerOpenOptions
        {
            Title = "Open diagram",
            AllowMultiple = false,
            FileTypeFilter = new[] { DrawFileType },
        });

    public Task<string?> PickSaveAsync(string? suggestedFileName)
        => RunSavePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save diagram",
            SuggestedFileName = suggestedFileName ?? "diagram",
            DefaultExtension = "draw",
            FileTypeChoices = new[] { DrawFileType },
        });

    public Task<string?> PickSaveImageAsync(string? suggestedFileName)
        => RunSavePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export image",
            SuggestedFileName = suggestedFileName ?? "diagram",
            DefaultExtension = "png",
            FileTypeChoices = new[] { PngFileType, JpegFileType },
        });

    public Task<string?> PickSaveSvgAsync(string? suggestedFileName)
        => RunSavePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export SVG",
            SuggestedFileName = suggestedFileName ?? "diagram",
            DefaultExtension = "svg",
            FileTypeChoices = new[] { SvgFileType },
        });

    public Task<string?> PickOpenImageAsync()
        => RunOpenPickerAsync(new FilePickerOpenOptions
        {
            Title = "Insert image",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType },
        });

    private static async Task<string?> RunOpenPickerAsync(FilePickerOpenOptions options)
    {
        IStorageProvider? storage = DesktopHost.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(options).ConfigureAwait(true);
        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static async Task<string?> RunSavePickerAsync(FilePickerSaveOptions options)
    {
        IStorageProvider? storage = DesktopHost.StorageProvider;
        if (storage is null)
        {
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(options).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }
}
