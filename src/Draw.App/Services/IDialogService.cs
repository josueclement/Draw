using System.Threading.Tasks;

namespace Draw.App.Services;

/// <summary>Simple modal message/confirmation dialogs (Avalonia has no built-in message box).</summary>
public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>
    /// Warns that <paramref name="documentName"/> has unsaved changes, offering Save / Don't Save / Cancel.
    /// </summary>
    Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string documentName);
}
