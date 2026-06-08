# JSON-configured keyboard shortcuts — Design

## Goal & constraints

Replace the two hard-coded shortcut locations with one data-driven keymap that handles single gestures
and vim/blender-style multi-key chords, defined in JSON (built-in defaults + per-user override). View
models stay decoupled from the rendering layer; no new dependencies; `System.Text.Json` and the
`IOptions<T>`/DI conventions are reused.

## Architecture

```
KeymapService (loads + merges defaults & user file)  ──►  IReadOnlyList<ParsedBinding>
                                                                │
ChordInputDispatcher  ◄── KeymapActionRegistry (action id → live command resolver)
   │  buffer + DispatcherTimer; exact-match dict + prefix set
   ▼
MainWindow tunnel KeyDown handler ──► HandleKeyDown(e) ──► command.Execute(param)
                                       (suppressed while a text surface is focused)
KeymapStatusViewModel  ──► status-bar TextBlock (pending chord / transient message)
```

All types live in `src/Draw.App/Input/` (namespace `Draw.App.Input`). They depend only on
`Avalonia.Input`/`Avalonia.Threading` value types and the existing view models — no `Avalonia.Controls`
in the registry/dispatcher.

## Keymap file & schema

`{ "bindings": [ { "keys": "<sequence>", "action": "<id>" }, … ] }`, camelCase, JSONC-tolerant
(comments + trailing commas). `keys` is a single gesture (`"Ctrl+Shift+S"`, `"Delete"`, `"F2"`) or a
space-separated chord (`"a s r"`). Modifiers: `Ctrl/Control`, `Shift`, `Alt`, `Meta/Cmd/Win`. Keys
follow Avalonia's `Key` enum (`A..Z`, `D0..D9`, `Oem*`, `Delete`, `Escape`, `F1..F12`); single digits
are mapped to `D0..D9` by the parser.

**Locations** (`%APPDATA%/Draw/`, via `Environment.SpecialFolder.ApplicationData`, mirroring
`RecentFilesService`): `keymap.json` (loaded), `keymap.example.json` (written on first run, commented,
lists the full action vocabulary). **Merge**: defaults keyed by parsed key-sequence; each user binding
upserts (last wins) or, with an empty/missing `action`, unbinds the same sequence. **Resilience**: a
missing or corrupt user file (`IOException`/`JsonException`/`UnauthorizedAccessException`) falls back to
defaults; an unparseable `keys` entry is skipped; an action id unknown to the registry is dropped when
the dispatcher builds its lookup (`Debug.WriteLine`).

## Action ids

Generated from the enums via `JsonNamingPolicy.CamelCase` so they match JSON enum casing and extend for
free when an enum value is added:

- `tool.shape.{…}` (`ShapeKind`), `tool.connector.{…}` (`RelationshipKind`),
  `tool.classNode.{…}` (`ClassNodeKind`), `tool.useCase.{…}` (`UseCaseNodeKind`), `tool.entity`,
  `tool.select`.
- `file.{new|newEr|open|save|saveAs|close}`, `edit.{undo|redo|copy|cut|paste|duplicate|delete|insertImage|
  spaceConnections|mergeConnections}`.
- `align.{…}` (`AlignmentMode`), `distribute.{…}` (`DistributionMode`), `zorder.{…}` (`ZOrderOperation`).
- `view.{zoomIn|zoomOut|zoomReset|fitToContent|toggleTheme}`, `export.{image|svg|copyImage}`.

Resolvers are closures over `ShellViewModel`. Document-scoped ids resolve through
`_shell.ActiveDocument` at fire time (null → no-op + "no active document"); shell/tool ids resolve
directly. `tool.*` adds route through `Toolbox.Select*Command` (arm) — `tool.select` calls
`ActivateSelectTool()`.

## Chord state machine

Buffer of `KeyStroke`. On each non-modifier key:

1. **Escape with a non-empty buffer** → `Reset()`, consume. (Empty buffer → falls through to its binding,
   e.g. `tool.select`.)
2. Build `tentative = buffer + Normalize(key, mods)`. `Normalize` strips Shift for bare letters/digits
   (no Ctrl/Alt/Meta) so a held Shift can't break a chord, while `Ctrl+Shift+L` stays distinct.
3. **Exact and not a longer prefix** → fire, clear.
4. **Is a (possibly-also-exact) prefix** → buffer it, remember the exact match (if any) as the pending
   commit, show pending, restart the idle timer, consume.
5. **No match, empty buffer** → return `false` (a lone unbound key is left for other handlers).
6. **No match, non-empty buffer** → if a shorter exact was pending, commit it and reprocess this key;
   else flash "no binding", clear.

Idle timeout (`ChordTimeoutMs`) commits a pending exact match or cancels the buffer. `Reset()` clears
on window deactivation / focus loss.

## Input routing & focus

Installed at the window with `AddHandler(InputElement.KeyDownEvent, …, RoutingStrategies.Tunnel)`: the
tunnel (preview) phase lets the dispatcher accumulate plain letters before a focused control or the old
bubble-phase handlers consume them. `e.Handled = true` only when the dispatcher consumes the key, so
menu commands, ribbon, and access keys (`Alt+F`) still work.

**Suppression**: at tunnel time `e.Source` is the window, so the guard consults
`FocusManager.GetFocusedElement()` — suppressed when it is (or is inside) a `TextBox`, `AutoCompleteBox`,
or `ComboBox`. This protects renaming, the inspector's type AutoCompleteBoxes, and enum ComboBox
type-ahead. `DiagramView`'s existing bubble-phase `e.Source is TextBox` guard and Escape→`EndEditing`
stay; the member/column editing handlers are untouched.

## Edge cases

- Pure modifier key presses don't buffer or cancel.
- `CanExecute == false` (e.g. copy with no selection) → consumed, no-op, no error (a known-but-disabled
  shortcut shouldn't fall through to the canvas).
- No collisions in the default scheme (verified: no binding is a prefix of another with matching leading
  strokes); the prefix/exact-with-pending logic still handles user configs that do collide.

## Verification

- `dotnet build Draw.slnx` clean (done; `WarningsAsErrors=nullable`).
- Manual run on Windows/macOS (WSL has no display):
  - Chords arm tools (`a s r`, `a c a`, `a t`); single gestures fire (`Ctrl+S`, `Delete`, align/z-order).
  - Status bar shows the pending sequence; ~1 s timeout clears it; an unknown sequence flashes.
  - Escape cancels a half-typed chord; with no chord it disarms the tool; in-place text editing still
    ends on Escape.
  - Typing in the inspector (TextBox / type AutoCompleteBox / enum ComboBox) does **not** trigger chords.
  - Menu items still invoke their commands (gesture hint text intentionally removed).
  - Delete `keymap.json` → defaults load + `keymap.example.json` appears; add an override → it applies;
    a corrupt file → silent fallback; an `""`-action entry unbinds a default.
