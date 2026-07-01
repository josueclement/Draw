# Auto-space connectors on connect & duplicate — Implementation plan

Branch: `feature/auto-space-connections`. Delivers the "continuous/automatic re-spacing as connectors
are added" follow-up named out-of-scope in
`documentation/specs/2026-06-04-connection-point-spacing-design.md`, building on
`documentation/plans/2026-06-30-connection-spacing-anti-crossing.md`.

Until now a new connector pinned **both ends to the midpoint of the side they attach to**
(`DiagramDocumentViewModel.LinkNodesCore` → `ConnectionDistributor.EvenAnchor(side, 0, 1)`), so every
connector added to the same side of a shape stacked on the same point ("merged by default"), and
duplicating/pasting shapes preserved those stacked anchors. **Space connections** existed only as a
manual command. Now connectors spread out automatically, controlled by a new editor option
(default on).

## Decisions

- **Setting `EditorOptions.AutoSpaceConnectors`, default on** — off restores the previous
  merge-on-midpoint behavior for both flows. No in-app toggle UI (none exists); tune via `appsettings.json`.
- **Connect = non-destructive free-slot.** The *new* connector's end takes a free position on its side;
  the ends already there are **not** moved. (Matches "only place the new connector".)
- **Duplicate & paste = full re-space.** Clones arrive stacked on the originals' anchors, so the
  affected shapes — the clones **and** any non-selected neighbour a boundary connector reconnects to —
  are re-spaced evenly, reusing the existing `ConnectionDistributor`/`PlanPinning` planner.
- **Mind-map "+" button = full re-space.** `CreateChildNode` re-spaces the parent's branches evenly
  after adding the child (like duplicate/paste), so repeatedly adding children keeps them fanned out.
  The tool owns mind-map layout, so there is nothing hand-tuned to preserve — full re-space, not
  free-slot. (Hand-*drawn* mind-map branches via the connect gesture still free-slot, like any connector.)

The three flows use different algorithms by design: the connect gesture must not disturb a hand-tuned
layout (free-slot); duplicate/paste and the mind-map "+" button start from a stacked/tool-owned layout
that a full re-space fixes.

## Design

- **Free-slot geometry (pure, Diagramming):** `ConnectionDistributor.FreeSlotAnchor(BoxSide, occupied)`
  returns the `(u,v)` anchor at the midpoint of the **widest gap** between the already-occupied
  along-side fractions, with the corners `0`/`1` as walls. Empty → `0.5` (so the first connector still
  centres and a rounded route still bows). Ties favour the gap nearer the `0` corner; occupants are
  clamped to `[0,1]`. Reuses the same side→`(u,v)` mapping as `EvenAnchor`.
- **Connect (`DiagramDocumentViewModel.LinkNodesCore`):** on the hand-drawn (null-anchor) path only,
  when the option is on, classify each end's side from the initial auto-route, gather the occupied
  fractions of every **other** connector end on that shape+side (`OccupiedFractions` helper, reading
  current `RouteStart`/`RouteEnd`), then pin both ends via `FreeSlotAnchor`. Both sides + occupied lists
  are read **before** pinning either end (a sibling's fraction is unaffected by the new end, and it keeps
  the source/target pins independent). The explicit-anchor path (mind-map "+") skips free-slot entirely —
  `CreateChildNode` re-spaces afterwards. Undo is unchanged — `AddConnector`/`CreateChildNode` already
  snapshot before `LinkNodesCore`.
- **Mind-map "+" (`DiagramDocumentViewModel.CreateChildNode`):** after linking the child and refreshing
  branch depths, call `AutoSpaceConnectorsForShapes([parent.Id, child.Id])` (folds into the method's
  existing undo snapshot; no-op when the option is off). Only the parent side redistributes (the child
  has one branch); ordering follows the children's positions via the anti-cross planner.
