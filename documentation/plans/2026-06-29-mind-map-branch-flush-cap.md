# Mind-map branch base flush to the node edge: Implementation plan

Branch: `fix-mind-map-branch-flush-cap`. No separate design spec — small, self-contained bug fix.

## Problem

A mind-map branch is a **filled tapered ribbon** (thick at the parent → thin at the child), built by
`TaperedStroke.BuildOutline`: each sampled centerline point is offset by ±half-width along its
**normal**, so the flat end "cap" where the ribbon meets a node is perpendicular to the **tangent at
that endpoint**. `TaperedStroke.Tangent` derives the endpoint tangent from a one-sided finite
difference (the chord to the next/previous sample of the *bending* curve), which is not the direction
the branch actually meets the node edge. Because the ribbon is wide at the parent and thin at the
child (`MindMapBranchStyle.WidthAt`: 9px → ~6px → …), the resulting mis-orientation is obvious on the
thick parent base but sub-pixel at the child tip. Two cases:

1. **Child moved off-axis** (anchors still at the edge midpoint): the branch curves, so the first
   chord is ~5° off the edge normal → the wide base is cut at a slight slant.
2. **After "Space connections"** (anchors fanned to off-centre positions, e.g. `(1, 0.25)`):
   `RoundedRouter` leaves along the *radial* direction `normalize(anchor − center)`, ~15° off the
   edge → the base is cut at a real slant.

The default ("merged" — every end on the edge midpoint, child directly across) is a straight ribbon,
so there is no problem there.

## Decision

Square each ribbon end to the **node-boundary outward normal at the attachment point**, not to the
curve tangent. For the centred-anchor case the edge normal equals the curve's leaving direction, so
the cap becomes perfectly flush and the wobble disappears; for the fanned case the cap squares to the
edge while the centerline still curves away — the base reads as flush, matching the child end.

Scope is kept to the mind-map ribbon: only the branch-outline path supplies the override, so ordinary
connector routing is untouched. Using the curve tangent (`_route.StartDirection`) instead was
rejected — it fixes only case 1. Making `RoundedRouter` leave perpendicular for off-centre anchors was
rejected as out of scope (it would change every anchored rounded connector); noted as a possible
follow-up if the slight near-base curve in fanned mind maps is unwanted.

## Files touched

1. **`src/Draw.Diagramming/Geometry/ShapeBoundary.cs`** — new
   `OutwardNormalAt(ShapeKind, Rect2D, Point2D)`: unit outward normal of the outline at the nearest
   boundary point — ellipse gradient on `EllipseBounds`, else the nearest `ShapeOutline.GetPolygon`
   edge's outward normal (sign-chosen away from the centre), with a radial/`+X` degenerate fallback.

2. **`src/Draw.Diagramming/MindMap/TaperedStroke.cs`** — `BuildOutline` gains optional
   `Point2D? startTangent`/`endTangent`; when supplied and non-degenerate they override the
   finite-difference tangent at `i == 0` / `i == last` (new `EndpointOrSampled` helper). Existing
   callers/tests are unaffected (params default to `null`).

3. **`src/Draw.App/ViewModels/ConnectorViewModel.cs`** — `GetBranchOutline()` passes the source/target
   edge normals as the start/end tangents: `startTangent = OutwardNormalAt(source, route.Start)`,
   `endTangent = OutwardNormalAt(target, route.End) * -1` (forward sense — out of the source, into the
   target — so the offset stays on the correct side and the ends don't pinch).

4. **SVG export** — no change; `DiagramSvgRenderer.EmitConnector` renders `GetBranchOutline()` directly
   and inherits the corrected outline.

5. **Tests** — `tests/Draw.Diagramming.Tests/ShapeBoundaryTests.cs` (`OutwardNormalAt`: rectangle four
   edges, off-centre right-edge point still `(1,0)`, ellipse rightmost/topmost) and
   `TaperedStrokeTests.cs` (explicit start/end tangents square the cap to the given direction on a
   bending centerline; omitted tangents keep the finite-difference behaviour).

## Notes / scope

- Mind-map-only behaviour change; ordinary connectors and routing are untouched.
- Verified: `dotnet build Draw.slnx` clean (0 warnings) and `dotnet test --solution Draw.slnx` green
  (390 tests). The visual result is pending verification on Windows/macOS (WSL2 can't render): in a
  mind map, drag a child off-axis and/or run *Space connections*, and confirm each branch's thick base
  now sits flush against the parent edge like the child end; re-export to SVG for parity.
