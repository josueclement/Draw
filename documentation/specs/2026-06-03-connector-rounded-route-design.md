# Connector route style "Rounded" (averaged mean curve): Design

Status: approved 2026-06-03. Branch: `feature/connector-rounded-route` (builds on
`feature/connector-editing`).

A `RouteStyle` alongside Straight / Orthogonal: a smooth curve whose shape is
**averaged from the bend points** added on the connector. It complements the connector-editing
waypoint feature — you add points with Ctrl+click and the rounded route smooths through them.

## 1. Behaviour (locked with the user)

- **Averaged "mean" curve.** The curve passes through the two boundary attachment endpoints and the
  **midpoints between consecutive bend points**, using each bend point as a pull-handle — the curve
  is pulled toward the points but does not pass through them (classic midpoint-quadratic smoothing).
- **No bend points → gentle S-curve**, bowing outward from each shape along its boundary normal.
- **True vector rendering** — real cubic bezier segments via `StreamGeometry` (crisp at any zoom,
  clean future SVG/PDF export), not a sampled polyline.
- Honours **forced anchors** and **waypoint editing** (add/move/remove) from the connector-editing
  feature.

## 2. Model (`Draw.Model/Connectors/RouteStyle.cs`)

Add `Rounded = 3`. Serializes as `"rounded"` (`JsonStringEnumConverter`); surfaces automatically in
the inspector dropdown (`Enum.GetValues<RouteStyle>()`). Existing files are unaffected; new ones
round-trip. No other model change — `Connector.Route` / `BendPoints` already cover it.

## 3. Route geometry (`Draw.Diagramming/Routing/ConnectorRoute.cs`)

A new poly-cubic representation (quadratics are converted to cubics so there is one uniform segment
type — exact conversion, no quality loss):

- `public readonly record struct CubicSegment(Point2D Control1, Point2D Control2, Point2D End);`
- `IReadOnlyList<CubicSegment>? Cubics` — non-null only for the rounded curve.
- `PolyCubic(Point2D start, IReadOnlyList<CubicSegment> segments)` — sets `Points` to the knot points
  `[start, seg0.End, …]` (so `Start`/`End` and label/decoration anchors keep working) and derives
  `StartDirection`/`EndDirection` from `segments[0].Control1 - start` and `end - last.Control2` (the
  first/last control directions), so arrow/diamond decorations orient correctly.

## 4. Strategy (`Draw.Diagramming/Routing/RoundedRouter.cs`, new)

`IConnectorRouteStrategy`, `Style => RouteStyle.Rounded`. Endpoints resolved anchor-aware (reusing
`ShapeBoundary.ResolveAnchor` / `IntersectFromCenter`, toward the first/last bend or opposite centre).

- **0 bend points:** one cubic with outward-normal controls (reusing the shared
  `RouteHelpers.SafeOutward` + handle-length logic) → the S-curve.
- **≥1 bend points:** `pts = Dedupe([source, …bends, target])`; midpoint-quadratic smoothing — for
  `i = 1..n-3` a quad `(control = pts[i], end = midpoint(pts[i], pts[i+1]))`, then a final quad
  `(control = pts[n-2], end = pts[n-1])` — each converted to a cubic
  (`C1 = S + ⅔(Q−S)`, `C2 = E + ⅔(Q−E)`). Degenerate (`< 3` points after dedupe) → a single
  near-straight cubic.

`SafeOutward` lives in `RouteHelpers` as the shared outward-normal helper.

## 5. View-model (`Draw.App/ViewModels/ConnectorViewModel.cs`)

- `BuildLineGeometry()`: a `Cubics` branch emits `BeginFigure(Start)` + one `CubicBezierTo` per segment.
- `GetFlattenedPoints()`: a `Cubics` branch samples each segment (reusing `CubicAt`, 16/segment) so
  hit-testing (`ConnectorDistance`) and center-label placement (`Midpoint`) follow the curve.
- `SupportsWaypoints`: add `RouteStyle.Rounded` so Ctrl/drag/Alt waypoint editing applies.

## 6. DI (`Draw.App/Hosting/ServiceCollectionExtensions.cs`)

`services.AddSingleton<IConnectorRouteStrategy, RoundedRouter>();` — the dispatcher auto-discovers it.

No changes needed to the inspector UI, serialization, or toolbox.

## 7. Verification

No automated tests and no GUI under WSL2: `dotnet build Draw.slnx` clean, then manual checks on
Windows/macOS — set a connector to **Rounded**; S-curve with no points; Ctrl+click points and the
curve averages toward them (riding the midpoints); drag/remove points reshape it; decorations stay
oriented; clicking the curve selects it; the center label sits on the curve; save+reload preserves
`"rounded"`; undo/redo works.
