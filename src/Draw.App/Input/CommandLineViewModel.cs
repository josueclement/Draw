using Draw.App.ViewModels;

namespace Draw.App.Input;

/// <summary>
/// Backs the vim <c>:</c> command line shown at the bottom of the window. Pure observable state — the
/// window code-behind drives it (open on <c>:</c>, parse + run on Enter, close on Escape); it owns no
/// command logic.
/// </summary>
public sealed class CommandLineViewModel : ViewModelBase
{
    /// <summary>True while the command box is visible and capturing input.</summary>
    public bool IsActive
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The text being typed, including the leading <c>:</c>.</summary>
    public string Text
    {
        get;
        set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Opens the command line, prefilled with the leading <c>:</c>.</summary>
    public void Begin()
    {
        Text = ":";
        IsActive = true;
    }

    /// <summary>Closes the command line and clears its text.</summary>
    public void End()
    {
        IsActive = false;
        Text = string.Empty;
    }
}
