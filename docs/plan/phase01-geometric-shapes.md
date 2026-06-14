# Phase 1 — Geometric shapes

Adds 7 general-purpose shapes to the existing **Shapes** ribbon dropdown and Shift+S *Common*
tool-menu submenu: **Hexagon, Pentagon, Octagon, Star** (5-point), **Cross/Plus, Cloud, Callout**.

Hexagon/Pentagon/Octagon/Star/Cross are pure polygons (outline only). Cloud and Callout are
curved/multi-figure: they get explicit geometry in both render builders plus an approximate
polygon in `ShapeOutline` for connector routing.

## Changes

1. `src/Draw.Model/Nodes/ShapeKind.cs` — append `Hexagon=9 … Callout=15` (append only).
2. `src/Draw.Diagramming/Geometry/ShapeOutline.cs` — `GetPolygon` cases for the 5 polygons; an
   approximate polygon for Cloud (bounding octagon) and Callout (box + tail) for routing.
3. `src/Draw.App/Rendering/ShapeGeometryBuilder.cs` — `Cloud`/`Callout` geometry (arc/stream).
4. `src/Draw.App/Rendering/ShapeSvgPathBuilder.cs` — matching SVG paths for `Cloud`/`Callout`.
5. `src/Draw.App/ViewModels/ToolboxViewModel.cs` — a `ShapeToolItem` for every new kind.
6. `src/Draw.App/Resources/ToolIcons.axaml` — `ToolIcon.*` 24×24 glyphs for the 7 shapes.
7. `src/Draw.App/Views/MainWindow.axaml` — `RibbonMenuItem`s in `ShapesDropDown`.
8. `src/Draw.App/Resources/ToolMenus.axaml` — `MenuItem`s under the *Common* submenu.
9. `tests/Draw.Diagramming.Tests/ShapeBoundaryTests.cs` — a ray-intersection test per new kind.

## Verification

- `dotnet build Draw.slnx` clean; `dotnet test --solution Draw.slnx` green.
- Visual (user, on Windows/macOS): each shape draws from the Shapes dropdown, renders, takes a
  connector on its boundary, edits its label, round-trips through save/reopen and SVG export.
</content>
