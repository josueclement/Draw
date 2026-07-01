namespace Draw.App.Input;

/// <summary>
/// The baked-in default keymap (migrated single gestures + a curated chord scheme) plus the
/// heavily-commented example written to the user data folder on first run. Kept as string literals so
/// they are build-checked and live next to the parser/registry; the repo ships no resource data files.
/// </summary>
public static class DefaultKeymap
{
    /// <summary>The default bindings loaded at startup unless overridden by the user's keymap.json.</summary>
    public const string DefaultJson = """
    {
      "bindings": [
        { "keys": "Ctrl+N", "action": "file.new" },
        { "keys": "Ctrl+O", "action": "file.open" },
        { "keys": "Ctrl+S", "action": "file.save" },
        { "keys": "Ctrl+Shift+S", "action": "file.saveAs" },
        { "keys": "Ctrl+W", "action": "file.close" },

        { "keys": "Ctrl+Z", "action": "edit.undo" },
        { "keys": "Ctrl+Y", "action": "edit.redo" },
        { "keys": "Ctrl+C", "action": "edit.copy" },
        { "keys": "Ctrl+X", "action": "edit.cut" },
        { "keys": "Ctrl+V", "action": "edit.paste" },
        { "keys": "Ctrl+D", "action": "edit.duplicate" },
        { "keys": "Ctrl+Shift+D", "action": "edit.duplicateWithConnectors" },
        { "keys": "Ctrl+A", "action": "edit.selectAll" },
        { "keys": "Delete", "action": "edit.delete" },

        { "keys": "Ctrl+Shift+F", "action": "view.fitToContent" },
        { "keys": "Ctrl+Shift+L", "action": "align.left" },
        { "keys": "Ctrl+Shift+C", "action": "align.centerHorizontal" },
        { "keys": "Ctrl+Shift+R", "action": "align.right" },
        { "keys": "Ctrl+Shift+T", "action": "align.top" },
        { "keys": "Ctrl+Shift+M", "action": "align.centerVertical" },
        { "keys": "Ctrl+Shift+B", "action": "align.bottom" },
        { "keys": "Ctrl+Shift+H", "action": "distribute.horizontal" },
        { "keys": "Ctrl+Shift+V", "action": "distribute.vertical" },
        { "keys": "Ctrl+OemCloseBrackets", "action": "zorder.bringForward" },
        { "keys": "Ctrl+OemOpenBrackets", "action": "zorder.sendBackward" },
        { "keys": "Ctrl+Shift+OemCloseBrackets", "action": "zorder.bringToFront" },
        { "keys": "Ctrl+Shift+OemOpenBrackets", "action": "zorder.sendToBack" },

        { "keys": "Escape", "action": "tool.select" },

        { "keys": "Shift+S", "action": "menu.shapes" },
        { "keys": "Shift+C", "action": "menu.connectors" },
        { "keys": "Shift+I", "action": "menu.icons" },
        { "keys": "Shift+T", "action": "menu.styles" },
        { "keys": "Shift+A", "action": "menu.align" },
        { "keys": "Shift+H", "action": "menu.help" },

        { "keys": "a r", "action": "tool.shape.rectangle" },
        { "keys": "a a", "action": "tool.connector.association" },
        { "keys": "a c", "action": "tool.classNode.class" },
        { "keys": "a i", "action": "tool.classNode.interface" },
        { "keys": "a t", "action": "tool.entity" },
        { "keys": "a b", "action": "tool.connector.mindMapBranch" },

        { "keys": "z i", "action": "view.zoomIn" },
        { "keys": "z o", "action": "view.zoomOut" },
        { "keys": "z r", "action": "view.zoomReset" },
        { "keys": "g f", "action": "view.fitToContent" },
        { "keys": "t t", "action": "view.toggleTheme" },
        { "keys": "t p", "action": "view.toggleInspector" },
        { "keys": "t g", "action": "view.toggleGrid" },
        { "keys": "x i", "action": "export.image" },
        { "keys": "x s", "action": "export.svg" },
        { "keys": "x c", "action": "export.copyImage" }
      ]
    }
    """;

    /// <summary>
    /// The commented example written to the user data folder on first run. JSONC (// comments and
    /// trailing commas) is tolerated by the loader. The file actually loaded is keymap.json, not this one.
    /// </summary>
    public const string ExampleJsonc = """
    // Draw keymap — copy this file to "keymap.json" (same folder) and edit it to customise shortcuts.
    //
    // Each entry binds a key sequence to an action:
    //   { "keys": "<sequence>", "action": "<action id>" }
    //
    // "keys" is either
    //   * a single gesture with optional modifiers, e.g. "Ctrl+Shift+S", "Delete", "F2"; or
    //   * a multi-key chord: space-separated keystrokes, e.g. "d r" (press d, then r).
    // Modifiers: Ctrl/Control, Shift, Alt, Meta (Cmd/Win). Bare letters/digits are case-insensitive and
    // ignore Shift. Key names follow Avalonia's Key enum (A..Z, D0..D9, Oem*, Delete, Escape, F1..F12, ...).
    //
    // "action" arms a tool for "add.*"/"tool.*" actions (then click/drag on the canvas to place), or runs
    // the command immediately for everything else. Set "action" to "" (empty) to UNBIND a default key.
    // Your entries are merged over the built-in defaults, keyed by the key sequence (last one wins).
    //
    // Available action ids:
    //   tool.select
    //   tool.shape.{rectangle|roundedRectangle|ellipse|circle|diamond|parallelogram|trapezoid|triangle|note}
    //   tool.connector.{association|directedAssociation|aggregation|composition|generalization|
    //                   realization|dependency|include|extend|relationship|mindMapBranch}
    //   tool.classNode.{class|interface|enum}
    //   tool.useCase.{actor|useCase|systemBoundary}
    //   tool.entity
    //   menu.shapes, menu.connectors   (open a category-grouped tool menu; default Shift+S / Shift+C)
    //   menu.icons                     (toggle status-marker icons on the selection; default Shift+I)
    //   menu.styles                    (apply a style to the selection; default Shift+T)
    //   menu.align                     (align & distribute the selection; default Shift+A)
    //   menu.help                      (show the keyboard-shortcut help overlay; default Shift+H)
    //   file.{new|open|save|saveAs|close}
    //   edit.{undo|redo|copy|cut|paste|duplicate|delete|selectAll|insertImage|spaceConnections|mergeConnections}
    //   align.{left|centerHorizontal|right|top|centerVertical|bottom}
    //   distribute.{horizontal|vertical}
    //   zorder.{bringToFront|bringForward|sendBackward|sendToBack}
    //   view.{zoomIn|zoomOut|zoomReset|fitToContent|toggleTheme|toggleInspector|toggleGrid}
    //   export.{image|svg|copyImage}
    //
    // Examples:
    {
      "bindings": [
        // Open the shapes menu with a different key (default is Shift+S):
        { "keys": "Insert", "action": "menu.shapes" },

        // Bind a direct chord to add a rectangle (the per-tool actions are still available):
        { "keys": "r r", "action": "tool.shape.rectangle" },

        // Bind a single key to fit-to-content:
        { "keys": "F", "action": "view.fitToContent" },

        // Disable the default Ctrl+D duplicate shortcut:
        { "keys": "Ctrl+D", "action": "" }
      ]
    }
    """;
}