- **Re-space for a shape set (`ConnectorSpacingCoordinator`):** the private `PinSelectedConnectionEnds`
  became `PinConnectionEnds(HashSet<Guid> ids, anchorFor, bool captureUndo)`; `SpaceSelectedConnections`
  /`MergeSelectedConnections` call it with the selection and `captureUndo: true` (unchanged). New public
  `SpaceConnectionsForShapes(shapeIds, captureUndo)` runs the even-spacing planner over an explicit set.
- **Seam + duplicate/paste (`IDocumentEditContext` / `ClipboardCoordinator`):** new
  `IDocumentEditContext.AutoSpaceConnectorsForShapes(shapeIds)`, implemented by the view model as a
  no-op when the option is off, else `SpaceConnectionsForShapes(..., captureUndo: false)` (folds into the
  clone's existing undo snapshot). `ClipboardCoordinator.PlaceClones` collects both endpoint ids of every
  cloned connector (covers clones + boundary neighbours) and calls it after `RebuildConnectors`. Paste's
  clipboard connectors are all internal, so "incl. neighbours" is naturally moot there.

No model/serialization change — this only computes and assigns existing `(u,v)` anchors.

## Steps

1. **`Draw.App/Configuration/EditorOptions.cs`** + **`Draw.App/appsettings.json`** — add
   `AutoSpaceConnectors` (default `true`).
2. **`Draw.Diagramming/Layout/ConnectionDistributor.cs`** — add pure `FreeSlotAnchor` (+ private
   `WidestGapMidpoint`/`AnchorAt`, the latter shared with `EvenAnchor`).
3. **`Draw.App/ViewModels/ConnectorSpacingCoordinator.cs`** — generalize to `PinConnectionEnds(ids, …,
   captureUndo)`; add `SpaceConnectionsForShapes`.
4. **`Draw.App/ViewModels/IDocumentEditContext.cs`** — add `AutoSpaceConnectorsForShapes`.
5. **`Draw.App/ViewModels/DiagramDocumentViewModel.cs`** — free-slot on the hand-drawn path in
   `LinkNodesCore` (+ `OccupiedFractions`/`AddFractionIfOnSide` helpers); full re-space of the parent's
   branches in `CreateChildNode` (the mind-map "+" button); implement `AutoSpaceConnectorsForShapes`.
6. **`Draw.App/ViewModels/ClipboardCoordinator.cs`** — auto-space in `PlaceClones`.
7. **`tests/Draw.Diagramming.Tests/ConnectionDistributorTests.cs`** — 5 `FreeSlotAnchor` cases (empty
   centres; single-midpoint → near-0 half; symmetric pair → middle gap; widest-gap not input order;
   clamp).
8. **Build** `dotnet build Draw.slnx` clean (0 warnings, nullable-as-error); **test**
   `dotnet test --solution Draw.slnx` green.

## Status

- [x] 1 Setting · [x] 2 FreeSlotAnchor · [x] 3 Coordinator · [x] 4 Seam · [x] 5 Connect flow ·
  [x] 6 Duplicate/paste · [x] 7 Tests · [x] 8 Build + test (clean, 420 tests green)

Implemented on `feature/auto-space-connections`; build clean (nullable-as-error), 420 tests pass.
Pending: manual verification on Windows/macOS (no GUI under WSL2):

1. **Connect:** a shape with 2 connectors on one side → draw a 3rd to it → the new one takes a free
   slot; the two existing don't move. The first connector on an empty side still centres/bows.
2. **Duplicate:** a shape with ≥2 connectors (incl. one to a non-selected neighbour) → Duplicate →
   clones fan out evenly and the neighbour's ends re-space; a single **Undo** reverts it all.
3. **Paste:** copy a small connected sub-graph → paste → its connectors arrive spread, not stacked.
4. **Mind-map:** add a 2nd child on the same parent side → the two parent-side branches fan out.
5. **Toggle off:** set `"AutoSpaceConnectors": false` → old merge-at-midpoint behavior returns for both
   connect and duplicate.
