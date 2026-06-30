# Space connections — anti-crossing order — Implementation plan

Branch: `feature-connection-spacing-anti-crossing`. A refinement of
`documentation/plans/2026-06-04-connection-point-spacing.md` /
`documentation/plans/2026-06-04-force-pin-on-arrange.md`.

**Space connections** spreads the connectors touching the selected shape(s) evenly along each edge
they land on. Until now it **preserved each connector's current position order** along the edge: the
pure planner `ConnectionDistributor.PlanPinning` grouped ends by `(node, side)` and sorted each group
by `FractionAlong` — where the connector *currently* sits on that edge. That order comes from each
end's existing anchor (or its ray-cast attachment), **not** from where the connected shape actually
is, so a connector leaving the top of a side toward a low shape and one leaving the bottom toward a
high shape kept their crossed order after spacing — they visibly crossed.

Now each side is ordered by the **position of the shape at the connector's far end**, so the
attachment slots run in the same order as the connected shapes and the connectors stop crossing. This
is crossing-minimising for straight connectors fanning from one edge to several shapes (matching
points-on-a-line to targets in sorted order). Scope is deliberately narrow: connectors keep whichever
side they currently land on (no side migration), and only the existing manual command is affected (no
auto-respace on move). **Merge connections** is unchanged — it collapses every end to the side
midpoint, so order is irrelevant.

## Design

The ordering needs the far-end location inside the pure planner, which it didn't have, so one field
is threaded through `PinningEnd`. Everything else (grouping, even-spacing, the no-op/undo contract) is
unchanged.

- **Order reference** = the **other shape's centre** — stable, and independent of the other end's own
  anchor (no circularity when both shapes of a connector are selected, since centres don't move when
  anchors are re-pinned).
- **Order key** along a side = the coordinate that varies on that side: `OtherEnd.Y` for the vertical
  Left/Right edges, `OtherEnd.X` for the horizontal Top/Bottom edges. Sorted ascending; `EvenAnchor`
  puts slot index 0 at the top (L/R) / left (T/B), so ascending key → ascending slot.
- **Tiebreak** = current `FractionAlong`, so connectors to the *same* far shape (equal centre) keep
  their current relative order.

## Steps

1. **`Draw.Diagramming/Layout/ConnectionDistributor.cs`**:
   - Added `Point2D OtherEnd` to the `PinningEnd<TEnd>` record (after `RoutePoint`, before the nullable
     `CurrentAnchor`).
   - New private `CrossingOrderKey(BoxSide side, Point2D otherEnd)` → `Y` for Left/Right, `X` for
     Top/Bottom.
   - `PlanPinning` now sorts each `(node, side)` group by `CrossingOrderKey(OtherEnd)`, tiebreaking by
     the existing `FractionAlong`, instead of by `FractionAlong` alone.

2. **`Draw.App/ViewModels/ConnectorSpacingCoordinator.cs`**: each `PinningEnd` now carries the far
   shape's centre — source end → `connector.Target.Bounds.Center`, target end →
   `connector.Source.Bounds.Center` (read up front with the rest of the route snapshot).

3. **`tests/Draw.Diagramming.Tests/ConnectionDistributorTests.cs`**: `End` helper + the direct
   `PinningEnd` constructions take an `OtherEnd`; `PlanPinning_SpreadsEndsOnSameSide_InFractionOrder`
   became `PlanPinning_OrdersEndsByFarShapePosition_NotCurrentPosition` (ends whose current order is
   the reverse of their far shapes reorder to follow the far shapes); added
   `PlanPinning_EqualFarShapePosition_KeepsCurrentOrder` (tiebreak).

4. **Build** `dotnet build Draw.slnx` clean (0 warnings, nullable-as-error); **test**
   `dotnet test --solution Draw.slnx` green.

No model/serialization change; no public-API change beyond the added `PinningEnd` field; reuses the
forced-anchor mechanism and the single-undo contract.

## Status

- [x] 1 Planner ordering · [x] 2 Coordinator far-end · [x] 3 Tests · [x] 4 Build + test (clean, 400 tests green)

Implemented on `feature-connection-spacing-anti-crossing`; build clean (nullable-as-error), 400 tests
pass. Pending: manual verification on Windows/macOS (no GUI under WSL2):
1. Place a shape with ≥3 connectors to other shapes positioned so the natural order crosses (e.g. the
   ends currently run top→bottom but their far shapes run bottom→top). **Space connections** → the
   connectors fan out without crossing, following the far shapes' order.
2. Connectors to the *same* far shape keep their current relative order (tiebreak).
3. **Merge connections** still collapses every end on a side to the midpoint (unchanged).
4. One **Undo** after a Space returns every end to its pre-space anchor in a single step.
5. Re-running Space with nothing to change adds no extra undo step (no-op contract intact).
