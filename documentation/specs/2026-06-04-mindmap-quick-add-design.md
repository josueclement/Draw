# Mind-map quick-add (+ helper buttons): Design

Status: approved 2026-06-04. Branch: `feature/mindmap-quick-add`.

A cross-cutting editor affordance. Building a mind map today is slow: arm a shape tool, click to
place, arm the connector tool, drag source→target — repeated per idea. This adds small **+** buttons
on the four sides of a node that, in one click, create a new node in that direction **and** connect it
to the source. It introduces no document concepts: it reuses the existing node/connector model, the
creation APIs, the boundary-attachment router, and the whole-document memento undo.

## 1. Goals / non-goals

In scope:

- A **+** button on each of the four sides (up/down/left/right) of a node.
- Buttons appear on **hover** over a node and while a single node is **selected**.
- Clicking a button creates a new node a fixed gap beyond that edge, **cloning the source's kind,
  style and size**, and links the two with a plain **Association** connector using **`RouteStyle.Rounded`**.
- The whole gesture (node + connector) is **one undo step**; the new node is selected and, for
  label-bearing kinds, focused for immediate inline text entry.
- Placement **nudges** sideways to avoid overlapping an existing node.
- Applies to **shapes + UML nodes**: `ShapeNode`, `ClassNode`, `ActorNode`, `UseCaseNode`.

Out of scope (follow-ups):

- Keyboard accelerators (Tab = child, Enter = sibling).
- Even connector-anchor distribution when several children share one side (use
  `ConnectionDistributor.EvenAnchor`).
- Image nodes and system boundaries as quick-add sources.
- A configurable gap (exposed in `EditorOptions`).

## 2. Reference semantics

- **Child geometry.** The child inherits the source's `Width`/`Height` (uniform mind-map nodes). Its
  bounds are placed a fixed `QuickAddGap` (48 world units) beyond the source edge in the chosen
  direction, centred on the perpendicular axis, then grid-snapped when snap is on.
- **Overlap nudge.** If the candidate overlaps any other node (`Rect2D.IntersectsWith`; system
  boundaries excepted, as they are containers), it is shifted along the axis perpendicular to the
  growth direction in increasing steps, alternating sides (`0, +s, −s, +2s, −2s, …`, `s = perp extent
  + gap`), capped at a few tries; if none clears, it falls back to the base position.
- **Connector.** `RelationshipKind.Association`, `RouteStyle.Rounded`, with **automatic** anchors
  (`SourceAnchor`/`TargetAnchor` left null). Because the child sits directly beyond the chosen edge,
  the router's centre-to-centre ray-cast picks the correct edges, and the link stays correct when
  either node moves.
- **Clone vs fresh.** "Clone kind & style" means a *fresh* same-kind node (default name/text, no
  members) carrying a clone of the source's `ShapeStyle` — not a content copy. A shape stays the same
  `ShapeKind`; a class stays a `ClassNode` of the same `ClassNodeKind`; an actor/use-case stays its kind.

## 3. Layer placement

- **Creation (VM).** `DiagramDocumentViewModel.QuickAddConnectedNode(source, direction)` owns the
  one-shot command. The existing `AddShape` / `AddClassNode` / `AddUseCaseNode` / `AddConnector` each
  captured undo and selected their result, so composing two of them would produce two undo steps. Their
  bodies were extracted into undo-free, no-select **cores** (`CreateShapeNode`, `CreateClassNode`,
  `CreateUseCaseNode`, `CreateConnector` — the latter gains a `RouteStyle` parameter); the public
  methods now wrap a core with `CaptureUndo` + select + `MarkModified` (behaviour unchanged), and
  quick-add captures undo once and calls cores directly. Placement math (`ComputeQuickAddBounds`,
  `NudgeToFreeSlot`, `OverlapsExistingNode`) is private to the VM and reuses `Rect2D` +
  `SnapExtensions.PositionSnappedToGrid`. `QuickAddDirection` is a small enum in `Draw.App.ViewModels`.
- **Affordance (view).** `DiagramView` renders the buttons on the existing world-space `Overlay`
  canvas (scaled by `1/Zoom`), mirroring the resize-handle pattern (`UpdateHandles` /
  `HandlePositions` / `HitTestHandle`). The overlay is `IsHitTestVisible=false`; clicks are hit-tested
  in code in `OnPointerPressed`, before the marquee/tool branches (the buttons sit *outside* the node
  bounds, so `HitTestNode` would miss them).

## 4. Interaction notes

- **Target resolution** (`QuickAddTarget`): the hovered candidate when idle, else the single selected
  candidate; null while any drag is active or a placement/connector tool is armed. So the buttons are
  hidden during gestures and in connector/shape-placement modes.
- **Hover stickiness.** The buttons live outside the node, so naive hover tracking would make them
  vanish as the pointer crosses the gap onto a button. Hover stays on the current node while the
  pointer is within its bounds inflated by `gap + button size`, keeping the buttons reachable without
  flicker. `PointerExited` clears the hover.
- **Refresh.** `UpdateQuickAddButtons` runs from `UpdateHandles` (selection change, transform, drag,
  release), on hover changes, and on toolbox tool changes — kept on its own overlay list so hover
  refreshes don't rebuild the resize handles.
- **Post-create focus.** The new node's `IsEditing` is set in the VM; the view focuses the node's
  inline `TextBox` via `TryFocusNodeEditor`, posting at `DispatcherPriority.Loaded` so the freshly
  added container is realized first (the same approach as `TryFocusMemberEditor`). Class nodes have no
  single inline label (`HasInlineLabel` is false), so they are created selected but not auto-edited.
- **Precedence.** Quick-add is checked after the resize-handle hit-test (handles sit on the edge, the
  buttons further out) and before tool/marquee handling. A press on a button ends any active edit
  first (existing `EndEditing` at the top of `OnPointerPressed`), so rapid add-then-add commits the
  previous label.
