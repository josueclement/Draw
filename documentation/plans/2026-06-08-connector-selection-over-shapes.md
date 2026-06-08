# Connector selection over shapes — Implementation plan

Branch: `fix/connector-selection-over-shapes`.

Bug fix: a connector that crosses over (or under) a node could not be selected anywhere it
overlapped that node's bounding box. Severe for use-case diagrams, where association/include/extend
connectors routinely run *entirely inside* a system boundary and were impossible to click.

## Root cause

In `OnPointerPressed` (`Draw.App/Views/DiagramView.axaml.cs`), select-mode hit-testing was
**node-first**: `HitTestNode(world)` ran first and `HitTestConnector(...)` only ran in the `else`
branch (no node hit). `HitTestNode` is a rectangular `n.Model.Bounds.Contains(p)` test, so any node
with a filled/bordered body (shapes, class/entity nodes, and especially system boundaries) "contains"
every interior point and always won — the connector hit-test was never reached. The connector
hit-test itself was already correct (`DiagramDocumentViewModel.HitTestConnector` →
`ConnectorDistance`/`DistanceToSegment`, 6px tolerance); it was just gated.

Connectors render in a layer declared after (on top of) the nodes layer in `DiagramView.axaml`, so
preferring the connector on click matches what is visually on top.

## Decision

Connector wins within tolerance: if the click is within the existing ~6px connector tolerance of a
connector line, select the connector; otherwise select the shape. Selection fix only — no
hover/cursor feedback, no geometry-accurate node hit-testing, no XAML/layer/Z-order changes.

## Steps

1. **View** — `Draw.App/Views/DiagramView.axaml.cs`, `OnPointerPressed`: restructure the select-mode
   branch from node-first to **connector-first**. Test `HitTestConnector` first (incl. the existing
   `Ctrl`+click waypoint-split path); fall back to node selection + `DragMode.Move`; then marquee.
   Reordering only — no helper logic changed. The already-selected-connector handle-precedence branch
   that runs earlier is untouched.

2. **Build** `dotnet build Draw.slnx` clean (nullable-as-error); then manual verification on
   Windows/macOS (no GUI under WSL2; no test framework in this repo).

## Out of scope (follow-ups)

- `HitTestNode` uses rectangular bounds, not geometry, so clicking the empty corner of an
  ellipse/use-case or rounded shape still selects it (node-vs-node only).
- Hover cursor affordance for connectors.

## Status

- [x] 1 View  · [x] 2 Build (clean)

Implemented on `fix/connector-selection-over-shapes`; build is clean (nullable-as-error). Pending:
manual verification on Windows/macOS (no GUI under WSL2).
