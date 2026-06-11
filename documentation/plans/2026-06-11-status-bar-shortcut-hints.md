# Status-bar shortcut hints — Implementation plan

Branch: `feature/status-bar-shortcut-hints`.

The app has many keyboard shortcuts (data-driven via `keymap.json`) and several mouse+modifier gestures
(`Ctrl+Click` to add a connector waypoint, `Alt+Click` to remove one, `Shift` to multi-select, wheel /
`Ctrl+wheel` to pan/zoom). The keyboard commands are discoverable from ribbon tooltips, but the **mouse
gestures are surfaced nowhere** — there is no menu, tooltip, or hint for them. This change adds a short,
**context-sensitive** list of the relevant shortcuts to the bottom status bar (after the existing items,
behind a separator) so those hidden gestures are taught in context.

## Decisions

- **Context-sensitive** — the hint set changes with the current state (idle / placement tool armed /
  connector selected / node(s) selected), keeping the single non-wrapping status-bar line short.
- **Scope = hard-to-discover gestures + mode keys only** — mouse+modifier interactions, arrow-nudge, and
  the mode keys (`Shift+S`, `Shift+C`, `Esc`). The common keyboard commands (`Ctrl+Z/C/V/S`, align,
  z-order) are deliberately excluded — they already appear as ribbon tooltips.
- **Live gestures from the keymap** — keyboard shortcuts that are user-rebindable are looked up at render
  time via `IKeymapService` + `KeyGestureParser.Describe`, so the displayed label tracks any rebind and
  the hint disappears if its action is unbound. Mouse/arrow gestures (not in the keymap) use fixed labels.
- **Format** — a vertical separator (`CarbonBorderBrush`) divides the section from the existing items;
  each entry shows the gesture in SemiBold and the action lighter (Opacity 0.8), entries joined by a
  middot `·`.

## Context → hints mapping

First match wins; each row is capped to fit one line. `(live)` = looked up from the keymap by action id.

- **No active document** → no hints (the whole section is hidden via `HasHints`).
- **A placement tool is armed** (`!Toolbox.IsSelectTool`): `Esc` Cancel `(live: tool.select)`.
  (`ActiveToolHint` already shows the "Click/drag to place …" instruction.)
- **Connector selected** (`HasConnectorSelection && !HasNodeSelection`):
  `Ctrl+Click` Add point · `Alt+Click` Remove point · `Del` Delete `(live: edit.delete)`.
- **Node(s) selected** (`HasNodeSelection`, also covers mixed selection):
  `↑↓←→` Nudge · `Shift+↑↓←→` Fine nudge · `Del` Delete `(live: edit.delete)`.
- **Idle** (document open, select tool, nothing selected):
  `Shift+S` Shape `(live: menu.shapes)` · `Shift+C` Connector `(live: menu.connectors)` ·
  `Ctrl+Wheel` Zoom · `Wheel` Pan.

## Approach

A small dedicated VM, `ShortcutHintsViewModel` (mirrors `KeymapStatusViewModel`), holds the current
`IReadOnlyList<ShortcutHint>` and a `HasHints` flag. `Refresh(document, toolbox)` runs the decision table
above (`SelectSpecs`), resolves each entry to a display gesture — keymap action ids via `GestureFor`
(reverse lookup over `IKeymapService.Bindings` + `KeyGestureParser.Describe`), literals as-is — drops any
unbound keymap hint, and stamps `ShowSeparator` (false on the first surviving entry, true after) for the
leading middot.

`ShellViewModel` owns the VM and drives `Refresh` from the signals it already observes: the
`ActiveDocument` setter (document switch), `OnActiveSelectionChanged` (selection change), and a new
subscription to `Toolbox.PropertyChanged` keyed on `ActiveToolHint` (the property `RaiseModes()` already
raises when a tool is armed/disarmed). The initial state is covered because the constructor's `OnNew()`
sets `ActiveDocument`, which triggers a refresh.

The status bar renders the list with an `ItemsControl` (horizontal `StackPanel` panel); the item template
draws an optional leading `·`, the bold gesture, and the lighter label.

## Files

- **New**: `src/Draw.App/Input/ShortcutHintsViewModel.cs` (`ShortcutHint` record + VM).
- `src/Draw.App/Hosting/ServiceCollectionExtensions.cs` — register the singleton.
- `src/Draw.App/ViewModels/ShellViewModel.cs` — inject, expose `ShortcutHints`, refresh hooks.
- `src/Draw.App/Views/MainWindow.axaml` — `xmlns:input` + the hints markup in the status bar.

## Verification

- `dotnet build Draw.slnx` — passes (0 warnings; nullable warnings are build errors). ✅
- **GUI is not renderable headless under WSL2** — manual check on Windows (`dotnet run --project
  src/Draw.App`):
  - Idle: `… │ Shift+S Shape · Shift+C Connector · Ctrl+Wheel Zoom · Wheel Pan`.
  - Arm a shape: switches to `… │ Esc Cancel` (alongside the existing "Click to place" text).
  - Select a connector: `… │ Ctrl+Click Add point · Alt+Click Remove point · Del Delete`; confirm
    Ctrl+Click adds a point and Alt+Click removes one.
  - Select node(s): `… │ ↑↓←→ Nudge · Shift+↑↓←→ Fine nudge · Del Delete`.
  - Rebind/unbind `menu.shapes` in `keymap.json` and confirm the idle hint updates / disappears
    (proves live lookup).

## Notes

- No automated test: there is no `Draw.App` test project (by convention only the `Model`/`Diagramming`
  pure-logic layers are tested; the GUI can't run headless). The decision table is isolated in
  `SelectSpecs` so it could be unit-tested if a `Draw.App.Tests` project is later added.
