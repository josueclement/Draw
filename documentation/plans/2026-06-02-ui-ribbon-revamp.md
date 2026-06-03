# UI Revamp — Ribbon Shell, Phosphor Icons & Carbon Theming

**Status:** Implemented on `feature/ribbon-ui-revamp` (non-headless tests green; visual pass pending on Windows/macOS — see Verification).

**Goal:** Replace the top text-button toolbar and the left-side ListBox tool palette with a
ribbon, give the whole app a consistent icon set and color palette, and drop the dull
white-fill/black-stroke shape default — using the author's own `Carbon.Avalonia.Desktop`
(ribbon + palette) and `PhosphorIconsAvalonia` (icons) libraries.

**Tech stack:** .NET 10, C# 14, Avalonia 12.0.4, CommunityToolkit.Mvvm, `Carbon.Avalonia.Desktop`
0.2.0, `PhosphorIconsAvalonia` 1.2.0, xUnit v3 on Microsoft.Testing.Platform.

## Decisions (from the requirements interview)

- **Chrome:** ribbon replaces the toolbar + left palette; the **menu bar is kept** (redundant but
  familiar), as are the inspector, document tabs and status bar.
- **Ribbon tabs (3):** **Home** (File · Edit) · **Insert** (drawing tools) · **View** (zoom · theme · export).
- **Tools:** one **dropdown button per category** (Shapes / Connectors / Class diagram / Use case);
  the dropdown header reflects the current pick. Select/Move is a toggle button.
- **Default shape style:** Fill `#FFEBEDF0`, Stroke `#FF3574F0`, Text `#FF1E1F22`, thickness 1.5
  (Carbon surface / accent / foreground). Connectors default to the same accent stroke.
- **Chrome recolor:** window, side panels, inspector and status bar adopt Carbon's theme-aware
  brushes; the canvas drawing surface + grid keep their existing brushes.
- **Icons:** Phosphor everywhere a match exists (ribbon, menu items, tab-close, inspector buttons);
  custom vector glyphs only for the UML items Phosphor lacks.

## Key library facts (verified against the restored assemblies)

- Carbon's `Themes/Fluent.axaml` is a **`ResourceDictionary`**, so it is merged via
  `ResourceInclude` in `Application.Resources` — **not** a `StyleInclude` in `Application.Styles`.
  `FluentTheme` stays in Styles (Carbon themes only its own controls).
- Ribbon model: `Ribbon`(Tabs) › `RibbonTab`(Header, Groups) › `RibbonGroup`(Header, Items) ›
  `RibbonButton` / `RibbonToggleButton`(IsChecked) / `RibbonDropDownButton`(Items of `RibbonMenuItem`).
  All buttons expose `IconData : Geometry?`.
- **`RibbonMenuItem` derives from `AvaloniaObject` (no `DataContext`)** — so a dropdown item's
  `Command` cannot be data-bound; it is assigned in code-behind. `Header`, `IconData`
  (`{pia:IconGeometry}` / `{StaticResource}`) and `CommandParameter` (`{x:Static}`) are parse-time
  and work in XAML.
- Phosphor `pia:IconGeometry` returns a `Geometry` → binds straight into `IconData`. Confirmed
  Phosphor also has `parallelogram`, `square_split_horizontal`, `list_numbers`, `user`,
  `bounding_box`, so those UML items reuse real icons; custom glyphs cover the rest.

## Files changed

- `Directory.Packages.props`, `src/Draw.App/Draw.App.csproj` — add the two packages
  (restore unifies Carbon's transitive deps cleanly; no extra pins needed).
- `src/Draw.Model/Styling/{ShapeStyle,StrokeStyle,FontSpec}.cs` — new default fill/stroke/text
  (single source; affects new shapes + connectors only — saved files keep their colors).
- `src/Draw.App/ViewModels/ToolboxViewModel.cs` — ctor + four `RelayCommand<TKind>`
  ("arm tool by kind", reusing the existing mutually-exclusive setters), `*Header` strings, `IsShapeMode`.
- `src/Draw.App/ViewModels/DiagramDocumentViewModel.cs` — `ZoomIn/Out/Reset` commands
  (clamp `[0.1, 8]`, mirroring the Ctrl+wheel handler).
- `src/Draw.App/Resources/ToolIcons.axaml` (new) — `StreamGeometry`/`GeometryGroup` glyphs for
  the 9 relationship caps (matched to `ConnectorDecorationBuilder`), ellipse, rounded-rect,
  trapezoid, interface lollipop.
- `src/Draw.App/App.axaml` — merge Carbon theme + ToolIcons via `ResourceInclude`.
- `src/Draw.App/Views/MainWindow.axaml` (+ `.axaml.cs`) — the 3-tab ribbon, grid re-index to
  `*,260`, chrome recolor, Phosphor icons on menu/tab-close/inspector buttons; code-behind
  `WireToolDropdowns` assigns each dropdown's shared command.
- Tests: `tests/Draw.Model.Tests/ShapeStyleTests.cs` (new defaults) and additions to
  `ToolboxViewModelTests` / `DiagramDocumentViewModelTests` (tool commands, headers, zoom).

## Verification

- `dotnet build Draw.slnx` — clean (0 errors; pre-existing `AVLN5001` Watermark warnings only).
- Non-headless suites green: Model 30/30, App (all 7 `*ViewModelTests`) 60/60, Diagramming 50/50.
- Custom glyph paths were parse-verified headlessly (correct bounds) before wiring.
- **Pending (must run on Windows/macOS):** the visual pass and `CanvasPlacementHeadlessTests` —
  that test uses real Skia rendering, which does not run under WSL2 (documented in CLAUDE.md).
  Visual checklist: 3 ribbon tabs render; each dropdown arms its tool (status-bar hint + header
  update); Select toggle reflects state; Zoom In/Out/Reset + `%`; theme toggle recolors ribbon and
  chrome; new shapes use the accent style; menu + keybindings still work; custom UML glyphs read
  correctly.

## Follow-ups (not done; out of scope)

- A dropdown can't bind a dynamic `ItemsSource`, so **Open Recent stays in the File menu only**
  (not surfaced on the ribbon).
- The dropdown buttons reflect the current pick via header text only (icon stays the category icon),
  per the parse-time-only icon constraint.
