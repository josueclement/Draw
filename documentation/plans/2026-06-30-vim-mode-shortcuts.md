# Vim-style shortcuts: `:` command line + h/j/k/l navigation + u/U undo

## Goal

Make the keyboard-forward editor feel more like neovim with two additions on top of the existing
chord input layer:

1. A bottom **`:` command line** for file/lifecycle actions.
2. Normal-mode **`h`/`j`/`k`/`l`** to move the selection between shapes, **Ctrl** to grow it, and
   **`u`/`U`** for undo/redo.

Everything reuses the existing save/quit/selection/undo plumbing — this wires new input gestures onto
commands plus one new selection operation; no document-model changes.

## `:` command line

- Opens when **`:` is typed** outside any text field and with no overlay open. Triggered on the typed
  **character** via the window `TextInput` handler (`MainWindow.OnGlobalTextInput`), not a physical key, so
  it works regardless of where `:` sits on the keyboard layout. A normally-collapsed `TextBox`
  (`CommandLineBar`/`CommandLineBox` in `MainWindow.axaml`) appears at the bottom, prefilled with `:` and
  focused; while it has focus the existing `IsTextEntryFocused()` guard suppresses the chord dispatcher.
- `Enter` parses + runs and closes; `Escape` cancels; an unrecognised command flashes a transient status
  message (`ChordInputDispatcher.Flash`, now public) and closes.
- Parsing is a pure `VimExCommand.Parse` (`Draw.App/Input/VimExCommand.cs`): strips the leading `:`, reads a
  trailing `!` as *force*, maps to `VimExKind` (`Write`/`Quit`/`WriteQuit`/`QuitAll`).
- Commands (executed in `MainWindow.ExecuteCommandLineAsync`, all reuse existing logic):

  | Command  | Action |
  |----------|--------|
  | `:w`     | `ShellViewModel.SaveActiveDocumentAsync` (prompts for a path if untitled) |
  | `:q`     | `ShellViewModel.CloseActiveDocumentAsync(force:false)` — close active tab, prompt if modified |
  | `:q!`    | `CloseActiveDocumentAsync(force:true)` — discard + close, no prompt |
  | `:wq`    | save; only if it succeeds, close the active tab |
  | `:qa`    | `MainWindow.ConfirmAndCloseAsync()` — quit, prompting per modified tab |
  | `:qa!`   | `_forceClose = true; Close()` — quit, discard all |

  `:q` closes a **tab** (a tab is a document/buffer); `:qa` quits the whole app — matching vim's
  window-vs-`:qa` split.

## h/j/k/l navigation + u/U

- Handled in `DiagramView.OnKeyDown` next to the existing arrow-nudge (these keys are unbound in the keymap,
  so they bubble past the suppressed-while-typing chord dispatcher). `TryVimDirection` maps `h/j/k/l` →
  left/down/up/right.
- `DiagramDocumentViewModel.SelectNearestInDirection(MoveDirection, extend)` drives selection:
  - Reference point = the **active-node cursor** (`ActiveNode`, seeded by a plain click / single selection),
    else the selection's bounding-box centre, else the viewport centre.
  - The nearest shape is found by the pure, headless-tested
    `Draw.Diagramming/Geometry/DirectionalNavigator.FindNearest` — candidate centre must lie in the pressed
    direction; score = distance along the axis + a weighted cross-axis penalty, so an aligned neighbour wins.
  - Plain move → `SelectOnly` the target; **Ctrl** → `SelectionCoordinator.Select` adds it (a growing chain,
    never deselects). The cursor advances to the target. With nothing selected, the first press just selects
    the node nearest the viewport centre as a starting anchor. Nodes only (a connector selection is cleared);
    no edge wrap-around.
- `u` → `Undo()`, `U`/`Shift+U` → `Redo()` (gated on `CanUndo`/`CanRedo`), mirroring `Ctrl+Z`/`Ctrl+Y`.
  Selection changes are not undoable.

## Tests

`tests/Draw.Diagramming.Tests/DirectionalNavigatorTests.cs` covers `FindNearest`: directional filtering,
same-axis exclusion, alignment-favoring tie-breaks, vertical directions, empty candidates. The Avalonia
view/VM glue and the `:` command line are not unit-tested (pure-logic-only test convention) — verified by
running the app.

## Status

Build clean, 396 tests green. **Pending visual verification** on Windows/macOS — the `:` trigger, command
box focus/rendering, and h/j/k/l/u behavior can't be exercised headless under WSL2.

## Out of scope

`:x`/`:wa`, ranges/counts (`3l`), other motions, registers/yank, dot-repeat; remapping the vim bindings via
`keymap.json`; a distinct visual for the active-node cursor.
