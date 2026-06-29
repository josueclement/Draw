using System.Collections.Generic;
using Draw.App.ViewModels;

namespace Draw.App.Input;

/// <summary>
/// One status-bar shortcut hint: a <see cref="Gesture"/> (e.g. <c>Ctrl+Click</c>) and the action it
/// performs (<see cref="Label"/>). <see cref="ShowSeparator"/> is <c>false</c> for the first hint in a
/// row and <c>true</c> thereafter, driving the leading middot that joins entries in the view.
/// </summary>
public sealed record ShortcutHint(string Gesture, string Label, bool ShowSeparator);

/// <summary>
/// Status-bar surface that points the user at the keyboard-shortcut help overlay. It surfaces a single
/// hint — "Shift+H Help" — whose gesture is looked up live from the keymap (so it tracks a user rebind and
/// disappears if unbound). The full, context-sensitive shortcut list now lives in that help overlay
/// (<see cref="Draw.App.ViewModels.ShortcutHelpViewModel"/>), keeping the status bar uncluttered.
/// </summary>
public sealed class ShortcutHintsViewModel : ViewModelBase
{
    private readonly IKeymapService _keymap;

    public ShortcutHintsViewModel(IKeymapService keymap)
    {
        _keymap = keymap;
    }

    /// <summary>The hints for the current state, in display order. Empty when no document is open.</summary>
    public IReadOnlyList<ShortcutHint> Hints
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(HasHints));
            }
        }
    } = [];

    public bool HasHints => Hints.Count > 0;

    /// <summary>Recomputes <see cref="Hints"/> from the active document's selection and the armed tool.</summary>
    public void Refresh(DiagramDocumentViewModel? document, ToolboxViewModel toolbox)
    {
        IReadOnlyList<HintSpec> specs = SelectSpecs(document, toolbox);
        List<ShortcutHint> hints = new(specs.Count);
        foreach (HintSpec spec in specs)
        {
            string? gesture = spec.ActionId is { } actionId ? GestureFor(actionId) : spec.Literal;
            if (string.IsNullOrEmpty(gesture))
            {
                continue; // Keymap action is unbound — drop the hint rather than show a blank gesture.
            }

            hints.Add(new ShortcutHint(gesture, spec.Label, hints.Count > 0));
        }

        Hints = hints;
    }

    /// <summary>
    /// The single hint surfaced while a document is open: a pointer to the keyboard-shortcut help overlay.
    /// (Everything else moved into that overlay.) Empty when no document is open.
    /// </summary>
    private static IReadOnlyList<HintSpec> SelectSpecs(DiagramDocumentViewModel? document, ToolboxViewModel toolbox)
        => document is null ? [] : [HintSpec.Keymap("menu.help", "Help")];

    /// <summary>The display string of the first keymap binding for <paramref name="actionId"/>, or null if unbound.</summary>
    private string? GestureFor(string actionId)
    {
        foreach (ParsedBinding binding in _keymap.Bindings)
        {
            if (binding.Action == actionId)
            {
                return KeyGestureParser.Describe(binding.Strokes);
            }
        }

        return null;
    }

    /// <summary>A hint before resolution: either a keymap <see cref="ActionId"/> to look up, or a fixed <see cref="Literal"/>.</summary>
    private readonly record struct HintSpec(string? ActionId, string? Literal, string Label)
    {
        public static HintSpec Keymap(string actionId, string label) => new(actionId, null, label);
    }
}
