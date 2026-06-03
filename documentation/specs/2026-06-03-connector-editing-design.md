# Connector editing — forced anchors, waypoints, movable labels: Design

Status: approved 2026-06-03. Branch: `feature/connector-editing`.

A cross-cutting enhancement to the Phase 2 connector machinery. Today a connector's geometry is
fully automatic and the user can't influence it: endpoints are ray-cast from each shape's centre
(so they slide as shapes move), `Connector.BendPoints` exists but has no editing UI, and the three
labels are recomputed every frame with no way to nudge them. This adds direct, in-canvas control
over all three, reusing the existing selection-handle + `Ctrl`/`Alt`+click interaction model.

## 1. Goals / non-goals

In scope:

- **Forced connection points** — pin either endpoint anywhere on its shape's outline. Stored
  relative to the shape's bounds so the attachment tracks both move and resize. Applies to all
  three route styles (straight, orthogonal, bezier).
- **Editable waypoints** on straight + orthogonal connectors — `Ctrl`+click the line inserts a
  bend point (and starts dragging it), drag moves a bend point, `Alt`+click removes one.
- **Movable labels** (source / center / target) — drag to reposition; stored as a relative offset
  from the natural anchor so the label keeps following the line; `Alt`+click resets to default.
- Waypoints and dragged labels snap to the grid on release when snap is enabled.

Out of scope:

- **Reconnecting** an endpoint to a *different* shape by dragging it off — an endpoint drag stays
  on its current shape (clamped to its bounds). Changing the connected node stays a delete+recreate.
- **Waypoints on bezier** — the bezier router ignores bend points; `Ctrl`+click does nothing on a
  curve. (Forced anchors *do* apply to bezier.)
- An inspector UI for any of this — it is purely direct-manipulation.

## 2. Interaction model (`Alt` = remove/reset everywhere)

Right-click is bound to Pan, so there are no context menus; everything is left-click + modifier.
Handles appear only when **exactly one connector is selected**.

| Gesture | Target | Action |
|---|---|---|
| drag | endpoint handle (circle) | re-pin that end on its shape's outline |
| `Alt`+click | endpoint handle | reset that end to automatic |
| `Ctrl`+click | connector line (straight/orthogonal) | insert a bend point + start dragging it |
| drag | bend-point handle (square) | move that bend point |
| `Alt`+click | bend-point handle | remove that bend point |
| drag | label | move the label (stores relative offset) |
| `Alt`+click | label | reset the label to default |

Endpoint handles are circles, **filled when the end is pinned**, hollow when automatic; bend points
are squares — matching the existing zoom-scaled white/blue handle styling on the overlay canvas.

## 3. Model (`Draw.Model/Connectors/Connector.cs`)

Five new nullable fields; null = today's automatic/default behaviour, so existing `.draw` files load
unchanged and `Clone()` (used by the memento undo + serializer) copies them:

- `Point2D? SourceAnchor`, `Point2D? TargetAnchor` — relative `(u,v)` in `[0,1]²` of the node's
  bounds. Resolved to an outline point by casting a ray from the shape centre through
  `(X + u·W, Y + v·H)`. Because every `ShapeOutline` kind is convex, that ray meets the outline
  exactly once, so `(u,v)` ↔ outline point is a clean bijection that deforms correctly on resize.
- `Point2D? SourceLabelOffset`, `Point2D? CenterLabelOffset`, `Point2D? TargetLabelOffset` —
  world-unit delta added to the natural (computed) label anchor.

## 4. Geometry + routing (`Draw.Diagramming`)

- `ShapeBoundary.ResolveAnchor(kind, bounds, relative)` — builds the toward-point from `(u,v)` and
  returns `IntersectFromCenter(...)`. All boundary math stays in `Draw.Diagramming`.
- `ConnectorRouteRequest` gains optional `Point2D? SourceAnchor, TargetAnchor` (the relative values).
- `StraightRouter`, `OrthogonalRouter`, `BezierRouter`: where each computes an endpoint via
  `IntersectFromCenter`, instead use `ResolveAnchor(...)` when the corresponding anchor is set. No
  other change — `ConnectorRoute.Polyline` already derives the endpoint direction (for arrow/diamond
  orientation) from the adjacent segment, and the bezier outward control derives from
  `endpoint − centre`, which the forced point feeds correctly.

## 5. View-model (`Draw.App/ViewModels/ConnectorViewModel.cs`)

- `Compute()` passes the model anchors into the request.
- Label X/Y add the stored offset; `NaturalLabelAnchor(kind)` / `LabelDisplay(kind)` expose the
  pre-offset and post-offset positions for the view's drag + hit-testing.
- Read accessors for the view: `RouteStart`, `RouteEnd`, `Waypoints` (the bend points),
  `SourceAnchored` / `TargetAnchored`, `SupportsWaypoints`.
- Mutators (each mutates the model then re-routes/re-raises): `SetSourceAnchor`, `SetTargetAnchor`,
  `InsertBendPointAt` (picks the nearest logical segment, returns the index), `MoveBendPoint`,
  `RemoveBendPoint`, `SnapBendPointToGrid`, `SetLabelOffset`, `SnapLabelToGrid`.
- `ConnectorLabelKind { Source, Center, Target }` enum.

## 6. View / interaction (`Draw.App/Views/DiagramView.axaml.cs`)

The connectors layer is `IsHitTestVisible="False"`, so all hit-testing is code-based in world space,
reusing the `HandleScreenSize / Zoom` tolerance and the `EnsureUndoCaptured()` lazy-capture pattern
from node move/resize. New `DragMode`s: `EndpointMove`, `WaypointMove`, `LabelMove`. Connector-handle
hit-testing runs in select mode **before** the node hit-test (endpoint handles sit on shape
boundaries). `UpdateHandles()` draws the connector's endpoint/bend-point handles on the `Overlay`
canvas when one connector is selected. Snapping happens on pointer release.

## 7. Verification

No automated tests (removed 2026-06-03) and no GUI under WSL2, so: `dotnet build Draw.slnx` stays
clean (nullable = error), then manual verification on Windows/macOS per the plan's checklist
(pin/reset endpoints, add/move/remove waypoints to route around a shape, move/reset each label,
undo/redo as single steps, save+reload round-trips, old files still open).
