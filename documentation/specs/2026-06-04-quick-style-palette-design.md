# Quick style palette: Design

Status: approved 2026-06-04. Branch: `feature/quick-style-palette`.

A cross-cutting editor enhancement. Today the only way to recolour a shape is to type a hex value
into the Inspector's `Fill` / `Stroke` boxes — slow and uncoordinated. This adds a **quick palette**
of ready-made, theme-aware pastel styles in the ribbon: one click recolours the whole selection with
a coordinated **fill + stroke + text** combination, and styled elements **recolour automatically**
when the light/dark theme is toggled.

It generalises the pattern the app already uses for a single colour — a node whose
`Style.Fill == ShapeStyle.DefaultFill` is rendered with a theme brush (`NodeDefaultFillBrush`) and
re-resolved on theme toggle — into ~10 named palette tokens that each carry a coordinated stroke and
text colour as well.

## 1. Goals / non-goals

In scope:

- A curated set of **10 pastel swatches**, each defining a Light and a Dark variant of
  `(fill, stroke, text)`. Plus a **Reset to default** entry and a **No fill (transparent)** entry.
- Applying a swatch sets fill + stroke colour + text colour on every selected **node**, and stroke +
  label-text colour on a selected **connector** (connectors have no fill).
- **Live theme adaptation**: a styled element stores the swatch's `Id`; rendering resolves the
  current theme's variant, so toggling the theme recolours it with no document edit.
- An **always-visible swatch grid** in a new **Styles** group on the **Home** ribbon tab.
- One undo step per click; reuses the existing style-edit / memento-undo plumbing.

Out of scope (follow-ups):

- User-defined / editable palettes; per-document palettes.
- A full colour picker (the Inspector hex fields remain for arbitrary colours).
- Applying anything beyond the three colours (thickness, dash, font family/size stay as they are).
- An active-swatch indicator (apply-only; no "current style" highlight).
- A keyboard shortcut.

## 2. Reference semantics

- A **swatch** is `StyleSwatch(string Id, string Name, SwatchVariant Light, SwatchVariant Dark)` where
  `SwatchVariant(ArgbColor Fill, ArgbColor Stroke, ArgbColor Text)`. `Variant(bool isDark)` picks the
  pair. `Id` is the persisted token and is **stable forever** (e.g. `"blue"`).
- A style carries an optional `string? PaletteId`. `null` ⇒ today's behaviour (custom / default-
  sentinel colours). Non-null ⇒ the element is "linked" to that swatch and rendered theme-adaptively.
- **Resolution** (render time): if `StylePalette.TryGet(PaletteId, out swatch)` succeeds, the
  fill/stroke/text brushes come from `swatch.Variant(theme.IsDark)`. Otherwise fall back to the raw
  `ArgbColor`s on the style (which, for a linked element, hold the last-applied theme's colours — a
  safe snapshot if the `Id` is ever unknown).
- **Apply** bakes the current theme's variant into the raw colour fields *and* sets `PaletteId`, so the
  document still reads sensibly without palette resolution (export, copy to another app, old readers).
- **Detach**: hand-editing a colour the swatch owns (Inspector `Fill` / `Stroke` for nodes,
  connector stroke) clears `PaletteId` — the element becomes custom and stops adapting. Editing a
  non-colour property (thickness, font, alignment) keeps the link.

## 3. Layer placement

- **Palette data** is UI-agnostic (`ArgbColor` only), so it lives in `Draw.Diagramming`
  (`Styling/StylePalette.cs`): the `StyleSwatch` / `SwatchVariant` records, the 10-entry table, and
  `TryGet(id)`. No Avalonia dependency; trivially unit-testable.
- **Model token**: `string? PaletteId` on `ShapeStyle` and `ConnectorStyle` (`Draw.Model/Styling`),
  copied in `Clone()`. Serialised by the existing `System.Text.Json` setup; the global
  `DefaultIgnoreCondition = WhenWritingNull` already omits it when null, so existing `.draw` files
  round-trip byte-for-byte and load unchanged.
