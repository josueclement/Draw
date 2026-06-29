using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Draw.App.ViewModels;

namespace Draw.App.Views;

/// <summary>
/// The Shift+I icon palette overlay. Mouse clicks are wired here (not via XAML command bindings) to keep
/// each tile's command resolution explicit, matching <see cref="ToolPaletteView"/>. Letter and Esc keys
/// never reach this view — the window intercepts them and calls the view-model directly (see
/// <c>MainWindow.OnGlobalKeyDown</c>).
/// </summary>
public partial class IconPaletteView : UserControl
{
    public IconPaletteView()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is IconPaletteViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }

    // A press on the card itself must not bubble to the backdrop (which would dismiss the palette).
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnEntryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is IconPaletteViewModel vm && sender is Button { DataContext: IconPaletteEntry entry })
        {
            vm.ToggleCommand.Execute(entry);
        }
    }
}
