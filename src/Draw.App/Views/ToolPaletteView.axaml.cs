using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Draw.App.ViewModels;

namespace Draw.App.Views;

/// <summary>
/// The keyboard tool palette overlay (Shift+S / Shift+C). Mouse clicks are wired here rather than via
/// XAML command bindings: it keeps each tile's command resolution explicit (compiled-binding failures
/// are silent in this app) and matches how the rest of the window wires its controls. Letter and Esc
/// keys never reach this view — the window intercepts them and calls the view-model directly (see
/// <c>MainWindow.OnGlobalKeyDown</c>).
/// </summary>
public partial class ToolPaletteView : UserControl
{
    public ToolPaletteView()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ToolPaletteViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }

    // A press on the card itself must not bubble to the backdrop (which would dismiss the palette).
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ToolPaletteViewModel vm && sender is Button { DataContext: PaletteCategoryEntry entry })
        {
            vm.SelectCategoryCommand.Execute(entry);
        }
    }

    private void OnItemClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ToolPaletteViewModel vm && sender is Button { DataContext: PaletteItemEntry entry })
        {
            vm.SelectItemCommand.Execute(entry);
        }
    }
}
