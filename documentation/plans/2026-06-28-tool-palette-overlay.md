# Tool palette overlay: neovim-style Shift+S / Shift+C picker — Implementation plan

Branch: `feat-tool-palette-overlay`.

Replaces the static hierarchical `ContextMenu` that `Shift+S` (add shape) and `Shift+C` (add
connector) used to open at the cursor with a **centered, neovim-style overlay**. It is a two-step
**letter drill-down**: first the categories (each with a mnemonic letter), then the chosen category's
items (mnemonic letter + icon + name) in a multi-column grid over a dim backdrop. Choosing an item (by
its letter or a click) **arms** the existing toolbox tool and closes — the arm/place/drag machinery is
unchanged, only the *selection* UI is new.

## Decisions

- **Two-step letter drill-down, no text filtering.** Categories → items. Matches the request; closest
  to a vim `:`-style picker driven by keystrokes rather than search.
- **Mnemonic letters, auto-derived, scoped per screen.** First free a–z letter of the name; on
  collision the next free letter in the name; then any unused a–z. The 6 categories compete only among
  themselves; a category's items only among themselves. Deterministic and order-stable. The algorithm
  is the one unit-tested piece (`MnemonicAssigner` in `Draw.Diagramming`, no Avalonia dependency).
- **Mirror the old catalog exactly.** Same 6 shape categories (Common, Flowchart, Arrows, UML incl.
  Class/Interface/Enum, Actor/Use case/System boundary, Package/Component/Deployment, ER Table,
  Mind-map topics) and 4 connector categories (Common, UML, ER, Mind map), same names, same glyphs.
- **Replace the old menu entirely.** `Resources/ToolMenus.axaml` and its code-behind wiring are deleted.
- **Input = letters + mouse click.** Esc backs out one level (items → categories) then closes; clicking
  the dim backdrop dismisses. No arrow-key cursor.
- **Icons shown,** reusing the existing `ToolIcon.*` resources and Phosphor glyphs — resolved to
  `Avalonia.Media.Geometry` in the Rendering layer exactly as `NodeMarkerVisuals` does, so the
  view-model can hold the glyph without referencing any Avalonia control.
- **Re-trigger while open switches family.** `Shift+S` re-opens/resets the shapes screen, `Shift+C`
  switches to connectors — achieved by repointing `ShowToolMenuCommand` to open the palette and letting
  modified chords still flow to the dispatcher, so the keymap stays authoritative (no hard-coded keys).

## How it works

- `Shift+S`/`Shift+C` → keymap actions `menu.shapes`/`menu.connectors` → `ShellViewModel.ShowToolMenuCommand`
  (unchanged `CanExecute = HasActiveDocument`) → now calls `ToolPalette.Open(family)` instead of raising
  an event.
- `ToolPaletteViewModel` builds the category list (zipping catalog names through `MnemonicAssigner`),
  then on drill-in builds the item list the same way. Choosing an item dispatches to the matching
  `ToolboxViewModel.Select*ToolCommand` (mirrors the retired `ArmCommandFor`) and closes.
- The keyboard is owned by the window: `MainWindow.OnGlobalKeyDown` intercepts when the palette is open
  — Esc → `Back()`, unmodified `A`–`Z` → `HandleLetter`, and any modified chord falls through to the
  chord dispatcher (so `Shift+S`/`Shift+C` re-open/switch). The view-model exposes only semantic
  methods, so it stays free of `Avalonia.Controls`/`Avalonia.Input`.
- `ToolPaletteView` is a full-window overlay added to `MainWindow`'s Grid wrapper, collapsed unless
  `ToolPalette.IsOpen`. `UniformGrid` item panels (not a `Canvas`), so the Avalonia-12
  `ItemsControl`+`Canvas` `ClipToBounds` trap does not apply.

## Files touched

- **New** `src/Draw.Diagramming/Mnemonics/MnemonicAssigner.cs` — pure per-screen letter assignment.
- **New** `tests/Draw.Diagramming.Tests/MnemonicAssignerTests.cs` — collisions, per-screen scoping,
  determinism, in-name fallback, >26 exhaustion.
- **New** `src/Draw.App/Rendering/ToolPaletteCatalog.cs` — `ToolArm` union + catalog records + the
  declarative, icon-resolving, cached catalog (transcription of the old `ToolMenus.axaml`).
- **New** `src/Draw.App/ViewModels/ToolPaletteViewModel.cs` — open/close/back/letter state machine,
  display records, arm dispatch.
- **New** `src/Draw.App/Views/ToolPaletteView.axaml` (+ `.axaml.cs`) — dim backdrop + centered grid.
- `src/Draw.App/Hosting/ServiceCollectionExtensions.cs` — register `ToolPaletteViewModel` singleton.
- `src/Draw.App/ViewModels/ShellViewModel.cs` — own `ToolPalette`; repoint `ShowToolMenuCommand`;
  delete the `ToolMenuRequested` event.
- `src/Draw.App/Views/MainWindow.axaml` — drop the `ToolMenus.axaml` merge; add `<v:ToolPaletteView>`.
- `src/Draw.App/Views/MainWindow.axaml.cs` — palette branch in `OnGlobalKeyDown`; remove
  `WireToolMenus`/`WireToolMenu`/`ArmCommandFor`/`OnToolMenuRequested` and their orphaned usings.
- `src/Draw.App/Views/DiagramView.axaml.cs` — remove `OpenToolMenu`.
- **Deleted** `src/Draw.App/Resources/ToolMenus.axaml`.

## Testing

- `dotnet build Draw.slnx` clean (0 warnings/errors). `dotnet test --solution Draw.slnx` green
  (377 tests, incl. 7 new mnemonic tests).
- Visual verification pending on Windows/macOS (WSL2 has no display): popup centering + dim backdrop;
  category → item drill-down; letter and click selection arm the tool; `Shift+C`/`Shift+S` switch family
  while open; Esc back/close; backdrop dismiss; light/dark theming; ribbon dropdowns still arm tools.

## Not done (deliberate)

- No type-to-filter/fuzzy search (drill-down only, by request).
- No arrow-key cursor (letters + mouse only).
- The taxonomy still lives in three hand-synced places (ribbon XAML, `ToolboxViewModel`, and this
  catalog); unifying them is out of scope.
