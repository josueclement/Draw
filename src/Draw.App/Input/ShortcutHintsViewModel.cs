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
/// Status-bar surface that lists the shortcuts relevant to the current state — placement tool armed,
/// a connector selected, node(s) selected, or idle — so the otherwise-undiscoverable mouse gestures
/// (Ctrl+Click to add a connector point, Alt+Click to remove one, wheel zoom/pan) are taught in
/// context. Keyboard shortcuts that live in the keymap are looked up live (so the label tracks any
/// user rebind and disappears when unbound); mouse and arrow gestures, which are hard-coded in the
/// view, use fixed labels.
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
    /// The decision table mapping current state to the shortcuts worth surfacing. First match wins;
    /// each row is capped to keep the single-line status bar from overflowing.
    /// </summary>
    private static IReadOnlyList<HintSpec> SelectSpecs(DiagramDocumentViewModel? document, ToolboxViewModel toolbox)
    {
        if (document is null)
        {
            return [];
        }

        if (!toolbox.IsSelectTool)
        {
            // A placement tool is armed; ActiveToolHint already explains the click/drag to place it.
            return [HintSpec.Keymap("tool.select", "Cancel")];
        }

        if (document.HasConnectorSelection && !document.HasNodeSelection)
        {
            return
            [
                HintSpec.Mouse("Ctrl+Click", "Add point"),
                HintSpec.Mouse("Alt+Click", "Remove point"),
                HintSpec.Keymap("edit.delete", "Delete"),
            ];
        }

        if (document.HasNodeSelection)
        {
            return
            [
                HintSpec.Mouse("↑↓←→", "Nudge"),
                HintSpec.Mouse("Shift+↑↓←→", "Fine nudge"),
                HintSpec.Keymap("edit.delete", "Delete"),
            ];
        }

        // Idle: a document is open, the select tool is active, nothing is selected.
        return
        [
            HintSpec.Keymap("menu.shapes", "Shape"),
            HintSpec.Keymap("menu.connectors", "Connector"),
            HintSpec.Mouse("Ctrl+Wheel", "Zoom"),
            HintSpec.Mouse("Wheel", "Pan"),
        ];
    }

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

        public static HintSpec Mouse(string literal, string label) => new(null, literal, label);
    }
}
