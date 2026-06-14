using System.Threading.Tasks;

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
