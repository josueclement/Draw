# Quick style palette — Implementation plan

Branch: `feature/quick-style-palette`. Design:
`documentation/specs/2026-06-04-quick-style-palette-design.md`.

A new **Styles** group on the **Home** ribbon tab holds an always-visible grid of 10 pastel swatches
plus **Reset to default** and **No fill** buttons. Clicking a swatch recolours the selection (all
nodes + a selected connector) with a coordinated fill + stroke + text combination. Swatches are
**theme-aware**: each carries a Light and a Dark variant and styled elements recolour automatically on
theme toggle — generalising the existing single default-fill sentinel into ~10 named palette tokens.

## Steps

1. **Palette data** — `Draw.Diagramming/Styling/StylePalette.cs` (new): `SwatchVariant(Fill, Stroke,
   Text)` and `StyleSwatch(Id, Name, Light, Dark)` records (`Variant(bool isDark)` helper); the
   10-entry `Swatches` table; `TryGet(string? id, out StyleSwatch)`. `ArgbColor` only, no Avalonia.

2. **Model token** — `Draw.Model/Styling/ShapeStyle.cs` + `ConnectorStyle.cs`: add
   `public string? PaletteId { get; set; }`, copy it in `Clone()`. Serialisation needs no attribute
   (global `DefaultIgnoreCondition = WhenWritingNull` omits null; old files load unchanged).

3. **Resolution in VMs**:
   - `Draw.App/ViewModels/NodeViewModelBase.cs`: `Fill` / `Stroke` / `Foreground` first try
     `StylePalette.TryGet(Model.Style.PaletteId)` → `Variant(_theme.IsDark)`; else the existing
     default-sentinel / raw-colour path. `RaiseStyleChanged` already covers all three.
   - `Draw.App/ViewModels/ConnectorViewModel.cs`: add `IThemeService` ctor param; `Stroke` (and a new
     theme-aware label brush) resolve `PaletteId` → `Variant(IsDark).Stroke` / `.Text`.
   - `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: pass `_theme` to `new ConnectorViewModel(...)`
     (both call sites, lines ~370 and ~1079); in `OnThemeChanged` also loop `Connectors` →
     `RaiseStyleChanged()`.

4. **Apply** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: `ApplyStyleSwatch(StyleSwatch)`,
   `ResetStyleToDefault()`, `ApplyNoFill()`. Each wraps `NotifyStyleEditStarting()` → mutate every
   `SelectedNodes` entry + `SelectedConnector` → `RaiseStyleChanged()` → `MarkModified()`; no-op when
   nothing selected. Swatch apply bakes `Variant(_theme.IsDark)` into the raw colour fields and sets
   `PaletteId`.

5. **Detach on manual edit** — `Draw.App/ViewModels/InspectorViewModel.cs`: `ApplyFill` /
   `ApplyShapeStroke` also set `PaletteId = null`; `ApplyConnectorStroke` clears the connector's
   `PaletteId`. Thickness / font / alignment edits keep the link.

6. **Presentation VMs** — `Draw.App/ViewModels/StylePaletteViewModel.cs` +
   `StyleSwatchViewModel.cs` (new). Palette VM: injects `IThemeService`, subscribes to `ThemeChanged`,
   exposes `Swatches` + `ResetCommand` + `NoFillCommand`, holds the active document
   (`SetActiveDocument`). Swatch VM: `Name`, theme-aware `PreviewBackground` / `PreviewBorder`,
   `ApplyCommand` → palette VM → active doc. On `ThemeChanged` the palette VM re-raises each swatch's
   preview brushes.

7. **Wiring** — `ServiceCollectionExtensions.cs`: register `StylePaletteViewModel` (singleton),
   inject into `ShellViewModel`; `ShellViewModel`: expose `StylePalette`, call
   `StylePalette.SetActiveDocument(field)` in the `ActiveDocument` setter (beside `Inspector.SetTarget`).

8. **Ribbon UI** — `Draw.App/Views/MainWindow.axaml`: new `<ribbon:RibbonGroup Header="Styles">` on the
   Home tab after Edit. A `UniformGrid` (5 cols) `ItemsControl` over `StylePalette.Swatches` (small
   coloured `Button`s, tooltip = name, `Command={Binding ApplyCommand}`), then Reset + No-fill buttons.
   Group `IsEnabled="{Binding ActiveDocument.HasSelection}"`. Reset icon: Phosphor
   `arrow_counter_clockwise`; No-fill: new `ToolIcon.NoFill` glyph in `ToolIcons.axaml` if needed.

9. **Build** `dotnet build Draw.slnx` clean (nullable-as-error); then manual verification on Windows
   (no GUI under WSL2).

## Reuses, not rebuilds

- `DiagramDocumentViewModel.NotifyStyleEditStarting` / `SelectedNodes` / `SelectedConnector` /
  `MarkModified` / `RaiseStyleChanged` (style-edit + memento undo).
- `IThemeService.IsDark` / `ThemeChanged`, the `OnThemeChanged` refresh loop, the default-fill
  sentinel in `NodeViewModelBase`, and `StyleMappingExtensions.ToBrush`.

## Status

- [x] 1 Palette data · [x] 2 Model token · [x] 3 Resolution · [x] 4 Apply · [x] 5 Detach ·
  [x] 6 Presentation VMs · [x] 7 Wiring · [x] 8 Ribbon · [x] 9 Build (clean, 0 warnings)

Implemented on `feature/quick-style-palette`; build clean (nullable-as-error), and because compiled
bindings are on every new ribbon binding path resolved at compile time. Pending manual verification on
Windows (no GUI under WSL2):
1. Select a shape → click a swatch: fill + stroke + text recolour as a set; one undo reverts it.
2. Select several shapes (+ a connector) → one swatch click restyles all in one undo step.
3. Toggle theme (View ▸ Toggle theme): styled shapes/connectors recolour to the dark/light variant
   automatically; the ribbon swatch faces re-tint too.
4. Reset to default → element returns to the theme-adaptive default and keeps following theme.
5. No fill → outline-only shape; stroke remains.
6. Hand-edit the Inspector `Fill` hex on a swatched shape → it detaches (stops adapting on toggle);
   other swatched shapes still adapt.
7. Save + reopen → styles persist; a pre-existing `.draw` file opens unchanged.
8. Styles group disabled with no selection, enabled with ≥1.
