# Evenly space connection points on a shape edge: Design

Status: approved 2026-06-04. Branch: `feature/connection-point-spacing`.

A cross-cutting editor enhancement. When several connectors land on the same side of a shape, their
attachment points bunch up wherever the automatic router or a manual drag left them, which looks
untidy. This adds a one-shot **Space connections** action that spreads the connectors attached to a
selected shape evenly along each edge they touch. It reuses the existing forced-anchor mechanism
(`Connector.SourceAnchor` / `TargetAnchor`), the whole-document memento undo, and the Arrange UI
surfaces introduced by align/distribute.

## 1. Goals / non-goals

In scope:

- For each selected shape, group the connector ends touching it by bounding-box **side**
  (left/top/right/bottom), and re-pin every side that has **≥2** ends so they are evenly spaced.
- Keep each connector on the side it currently touches; preserve the ends' current order along the
  side (just equalize the gaps).
- Pin the result as forced anchors so the spacing sticks when the shape is moved or resized.
- Two entry points, reusing the **Arrange** surfaces: a ribbon button and the right-click canvas menu.
- One undo step per action; a no-op (a selection with nothing to space) adds no undo entry.

Out of scope (noted as follow-ups):

- Continuous / automatic re-spacing as connectors are added or moved.
- A per-edge picker, or moving connectors onto a different side than the one they currently touch.
- A keyboard shortcut.
- Any model-schema or router change — this only computes and assigns `(u,v)` anchors.

## 2. Reference semantics

- A connector end's attachment is a relative `(u,v)` point in `[0,1]²` of the shape bounds, resolved
  to the outline by a ray cast from the centre (`ShapeBoundary.ResolveAnchor`). `null` = automatic.
- **Side classification** uses the current *resolved* world point (`ConnectorViewModel.RouteStart` /
  `RouteEnd`). Take `rx,ry` (the point relative to bounds, clamped to `[0,1]`); the nearer pair of
  edges wins — `min(rx,1−rx) ≤ min(ry,1−ry)` ⇒ a vertical side (`Left` if `rx≤0.5` else `Right`),
  otherwise a horizontal side (`Top`/`Bottom`). Ties resolve to the vertical side, deterministically.
- **Spacing**: within a side group of `N` ends ordered by their current along-side fraction, end `i`
  (0-based) is pinned at fraction `t = (i+1)/(N+1)` of the side — so the gap to each corner equals the
  inter-point gap. The anchor is `(0,t)` Left, `(1,t)` Right, `(t,0)` Top, `(t,1)` Bottom. For a
  rectangle `ResolveAnchor` maps these exactly onto the edge; for ellipses/diamonds/triangles they land
  on the corresponding arc/edge segment — visually reasonable, not arc-length-exact.
- A connector linking two selected shapes is spaced at **both** ends, each on its own shape/side.

## 3. Layer placement

- The math is pure and UI-agnostic (operates on `Rect2D` + `Point2D`, needs no shape kind), so it lives
  in `Draw.Diagramming` (`Layout/ConnectionDistributor.cs` + `BoxSide` enum), beside `ShapeArranger`.
- `DiagramDocumentViewModel` owns the operation (`SpaceSelectedConnections`) and the command
  (`SpaceConnectionsCommand`), mirroring the align/distribute commands. It reads each connector's
  current route, classifies, then re-pins via `ConnectorViewModel.SetSourceAnchor` / `SetTargetAnchor`
  (which recompute and re-raise). The ribbon button and context-menu item reference
  `ActiveDocument.SpaceConnectionsCommand`; no `ShellViewModel` change is needed.

## 4. Interaction notes

- Enable/disable: `CanSpaceConnections` is `≥1` selected shape; the operation itself is a no-op (and
  captures no undo) when no side of a selected shape has ≥2 ends. Refreshed in `RaiseSelectionChanged`.
- The right-click arrange menu now opens with **≥1** node selected (was ≥2) so a single shape can be
  tidied. Align/Distribute items grey themselves out below their own minimums (≥2 / ≥3); right-drag
  still pans and the middle button never opens the menu.
- Ribbon: a **Space connections** button in a new **Connections** group on the Arrange tab, with a
  custom `ToolIcon.SpaceConnections` glyph (a shape edge with three evenly-spaced connectors).
