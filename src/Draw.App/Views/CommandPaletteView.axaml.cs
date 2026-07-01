using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Draw.App.ViewModels;

namespace Draw.App.Views;

/// <summary>
/// The vim <c>:</c> command palette overlay. Unlike the letter-driven palettes this one hosts a focused
/// input, so Enter/Esc are handled here (not via the window's overlay key routing) and mouse clicks are
/// wired here rather than through XAML command bindings, matching <see cref="StylePickerView"/>.
/// </summary>
public partial class CommandPaletteView : UserControl
{
    public CommandPaletteView()
    {
        InitializeComponent();
    }

    /// <summary>Focuses the input and puts the caret at the end. Called by the window when the palette opens.</summary>
    public void FocusInput()
    {
        CommandInput.Focus();
        CommandInput.CaretIndex = CommandInput.Text?.Length ?? 0;
    }

    private void OnBackdropPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }

    // A press on the card itself must not bubble to the backdrop (which would dismiss the palette).
    private void OnPanelPressed(object? sender, PointerPressedEventArgs e) => e.Handled = true;

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not CommandPaletteViewModel vm)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            vm.Run(vm.Text);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.Close();
            e.Handled = true;
        }
    }

    private void OnRowClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is CommandPaletteViewModel vm && sender is Button { DataContext: CommandPaletteEntry entry })
        {
            vm.RunCommand.Execute(entry);
        }
    }
}