- **Resolution** lives in the view models (`Draw.App`): `NodeViewModelBase` (`Fill` / `Stroke` /
  `Foreground`) and `ConnectorViewModel` (`Stroke`, label colour). `NodeViewModelBase` already holds
  `IThemeService` (`IsDark`); `ConnectorViewModel` gains it via its constructor (both call sites are
  in `DiagramDocumentViewModel`, which holds `_theme`).
- **Apply** lives on `DiagramDocumentViewModel` (`ApplyStyleSwatch` / `ResetStyleToDefault` /
  `ApplyNoFill`), mirroring `InspectorViewModel.ApplyShapeStyle`: one `NotifyStyleEditStarting()`
  snapshot, mutate every `SelectedNodes` entry + `SelectedConnector`, `RaiseStyleChanged()`,
  `MarkModified()`.
- **Presentation**: `StylePaletteViewModel` + `StyleSwatchViewModel` (`Draw.App/ViewModels`),
  app-global (DI singleton), exposed on `ShellViewModel.StylePalette`. The palette VM holds the
  active document (kept in sync by `ShellViewModel.ActiveDocument`, beside `Inspector.SetTarget`),
  subscribes to `IThemeService.ThemeChanged` to re-tint the swatch faces, and routes
  apply/reset/no-fill to the active document.

## 4. Theme refresh

`DiagramDocumentViewModel.OnThemeChanged` currently re-raises only `Nodes`. It must also loop
`Connectors` and call `RaiseStyleChanged()`, so palette-linked connectors recolour on toggle. The
swatch faces in the ribbon re-tint because the palette VM re-raises each `StyleSwatchViewModel`'s
preview brushes from the same `ThemeChanged` event.

## 5. The palette

Ten low-saturation hues, each with a Light variant (soft tint, deeper same-hue stroke, near-black
text) and a Dark variant (muted mid-tone that reads on the `#1F1F1F` canvas, lighter stroke, near-
white text). Starting hexes (tuned visually on Windows; all in one table in `StylePalette.cs`):

| Id | Name | Light fill | Dark fill |
|----|------|-----------|-----------|
| blue | Blue | `#DCE8FB` | `#2E3F57` |
| teal | Teal | `#D5EEEA` | `#29423E` |
| green | Green | `#DDF0D8` | `#324A2C` |
| sand | Sand | `#F6F0CE` | `#4A4528` |
| orange | Orange | `#FBE6D2` | `#543F2A` |
| coral | Coral | `#FBDDDC` | `#553233` |
| pink | Pink | `#F8DCEC` | `#4F2E40` |
| purple | Purple | `#E7DEF7` | `#3D3357` |
| gray | Gray | `#E8E8EA` | `#3A3A3E` |
| slate | Slate | `#DEE4EA` | `#38414C` |

`Reset to default` clears `PaletteId` and restores `ShapeStyle.DefaultFill` / default stroke /
`FontSpec.DefaultColor` (so the element returns to the theme-adaptive default look). `No fill` clears
`PaletteId`, sets the fill to `ArgbColor.Transparent`, and leaves the stroke (outline-only shape).

## 6. Interaction notes

- Apply methods no-op (no undo entry) when nothing is selected. The Styles ribbon group binds
  `IsEnabled` to `ActiveDocument.HasSelection` so the grid greys out with an empty selection.
- Copy / paste / duplicate carry `PaletteId` via `Clone()`, so pasted elements stay theme-adaptive.
- New nodes are still created from `DiagramDocument.DefaultShapeStyle` (no `PaletteId`) — unchanged.
- Export / copy-image render through the same VM brushes, so palette colours apply at the current
  theme automatically; no export-specific handling.

## 7. Risks

- **Stable ids** — once shipped, a swatch `Id` must never change or saved diagrams lose their link
  (they fall back to baked colours, not a crash, but the visual tie breaks). Renames change `Name`
  only.
- **Colour quality** is subjective and unverifiable under WSL2; hexes are isolated in one table for
  easy tuning after a Windows run.
