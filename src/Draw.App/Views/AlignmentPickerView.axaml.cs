using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Draw.App.ViewModels;

namespace Draw.App.Views;

/// <summary>
/// The Shift+A align &amp; distribute palette overlay. Mouse clicks are wired here (not via XAML command
/// bindings) so each tile resolves its action through the view-model by mnemonic, matching
/// <see cref="StylePickerView"/>. Letter and Esc keys never reach this view — the window intercepts them
/// and calls the view-model directly (see <c>MainWindow.OnGlobalKeyDown</c>).
/// </summary>
public partial class AlignmentPickerView : UserControl
{
    public AlignmentPickerView()
    {
        InitializeComponent();
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is AlignmentPickerViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }

    // A press on the card itself must not bubble to the backdrop (which would dismiss the palette).
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnTileClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AlignmentPickerViewModel vm && sender is Button { DataContext: ArrangeTile tile })
        {
            vm.Activate(tile.Letter);
        }
    }
}
