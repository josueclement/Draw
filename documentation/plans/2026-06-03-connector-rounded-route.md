# Connector route style "Rounded" — Implementation plan

Branch: `feature/connector-rounded-route` (off `feature/connector-editing`).
Design: `documentation/specs/2026-06-03-connector-rounded-route-design.md`.

A fourth `RouteStyle`: a smooth curve averaged from the bend points (rides the midpoints between
consecutive points; gentle S-curve when there are none). True vector (cubic) rendering.

## Steps

1. **Model** — `RouteStyle.cs`: add `Rounded = 3`.
2. **Geometry** — `ConnectorRoute.cs`: add `CubicSegment` record, `Cubics` property, `PolyCubic`
   factory (knot `Points` + `Start`/`End`Direction from first/last controls).
3. **Helper** — `RouteHelpers.cs`: add shared `SafeOutward` (used by the rounded router).
4. **Strategy** — new `RoundedRouter.cs`: anchor-aware endpoints; 0 points → outward-normal S-curve;
   ≥1 points → midpoint-quadratic smoothing converted to cubic segments.
5. **VM** — `ConnectorViewModel.cs`: `BuildLineGeometry` + `GetFlattenedPoints` handle `Cubics`;
   `SupportsWaypoints` includes `Rounded`.
6. **DI** — `ServiceCollectionExtensions.cs`: register `RoundedRouter`.
7. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows/macOS.

## Status

- [x] 1 Model · [x] 2 Geometry · [x] 3 Helper · [x] 4 Strategy · [x] 5 VM · [x] 6 DI · [x] 7 Build (clean)

Implemented; build clean (nullable-as-error). Pending: manual verification on Windows/macOS
(no GUI under WSL2).
