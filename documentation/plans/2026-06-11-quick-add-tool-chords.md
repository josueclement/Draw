# Quick-add tool chords (vim-like) + status-bar hints — Implementation plan

Branch: `feature/status-bar-shortcut-hints` (continues the status-bar shortcut-hints work, a prerequisite).

Before the `Shift+S` / `Shift+C` category menus existed, the app shipped vim-like multi-key chords
(e.g. `a s r` = arm rectangle). Those *defaults* were replaced by the menus, but the multi-key chord
engine (`ChordInputDispatcher`) and the granular `tool.*` arm-actions remained fully present and
bindable — the `a` key prefix was simply unused. This change brings back fast two-key chords for the
most common items and surfaces them in the status bar (the context-sensitive hints added previously).
No new behavior: just new default keymap bindings plus one idle-hint edit.

## Decisions

- **Arm the tool** (then click/drag to place), reusing the existing `tool.*` actions — required for
  connectors (two endpoints) and matching the old `asr` style. No new placement code.
- **Chord set** (uniform `a` = "add" prefix), all mapping to existing action ids:

  | Chord | Action id | Arms |
  |-------|-----------|------|
  | `a r` | `tool.shape.rectangle`       | Rectangle |
  | `a a` | `tool.connector.association` | Connector (Association — "add association") |
  | `a c` | `tool.classNode.class`       | Class |
  | `a i` | `tool.classNode.interface`   | Interface |
  | `a t` | `tool.entity`                | Table (ER) |

- **Status bar (idle context only)** shows the shape/connector menus (`Shift+S` Shapes, `Shift+C`
  Connectors) first, then the 5 quick-add chords (the connector chord is labelled "Association" to
  distinguish it from the `Shift+C` "Connectors" menu). This replaces the previous wheel zoom·pan idle
  hints. Gestures render via the existing `KeyGestureParser.Describe`, which lowercases letters — so they
  read `a r`, `a a`, … matching the live pending-chord feedback. Wheel zoom/pan still works; it's just
  not shown in the idle bar. (Note: 7 entries plus the left-hand items make a wide line that can clip on
  a narrow window — accepted by the user.)

## Why it's safe

- Bare-letter chords already work (`z i`, `g f`, `t t`, `x s`); `a` adds no new mechanism.
- No conflict: no default binding starts with bare `a`; the `align.*` chords are `Ctrl+Shift+…`
  (modified), distinct from unmodified `a …`. The dispatcher is suppressed while a TextBox/AutoComplete/
  ComboBox is focused, so typing `a` while editing a label/member/column won't arm a tool.
- `KeymapActionRegistry` already resolves all five action ids; `ExampleJsonc` ships a working
  `r r → tool.shape.rectangle` sample, proving the arm path end-to-end.

## Changes

- `src/Draw.App/Input/DefaultKeymap.cs` — added the five `a _` chord bindings to `DefaultJson`; changed
  the `ExampleJsonc` syntax-example chord from `"a s r"` to `"d r"` (so the comment no longer illustrates
  the syntax with a now-live `a` prefix).
- `src/Draw.App/Input/ShortcutHintsViewModel.cs` — `SelectSpecs` idle branch now returns the five
  quick-add chords (looked up live from the keymap). Other contexts (armed tool / connector / node)
  unchanged. No XAML change — `MainWindow.axaml` already renders `ShortcutHints.Hints`.

## Verification

- `dotnet build Draw.slnx` — passed (0 warnings; nullable warnings are build errors). ✅
- **GUI not renderable headless under WSL2** — manual check on Windows (`dotnet run --project src/Draw.App`):
  - `a` then `r` → rectangle tool armed ("Click on the canvas to place Rectangle."); click to place.
    Repeat `a a` (arms connector — drag between two nodes), `a c` (class), `a i` (interface), `a t` (table).
  - `a` alone shows `a` pending in the status bar, resolving/clearing on the second key or after timeout.
  - Idle status bar reads
    `… │ Shift+S Shapes · Shift+C Connectors · a r Rectangle · a a Association · a c Class · a i Interface · a t Table`.
  - Typing `a` inside a class-member / table-column edit → no tool armed (dispatcher suppressed).
  - Rebind/unbind `a r` in `keymap.json` → the idle Rectangle hint updates / disappears (live lookup).

## Notes

- No automated test (no `Draw.App` test project; GUI not headless-testable). Verification is build + manual.
