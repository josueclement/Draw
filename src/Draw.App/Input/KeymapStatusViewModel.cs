using Draw.App.ViewModels;

namespace Draw.App.Input;

/// <summary>
/// Status-bar surface for the keymap dispatcher: the in-progress chord (<see cref="Pending"/>) and a
/// short-lived message (<see cref="TransientMessage"/>, e.g. "no binding"). <see cref="Display"/> shows
/// the pending chord when one is active, otherwise the transient message, otherwise nothing.
/// </summary>
public sealed class KeymapStatusViewModel : ViewModelBase
{
    public string? Pending
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    public string? TransientMessage
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(Display));
            }
        }
    }

    public string Display => !string.IsNullOrEmpty(Pending)
        ? Pending
        : TransientMessage ?? string.Empty;
}
