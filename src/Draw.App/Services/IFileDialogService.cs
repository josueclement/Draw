using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;

namespace Draw.App.Services;

/// <summary>Shows native open/save dialogs and returns the chosen local path.</summary>
public interface IFileDialogService
{
    Task<string?> PickOpenAsync();

    Task<string?> PickSaveAsync(string? suggestedFileName);

    Task<string?> PickSaveImageAsync(string? suggestedFileName);

    Task<string?> PickSaveSvgAsync(string? suggestedFileName);

    Task<string?> PickOpenImageAsync();
}

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

    public async Task<string?> PickOpenAsync()
    {
        IStorageProvider? storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open diagram",
            AllowMultiple = false,
            FileTypeFilter = new[] { DrawFileType },
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickSaveAsync(string? suggestedFileName)
    {
        IStorageProvider? storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save diagram",
            SuggestedFileName = suggestedFileName ?? "diagram",
            DefaultExtension = "draw",
            FileTypeChoices = new[] { DrawFileType },
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveImageAsync(string? suggestedFileName)
    {
        IStorageProvider? storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export image",
            SuggestedFileName = suggestedFileName ?? "diagram",
            DefaultExtension = "png",
            FileTypeChoices = new[] { PngFileType, JpegFileType },
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickSaveSvgAsync(string? suggestedFileName)
    {
        IStorageProvider? storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export SVG",
            SuggestedFileName = suggestedFileName ?? "diagram",
            DefaultExtension = "svg",
            FileTypeChoices = new[] { SvgFileType },
        }).ConfigureAwait(true);

        return file?.TryGetLocalPath();
    }

    public async Task<string?> PickOpenImageAsync()
    {
        IStorageProvider? storage = GetStorageProvider();
        if (storage is null)
        {
            return null;
        }

        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Insert image",
            AllowMultiple = false,
            FileTypeFilter = new[] { ImageFileType },
        }).ConfigureAwait(true);

        return files.Count > 0 ? files[0].TryGetLocalPath() : null;
    }

    private static IStorageProvider? GetStorageProvider()
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .MainWindow?.StorageProvider;
}
