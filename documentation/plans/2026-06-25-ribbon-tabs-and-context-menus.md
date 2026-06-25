# Ribbon tab split + richer shape context menu

**Status:** implemented; pending visual verification (WSL2 has no display — verify on Windows/macOS).

## Objective

On small screens the ribbon's busiest tabs (Home, Insert, Arrange) packed too many groups into one
tab, so groups got clipped. Spread the heavy groups across more dedicated tabs (fewer groups per tab
→ less horizontal pressure), and surface common view/style actions on the shape right-click menu so
they're reachable without hunting through the ribbon. All commands and icons already existed — this
is markup restructuring + reuse, no new behavior.

## Changes

### Ribbon layout (`src/Draw.App/Views/MainWindow.axaml`)

Tabs go from 4 to **9**, in order:
`Home · Insert · UML · ER · Mind map · Styles · Arrange · View · Export`.

- **Export** group leaves Home and becomes the last tab (Image / SVG / Copy image).
- **UML**, **ER**, **Mind map**, **Styles** groups each leave Insert and become their own tab.
- **Insert image** moves out of its own `Image` group into Insert ▸ **Common** (the `Image` group is
  removed); Insert is left with just **Common**.
- Insert stays the default-selected tab (still index 1; `MainWindow.axaml.cs` unchanged here).
- Moved tool dropdowns keep their `x:Name`s, so the existing `WireToolDropdowns` wiring (by field
  name) is unaffected.

### Arrange ▸ Order as a dropdown

The four Order buttons collapse into one `RibbonDropDownButton x:Name="OrderDropDown"`, mirroring the
existing `AlignDropDown` (same `ToolIcon.*` glyphs, `IsEnabled` bound to
`ActiveDocument.HasNodeSelection`). Wired in `MainWindow.axaml.cs` via a new `WireOrderDropdown`
helper (shared `RelayCommand<ZOrderOperation>`, auto-closes the dropdown on pick), calling the
already-public `DiagramDocumentViewModel.ReorderSelected`.

### Shape context menu (`src/Draw.App/Views/DiagramView.axaml.cs`)

`BuildArrangeMenu` now takes the `ShellViewModel` (resolved from the view's window DataContext via
`TopLevel.GetTopLevel(this)`), since theme/inspector/snap/styles live on the shell. Three submenus
are added, reusing existing commands:

- **Styles** (directly under Icons) — one item per `StylePalette.Swatches` swatch (colour-chip icon +
  `ApplyCommand`), then Reset / No fill. Gated on a selection.
- **Zoom** (bottom) — Zoom in / out / Reset / Fit to content (document-VM commands).
- **Appearance** (bottom) — Toggle theme (command) + checkable Properties / Snap to grid (flip the
  shell properties the ribbon toggles bind two-way).

Helpers added: `BuildStylesMenu`, `BuildZoomMenu`, `BuildAppearanceMenu`, `SwatchIcon`.

## Files touched

- `src/Draw.App/Views/MainWindow.axaml` — ribbon tab/group restructure + Order dropdown.
- `src/Draw.App/Views/MainWindow.axaml.cs` — `WireOrderDropdown` + constructor call.
- `src/Draw.App/Views/DiagramView.axaml.cs` — shell-aware `BuildArrangeMenu` + Styles/Zoom/Appearance submenus.

No model/`Draw.Diagramming` changes; no new packages; no new VM patterns.

## Verification

- `dotnet build Draw.slnx` — clean (0 warnings/errors; nullable warnings are build errors here).
- `dotnet test --solution Draw.slnx` — 370 passing (pure-logic; unaffected, confirmed green).
- Visual (Windows/macOS): 9 tabs in order with Insert selected on launch; Insert ▸ Common includes
  Insert image and no standalone Image group; UML/ER/Mind map/Styles/Export tabs populated and
  functional; Arrange ▸ Order is one dropdown that reorders + auto-closes and disables with no
  selection; right-clicking a shape shows the Styles submenu under Icons (full palette + Reset/No
  fill) and Zoom + Appearance at the bottom, with Properties/Snap reflecting and toggling state and
  Toggle theme flipping the theme.

## Out of scope (possible follow-ups)

- Splitting the single-group UML tab into finer sub-groups.
- Tab-strip overflow behavior on very narrow windows (9 tabs) — relies on Carbon ribbon defaults.
