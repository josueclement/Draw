using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Carbon.Avalonia.Desktop.Controls.ContentDialog;
using Draw.App.ViewModels;
using Draw.App.Views;
using Draw.Model.Nodes;
using IContentDialogService = Carbon.Avalonia.Desktop.Services.IContentDialogService;

namespace Draw.App.Services;

/// <summary>What the user chose when warned about a document with unsaved changes.</summary>
public enum UnsavedChangesChoice
{
    Save,
    Discard,
    Cancel,
}

/// <summary>Simple modal message/confirmation dialogs (Avalonia has no built-in message box).</summary>
public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);

    /// <summary>
    /// Warns that <paramref name="documentName"/> has unsaved changes, offering Save / Don't Save / Cancel.
    /// </summary>
    Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string documentName);

    /// <summary>
    /// Opens the modal members editor seeded with <paramref name="current"/>. Returns the edited
    /// members on Save (to be applied by the caller as one undo step) or <c>null</c> on Cancel.
    /// </summary>
    Task<IReadOnlyList<ClassMember>?> EditClassMembersAsync(ClassNodeKind kind, IReadOnlyList<ClassMember> current, IReadOnlyList<string> typeSuggestions);

    /// <summary>
    /// Opens the modal columns editor seeded with <paramref name="current"/>. Returns the edited
    /// columns on Save (to be applied by the caller as one undo step) or <c>null</c> on Cancel.
    /// </summary>
    Task<IReadOnlyList<EntityColumn>?> EditEntityColumnsAsync(IReadOnlyList<EntityColumn> current, IReadOnlyList<string> typeSuggestions);
}

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

    public async Task<IReadOnlyList<ClassMember>?> EditClassMembersAsync(ClassNodeKind kind, IReadOnlyList<ClassMember> current, IReadOnlyList<string> typeSuggestions)
    {
        ClassMembersEditorViewModel editor = new(kind, current, typeSuggestions);
        ClassMembersEditorView view = new() { DataContext = editor };

        DialogResult result = await _contentDialog.ShowAsync(dialog =>
        {
            dialog.Title = kind == ClassNodeKind.Enum ? "Edit literals" : "Edit members";
            dialog.Content = view;
            dialog.PrimaryButtonText = "Save";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = DefaultButton.Primary;
        });

        return result == DialogResult.Primary ? editor.BuildResult() : null;
    }

    public async Task<IReadOnlyList<EntityColumn>?> EditEntityColumnsAsync(IReadOnlyList<EntityColumn> current, IReadOnlyList<string> typeSuggestions)
    {
        EntityColumnsEditorViewModel editor = new(current, typeSuggestions);
        EntityColumnsEditorView view = new() { DataContext = editor };

        DialogResult result = await _contentDialog.ShowAsync(dialog =>
        {
            dialog.Title = "Edit columns";
            dialog.Content = view;
            dialog.PrimaryButtonText = "Save";
            dialog.CloseButtonText = "Cancel";
            dialog.DefaultButton = DefaultButton.Primary;
        });

        return result == DialogResult.Primary ? editor.BuildResult() : null;
    }

    private static async Task<bool> ShowAsync(string title, string message, bool isConfirm)
    {
        Window? owner = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (owner is null)
        {
            return false;
        }

        bool result = false;
        Window dialog = new()
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
        };

        Button confirmButton = new() { Content = isConfirm ? "Yes" : "OK", MinWidth = 84, IsDefault = true };
        confirmButton.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        buttons.Children.Add(confirmButton);

        if (isConfirm)
        {
            Button cancelButton = new() { Content = "No", MinWidth = 84, IsCancel = true };
            cancelButton.Click += (_, _) =>
            {
                result = false;
                dialog.Close();
            };
            buttons.Children.Add(cancelButton);
        }

        StackPanel root = new() { Margin = new Thickness(20), Spacing = 18 };
        root.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        root.Children.Add(buttons);
        dialog.Content = root;

        await dialog.ShowDialog(owner);
        return result;
    }
}
