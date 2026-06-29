using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Draw.App.ViewModels;

namespace Draw.App.Views;

/// <summary>
/// The Shift+Y style picker overlay. Mouse clicks are wired here (not via XAML command bindings) to keep
/// each tile's command resolution explicit, matching <see cref="ToolPaletteView"/>. Letter and Esc keys
/// never reach this view — the window intercepts them and calls the view-model directly (see
/// <c>MainWindow.OnGlobalKeyDown</c>).
/// </summary>
public partial class StylePickerView : UserControl
{
    public StylePickerView()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is StylePickerViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }

    // A press on the card itself must not bubble to the backdrop (which would dismiss the picker).
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnSwatchClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StylePickerViewModel vm && sender is Button { DataContext: StyleSwatchEntry entry })
        {
            vm.PickSwatchCommand.Execute(entry);
        }
    }

    private void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StylePickerViewModel vm)
        {
            vm.ResetCommand.Execute(null);
        }
    }

    private void OnNoFillClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is StylePickerViewModel vm)
        {
            vm.NoFillCommand.Execute(null);
        }
    }
}
