using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Input;

namespace Draw.App.ViewModels;

/// <summary>One row in the help overlay: a gesture (e.g. <c>Ctrl+A</c>) and the action it performs.</summary>
public sealed record ShortcutHelpRow(string Gesture, string Label);

/// <summary>A titled group of related shortcut rows in the help overlay.</summary>
public sealed record ShortcutHelpGroup(string Title, IReadOnlyList<ShortcutHelpRow> Rows);

/// <summary>
/// Drives the Shift+H help overlay: a read-only, centered card (same chrome as the tool palette) listing
/// the main shortcuts grouped by area. Keyboard rows resolve their gesture text live from the keymap
/// (so a user rebind is reflected, and an unbound action is dropped); mouse/arrow rows — which are
/// hard-coded canvas gestures, not keymap actions — use fixed literals. Esc closes; letters do nothing.
/// </summary>
public sealed class ShortcutHelpViewModel : ViewModelBase, IOverlayPalette
{
    private readonly IKeymapService _keymap;

    public ShortcutHelpViewModel(IKeymapService keymap)
    {
        _keymap = keymap;
        DismissCommand = new RelayCommand(Close);
    }

    /// <summary>Click on the dim backdrop (closes the overlay).</summary>
    public RelayCommand DismissCommand { get; }

    /// <summary>Product + version (e.g. <c>Draw 1.0.0</c>), read once from the assembly so there is
    /// no duplicated literal. Immutable, so it needs no change notification.</summary>
    public string VersionLabel { get; } =
        typeof(ShortcutHelpViewModel).Assembly.GetName().Version is { } version
            ? $"Draw {version.ToString(3)}"
            : "Draw";

    /// <summary>The shortcut groups, rebuilt each time the overlay opens.</summary>
    public IReadOnlyList<ShortcutHelpGroup> Groups
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    public bool IsOpen
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Opens the overlay, rebuilding the list from the live keymap.</summary>
    public void Open()
    {
        Groups = BuildGroups();
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public bool Back()
    {
        if (!IsOpen)
        {
            return false;
        }

        Close();
        return true;
    }

    // The help page has nothing to pick; swallow letters so they don't start a chord behind the overlay.
    public bool HandleLetter(char letter) => false;

    private IReadOnlyList<ShortcutHelpGroup> BuildGroups()
    {
        ShortcutHelpGroup[] groups =
        [
            Group(
                "Palettes",
                Key("menu.shapes", "Shapes"),
                Key("menu.connectors", "Connectors"),
                Key("menu.icons", "Icons"),
                Key("menu.styles", "Styles"),
                Key("menu.help", "Help")),
            Group(
                "Edit",
                Key("edit.undo", "Undo"),
                Key("edit.redo", "Redo"),
                Key("edit.cut", "Cut"),
                Key("edit.copy", "Copy"),
                Key("edit.paste", "Paste"),
                Key("edit.duplicate", "Duplicate"),
                Key("edit.duplicateWithConnectors", "Duplicate with connectors"),
                Key("edit.selectAll", "Select all"),
                Key("edit.delete", "Delete")),
            Group(
                "View",
                Key("view.zoomIn", "Zoom in"),
                Key("view.zoomOut", "Zoom out"),
                Key("view.zoomReset", "Reset zoom"),
                Key("view.fitToContent", "Fit to content"),
                Key("view.toggleTheme", "Toggle theme"),
                Key("view.toggleInspector", "Toggle inspector"),
                Key("view.toggleGrid", "Toggle grid")),
            Group(
                "Arrange",
                Key("align.left", "Align left"),
                Key("align.centerHorizontal", "Align center"),
                Key("align.right", "Align right"),
                Key("distribute.horizontal", "Distribute horizontally"),
                Key("zorder.bringToFront", "Bring to front"),
                Key("zorder.sendToBack", "Send to back")),
            Group(
                "File",
                Key("file.new", "New"),
                Key("file.open", "Open"),
                Key("file.save", "Save"),
                Key("file.saveAs", "Save as"),
                Key("file.close", "Close")),
            Group(
                "Canvas",
                Lit("Ctrl+Click", "Add connector point"),
                Lit("Alt+Click", "Remove connector point"),
                Lit("↑ ↓ ← →", "Nudge"),
                Lit("Shift+↑↓←→", "Fine nudge"),
                Lit("Wheel", "Zoom / pan")),
        ];

        return groups.Where(g => g.Rows.Count > 0).ToList();
    }

    /// <summary>A keymap-backed row, or null when the action is unbound (so the row is dropped).</summary>
    private ShortcutHelpRow? Key(string actionId, string label)
    {
        string? gesture = GestureFor(actionId);
        return string.IsNullOrEmpty(gesture) ? null : new ShortcutHelpRow(gesture, label);
    }

    /// <summary>A fixed-gesture row for a hard-coded mouse/arrow gesture (never dropped).</summary>
    private static ShortcutHelpRow Lit(string gesture, string label) => new(gesture, label);

    private static ShortcutHelpGroup Group(string title, params ShortcutHelpRow?[] rows)
        => new(title, rows.Where(r => r is not null).Select(r => r!).ToList());

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
}
