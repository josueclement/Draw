using Avalonia.Controls;
using Avalonia.Input;
using Draw.App.ViewModels;

namespace Draw.App.Views;

/// <summary>
/// The Shift+H keyboard-shortcut help overlay. Read-only — only the backdrop press (dismiss) is wired
/// here; Esc closes via the window's key dispatcher (see <c>MainWindow.OnGlobalKeyDown</c>).
/// </summary>
public partial class ShortcutHelpView : UserControl
{
    public ShortcutHelpView()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ShortcutHelpViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }

    // A press on the card itself must not bubble to the backdrop (which would close the overlay).
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;
}
