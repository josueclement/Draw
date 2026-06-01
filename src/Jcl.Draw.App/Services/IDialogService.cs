using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace Jcl.Draw.App.Services;

/// <summary>Simple modal message/confirmation dialogs (Avalonia has no built-in message box).</summary>
public interface IDialogService
{
    Task ShowErrorAsync(string title, string message);

    Task<bool> ConfirmAsync(string title, string message);
}

public sealed class DialogService : IDialogService
{
    public Task ShowErrorAsync(string title, string message) => ShowAsync(title, message, isConfirm: false);

    public Task<bool> ConfirmAsync(string title, string message) => ShowAsync(title, message, isConfirm: true);

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
