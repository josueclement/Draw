# Force-pin all connection points when arranging — Implementation plan

Branch: `feature/force-pin-connections`. Enhances `documentation/plans/2026-06-04-connection-point-spacing.md`.

Small behavior tweak to the existing **Space connections** action. Previously it pinned connector ends
only on bounding-box sides with ≥2 ends and left a lone end on a side automatic (hollow handle), so the
arranged layout was only half-locked. Now the action **force-pins every end touching the selected
shape(s)**: crowded sides are spread evenly (unchanged), and a side with a single end is **centred on
that edge**. The whole arrangement is locked and survives later moves/resizes.

The lone-end case is just the spreading formula at count 1: `EvenAnchor(side, 0, 1)` → fraction
`(0+1)/(1+1) = 0.5`, i.e. the middle of the edge. So both cases share one code path.

## Steps

1. **Expose current anchors** — `Draw.App/ViewModels/ConnectorViewModel.cs`: add read-only
   `SourceAnchor`/`TargetAnchor` (`ModelPoint?`) next to the existing `SourceAnchored`/`TargetAnchored`
   bools, so the document VM can compare against the stored anchor and skip true no-ops.

2. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs` `SpaceSelectedConnections`:
   restructure into read-all-targets then apply (a connector's route depends only on its own endpoints,
   so reading every `RouteStart`/`RouteEnd` up front is order-independent). For each `(node, side)` group,
   order by current fraction and target `EvenAnchor(side, i, count)` — which centres a lone end (count 1)
   and spreads several at equal gaps. Apply each op only when it differs from the current anchor;
   `CaptureUndo()` once on the first real change; `MarkModified()` only if something changed — so a no-op
   still adds no undo entry.

3. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows (no GUI under WSL2).

No UI change: same **Space connections** ribbon button / context-menu item; `CanSpaceConnections` (≥1
selected) is already correct.

## Status

- [x] 1 Expose anchors · [x] 2 Document VM · [x] 3 Build (clean, 0 warnings)

Implemented on `feature/force-pin-connections`; build clean (nullable-as-error). Pending: manual
verification on Windows (no GUI under WSL2):
1. 3 connectors on one side + 1 on another → select shape → **Space connections**: the 3 spread evenly
   *and* the lone end moves to the centre of its edge and becomes filled (pinned).
2. Re-run → no visual change and the undo count does not grow (true no-op).
3. Undo → 3 unspaced, lone end back where it was and hollow (automatic) again.
4. Move the shape → all pinned ends (incl. the centred lone one) keep their relative positions.
5. Alt+click the now-pinned lone endpoint → releases it back to automatic (existing reset still works).
