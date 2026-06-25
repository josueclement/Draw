# Phase 2 — Flowchart shapes

Adds 8 standard flowchart symbols in a new **Flowchart** ribbon dropdown (Insert ▸ Common) and a
new "_Flowchart" submenu in the Shift+S tool-menu: **Terminator** (stadium), **Cylinder/Database**,
**Document** (wavy bottom), **Predefined process** (double bars), **Manual input** (slanted top),
**Off-page connector** (home-plate), **Display**, **Delay** (D-shape).

Manual input and Off-page connector are pure polygons (outline only). The other six are
curved/multi-figure: explicit geometry in `ShapeGeometryBuilder` + `ShapeSvgPathBuilder`, with the
bounding rectangle as the routing polygon in `ShapeOutline` (acceptable, like RoundedRectangle/Note).

## Changes

1. `src/Draw.Model/Nodes/ShapeKind.cs` — append `Terminator=16 … Delay=23`.
2. `src/Draw.Diagramming/Geometry/ShapeOutline.cs` — polygons for Manual input / Off-page connector;
   rectangle routing for the six curved kinds.
3. `src/Draw.App/Rendering/ShapeGeometryBuilder.cs` & `ShapeSvgPathBuilder.cs` — geometry/SVG for the
   six curved kinds (Terminator reuses the rounded-rect builder with radius = min(w,h)/2).
4. `src/Draw.App/ViewModels/ToolboxViewModel.cs` — `ShapeToolItem` for each kind + a `FlowchartHeader`.
5. `src/Draw.App/Views/MainWindow.axaml` — new `FlowchartDropDown` in the Common group.
6. `src/Draw.App/Views/MainWindow.axaml.cs` — `WireDropdown(FlowchartDropDown, SelectShapeToolCommand)`.
7. `src/Draw.App/Resources/ToolMenus.axaml` — new "_Flowchart" submenu.
8. `src/Draw.App/Resources/ToolIcons.axaml` — `ToolIcon.*` glyphs for the 8 shapes.
9. `tests/Draw.Diagramming.Tests/ShapeBoundaryTests.cs` — a ray-intersection test per kind.

## Verification

- `dotnet build Draw.slnx` clean; `dotnet test --solution Draw.slnx` green.
- Visual (user, Windows/macOS): each shape draws from the Flowchart dropdown, renders correctly
  (Cylinder lid, Document wave, Display/Delay caps), connects, edits label, round-trips save + SVG.
</content>
