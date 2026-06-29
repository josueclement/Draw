# Toggle grid visibility (per-document, persisted): Implementation plan

Branch: `feat-toggle-grid`. No separate design spec — small, self-contained.

## Problem

The canvas always draws a faint tiled grid behind the nodes (the `GridBackground` rectangle whose
`Fill` is built in `DiagramView.UpdateGrid()`); there was no way to turn it off.

## Decision

Let the user toggle the grid from three surfaces — a **ribbon toggle** (View ▸ Appearance), an
**Appearance context-menu** checkbox, and a **`t g`** keyboard chord — and document the chord in the
Shift+H help overlay. Unlike the sibling "Snap to grid" (an app-wide, session-only `EditorOptions`
flag), grid visibility is:

- **Per-document** — each open diagram has its own state.
- **Serialized into the `.draw` file** — reopening a document restores whether the grid was shown.
- Default **shown** (`true`) for new and pre-existing documents.

The source of truth is therefore `DiagramDocument.ShowGrid` in `Draw.Model`. The ribbon/menu bind
through a thin `ShellViewModel` proxy onto the active document, and the canvas repaints by reacting to
the document VM's `ShowGrid` property change — the same mechanism it already uses for Zoom/Pan.

## Files touched

1. **`src/Draw.Model/Documents/DiagramDocument.cs`** — add `public bool ShowGrid { get; set; } = true;`.
   Reflection-based `System.Text.Json` serializes it as `showGrid`. **No schema bump**: it is additive
   with a sensible default, so a pre-feature file omits the key and the initializer (`true`) stands —
   it opens with the grid shown. Forward-compatible too (an older build ignores the unknown key;
   `SchemaVersion` stays `2`).

2. **`tests/Draw.Model.Tests/JsonDocumentSerializerTests.cs`** — two cases: a hidden-grid document
   round-trips to `false`; a document deserialized from `"{}"` (no `showGrid`) defaults to `true`.

3. **`src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`** — `ShowGrid` get/set over `_document`
   (raises `OnPropertyChanged` + `MarkModified` so Ctrl+S persists it; **not** undo-captured — a
   display preference), plus a `ToggleGridCommand` (`() => ShowGrid = !ShowGrid`).

4. **`src/Draw.App/Views/DiagramView.axaml.cs`** — react to `ShowGrid` in `OnVmPropertyChanged` by
   calling `UpdateGrid()`; gate `UpdateGrid()` on `!_vm.ShowGrid` (clears `GridBackground.Fill`); add
   the "Show grid" checkbox to `BuildAppearanceMenu`.

5. **`src/Draw.App/ViewModels/ShellViewModel.cs`** — bindable `ShowGrid` proxy onto `ActiveDocument`;
   raise it when the active document changes and forward the active document's own `ShowGrid` change,
   so the ribbon/menu check state stays in sync on tab switch and on `t g`.

6. **`src/Draw.App/Views/MainWindow.axaml`** — `RibbonToggleButton` ("Show grid", `Icon=grid_nine`) in
   View ▸ Appearance, `IsChecked="{Binding ShowGrid, Mode=TwoWay}"`, `IsEnabled` on `HasActiveDocument`
   (per-document, unlike app-wide Snap).

7. **`src/Draw.App/Input/DefaultKeymap.cs`** — `{ "keys": "t g", "action": "view.toggleGrid" }` in the
   `t` family; action id added to the example comment.

8. **`src/Draw.App/Input/KeymapActionRegistry.cs`** — `AddDoc("view.toggleGrid", d => (d.ToggleGridCommand, null))`
   (per-document, like the zoom actions).

9. **`src/Draw.App/ViewModels/ShortcutHelpViewModel.cs`** — `Key("view.toggleGrid", "Toggle grid")` in
   the View group; gesture string auto-resolved from the live keymap.

## Notes / scope

- The grid's appearance (cell size, colour) is unchanged — this toggles visibility only.
- Because grid state now lives in the memento-captured document, undoing a *later* gesture can revert
  an intervening grid toggle. Inherent to whole-document snapshots; accepted.
- Verified: `dotnet build Draw.slnx` clean (confirms the `grid_nine` icon member resolves) and
  `dotnet test --solution Draw.slnx` green (379 tests). GUI behaviour pending visual verification on
  Windows/macOS (WSL2 can't render).
