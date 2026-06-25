# Shortcut additions: `a b` branch tool, `t p` toggle inspector, `Ctrl+A` select all — Implementation plan

Branch: `feat/keyboard-shortcuts-ab-tp-selectall`.

Three small additions to the existing keymap system (`ChordInputDispatcher` + `KeymapService` +
`KeymapActionRegistry` + `DefaultKeymap`). Two reuse commands/actions that already exist; one adds a
new select-all command. No behavior changes to existing tools or to selection on click.

## Decisions

- **`a b` → arm the mind-map branch connector tool** (action `tool.connector.mindMapBranch`).
  Per the request, `a b` does **not** create a child shape — it arms the existing "Mind-map branch"
  connector tool (same as the ribbon *Branch* button / `Shift+S` menu), then the user drags between
  two mind-map shapes. The action is already auto-registered by the `RelationshipKind` loop in
  `KeymapActionRegistry.Build()`, so this is a binding-only change. Fits the uniform `a` = "add"
  chord prefix (`a r/a a/a c/a i/a t`).
- **`t p` → toggle the Inspector panel** (new action `view.toggleInspector` → the existing
  `ShellViewModel.ToggleInspectorCommand`). The Inspector/Properties panel on the right is the only
  panel. Fits the `t` = "toggle" prefix alongside `t t` (theme).
- **`Ctrl+A` → select everything** (new action `edit.selectAll`, document-scoped → new
  `DiagramDocumentViewModel.SelectAllCommand`). Selects **all nodes and all connectors** on the
  active diagram via a new `SelectionCoordinator.SelectAll()`. The command is always-executable
  (select-all on an empty diagram is a harmless no-op). Single-gesture binding, not a chord.
  Safe with inline text editing: `ChordInputDispatcher` skips dispatch while a TextBox/AutoCompleteBox/
  ComboBox is focused, so `Ctrl+A` in a label/member editor still does native text-select-all.

## Not done (deliberate)

- **`Shift+click` = `Ctrl+click`** was requested but is unnecessary: `Shift+click` already
  multi-selects shapes (`DiagramView.axaml.cs` → `ToggleSelectUnified`) and is the more capable
  variant (it preserves selected connectors; `Ctrl` clears them). Left unchanged.
- No `Edit ▸ Select All` menu item / ribbon button, and no status-bar hint entries for the new
  chords. Can be added later for discoverability.

## Files touched

- `src/Draw.App/Input/DefaultKeymap.cs` — 3 bindings (`Ctrl+A`, `a b`, `t p`) + `ExampleJsonc`
  action-id comment updates (`mindMapBranch`, `selectAll`, `toggleInspector`).
- `src/Draw.App/Input/KeymapActionRegistry.cs` — register `edit.selectAll` (doc-scoped) and
  `view.toggleInspector`.
- `src/Draw.App/ViewModels/SelectionCoordinator.cs` — `SelectAll()`.
- `src/Draw.App/ViewModels/DiagramDocumentViewModel.cs` — `SelectAll()` + `SelectAllCommand`.

## Verification

- `dotnet build Draw.slnx` — clean (0 warnings / 0 errors; nullable warnings are build errors here).
- No automated tests added: the keymap/registry/selection code lives in the Avalonia-dependent
  `Draw.App` project, which has no headless test coverage.
- Manual (Windows/macOS — WSL2 cannot render the GUI):
  1. `a` then `b` → status bar shows "Drag from one node to another to draw Mind-map branch."; drag
     between two mind-map topics → a tapered branch connector is created.
  2. `t` then `p` → the right Inspector panel toggles open/closed.
  3. With nodes + connectors present, `Ctrl+A` → all nodes and connectors are selected; while editing
     an inline text label, `Ctrl+A` selects the text in the editor, not the canvas.
