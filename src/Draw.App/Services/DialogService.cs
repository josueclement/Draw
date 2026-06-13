using System.Threading.Tasks;
using Carbon.Avalonia.Desktop.Controls.ContentDialog;
using IContentDialogService = Carbon.Avalonia.Desktop.Services.IContentDialogService;

namespace Draw.App.Services;

public sealed class DialogService : IDialogService
{
    private readonly IContentDialogService _contentDialog;

    public DialogService(IContentDialogService contentDialog)
    {
        _contentDialog = contentDialog;
    }

    public Task ShowErrorAsync(string title, string message) => ShowAsync(title, message, isConfirm: false);

    public Task<bool> ConfirmAsync(string title, string message) => ShowAsync(title, message, isConfirm: true);

    public async Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string documentName)
    {
        DialogResult result = await _contentDialog.ShowAsync(dialog =>
        {
            dialog.Title = "Unsaved changes";
            dialog.Content = $"Save changes to {documentName} before closing?";
            dialog.PrimaryButtonText = "Save";
            dialog.SecondaryButtonText = "Don't Save";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = DefaultButton.Primary;
        });

        return result switch
        {
            DialogResult.Primary => UnsavedChangesChoice.Save,
            DialogResult.Secondary => UnsavedChangesChoice.Discard,
            _ => UnsavedChangesChoice.Cancel,
        };
    }

    private async Task<bool> ShowAsync(string title, string message, bool isConfirm)
    {
        DialogResult result = await _contentDialog.ShowAsync(dialog =>
        {
            dialog.Title = title;
            dialog.Content = message;
            dialog.PrimaryButtonText = isConfirm ? "Yes" : "OK";
            dialog.DefaultButton = DefaultButton.Primary;
            if (isConfirm)
            {
                dialog.CloseButtonText = "No";
            }
        });

        return result == DialogResult.Primary;
    }
}
