# Connector editing — Implementation plan

Branch: `feature/connector-editing`. Design: `documentation/specs/2026-06-03-connector-editing-design.md`.

Cross-cutting enhancement to Phase 2 connectors: user-forced connection points (free along the
outline), editable waypoints (Ctrl+click add / drag / Alt+click remove) on straight + orthogonal
connectors, and movable source/center/target labels.

## Steps

1. **Model** — `Draw.Model/Connectors/Connector.cs`: add `SourceAnchor`, `TargetAnchor` (relative
   `(u,v)`) and `SourceLabelOffset` / `CenterLabelOffset` / `TargetLabelOffset` (world delta), all
   `Point2D?`; copy them in `Clone()`. Backward compatible (null = current behaviour).

2. **Geometry** — `Draw.Diagramming/Geometry/ShapeBoundary.cs`: add
   `ResolveAnchor(kind, bounds, relative)` (ray-cast through the relative point).

3. **Routing** — `ConnectorRouteRequest` gains optional `SourceAnchor`/`TargetAnchor`;
   `StraightRouter` / `OrthogonalRouter` / `BezierRouter` use `ResolveAnchor` when an anchor is set,
   else the existing `IntersectFromCenter`.

4. **Connector VM** — `Draw.App/ViewModels/ConnectorViewModel.cs`: pass anchors into the request;
   add label offsets + `NaturalLabelAnchor`/`LabelDisplay`; add read accessors (`RouteStart`,
   `RouteEnd`, `Waypoints`, `SourceAnchored`, `TargetAnchored`, `SupportsWaypoints`); add mutators
   (`Set*Anchor`, `InsertBendPointAt`, `MoveBendPoint`, `RemoveBendPoint`, `SnapBendPointToGrid`,
   `SetLabelOffset`, `SnapLabelToGrid`); add `ConnectorLabelKind`.

5. **View** — `Draw.App/Views/DiagramView.axaml.cs`: add `EndpointMove`/`WaypointMove`/`LabelMove`
   drag modes; code-based connector-handle hit-testing (endpoints → waypoints → labels) in select
   mode before the node hit-test; `Ctrl`+click-on-line add; `Alt`+click remove/reset; draw connector
   handles in `UpdateHandles()`; snap on release.

6. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows/macOS.

## Status

- [x] 1 Model  · [x] 2 Geometry  · [x] 3 Routing  · [x] 4 Connector VM  · [x] 5 View  · [x] 6 Build (clean)

Implemented on `feature/connector-editing`; build is clean (nullable-as-error). Passed an adversarial
logic review (no blockers). Pending: manual verification on Windows/macOS (no GUI under WSL2).
