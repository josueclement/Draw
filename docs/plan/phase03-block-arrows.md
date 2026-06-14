# Phase 3 — Block arrows

Adds 5 block-arrow shapes in a new **Arrows** ribbon dropdown (Insert ▸ Common) and a new
"_Arrows" Shift+S tool-menu submenu: **Arrow right, Arrow left, Arrow up, Arrow down,
Bidirectional**. All five are pure polygons — outline + UI only, no render-builder changes.

## Changes

1. `src/Draw.Model/Nodes/ShapeKind.cs` — append `ArrowRight=24 … ArrowDouble=28`.
2. `src/Draw.Diagramming/Geometry/ShapeOutline.cs` — `GetPolygon` cases (7–10 vertices each).
3. `src/Draw.App/ViewModels/ToolboxViewModel.cs` — `ShapeToolItem` per arrow, an `ArrowsHeader`,
   and the `Arrow` category mapping in `CategoryOf`.
4. `src/Draw.App/Views/MainWindow.axaml` — new `ArrowsDropDown` in the Common group.
5. `src/Draw.App/Views/MainWindow.axaml.cs` — `WireDropdown(ArrowsDropDown, SelectShapeToolCommand)`.
6. `src/Draw.App/Resources/ToolMenus.axaml` — new "_Arrows" submenu.
7. `src/Draw.App/Resources/ToolIcons.axaml` — `ToolIcon.Arrow*` glyphs.
8. `tests/Draw.Diagramming.Tests/ShapeBoundaryTests.cs` — a ray-intersection test per arrow.

## Verification

- `dotnet build Draw.slnx` clean; `dotnet test --solution Draw.slnx` green.
- Visual (user, Windows/macOS): each arrow draws from the Arrows dropdown, points the right way,
  connects, edits its label, round-trips save + SVG.
</content>
