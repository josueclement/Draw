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

        { "keys": "a s r", "action": "tool.shape.rectangle" },
        { "keys": "a s o", "action": "tool.shape.roundedRectangle" },
        { "keys": "a s e", "action": "tool.shape.ellipse" },
        { "keys": "a s c", "action": "tool.shape.circle" },
        { "keys": "a s d", "action": "tool.shape.diamond" },
        { "keys": "a s p", "action": "tool.shape.parallelogram" },
        { "keys": "a s z", "action": "tool.shape.trapezoid" },
        { "keys": "a s t", "action": "tool.shape.triangle" },
        { "keys": "a s n", "action": "tool.shape.note" },

        { "keys": "a c a", "action": "tool.connector.association" },
        { "keys": "a c d", "action": "tool.connector.directedAssociation" },
        { "keys": "a c g", "action": "tool.connector.aggregation" },
        { "keys": "a c o", "action": "tool.connector.composition" },
        { "keys": "a c n", "action": "tool.connector.generalization" },
        { "keys": "a c r", "action": "tool.connector.realization" },
        { "keys": "a c p", "action": "tool.connector.dependency" },
        { "keys": "a c i", "action": "tool.connector.include" },
        { "keys": "a c e", "action": "tool.connector.extend" },
        { "keys": "a c l", "action": "tool.connector.relationship" },

        { "keys": "a u c", "action": "tool.classNode.class" },
        { "keys": "a u i", "action": "tool.classNode.interface" },
        { "keys": "a u e", "action": "tool.classNode.enum" },

        { "keys": "a k a", "action": "tool.useCase.actor" },
        { "keys": "a k u", "action": "tool.useCase.useCase" },
        { "keys": "a k b", "action": "tool.useCase.systemBoundary" },

        { "keys": "a t", "action": "tool.entity" },

        { "keys": "z i", "action": "view.zoomIn" },
        { "keys": "z o", "action": "view.zoomOut" },
        { "keys": "z r", "action": "view.zoomReset" },
        { "keys": "g f", "action": "view.fitToContent" },
        { "keys": "t t", "action": "view.toggleTheme" },
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
    //   * a multi-key chord: space-separated keystrokes, e.g. "a s r" (press a, then s, then r).
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
    //                   realization|dependency|include|extend|relationship}
    //   tool.classNode.{class|interface|enum}
    //   tool.useCase.{actor|useCase|systemBoundary}
    //   tool.entity
    //   file.{new|newEr|open|save|saveAs|close}
    //   edit.{undo|redo|copy|cut|paste|duplicate|delete|insertImage|spaceConnections|mergeConnections}
    //   align.{left|centerHorizontal|right|top|centerVertical|bottom}
    //   distribute.{horizontal|vertical}
    //   zorder.{bringToFront|bringForward|sendBackward|sendToBack}
    //   view.{zoomIn|zoomOut|zoomReset|fitToContent|toggleTheme}
    //   export.{image|svg|copyImage}
    //
    // Examples:
    {
      "bindings": [
        // Remap "add rectangle" to a shorter chord:
        { "keys": "r r", "action": "tool.shape.rectangle" },

        // Bind a single key to fit-to-content:
        { "keys": "F", "action": "view.fitToContent" },

        // Disable the default Ctrl+D duplicate shortcut:
        { "keys": "Ctrl+D", "action": "" }
      ]
    }
    """;
}
