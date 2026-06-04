# Mind-map quick-add (+ helper buttons) — Implementation plan

Branch: `feature/mindmap-quick-add`. Design: `documentation/specs/2026-06-04-mindmap-quick-add-design.md`.

Cross-cutting editor affordance: **+** buttons on a node's four sides that, in one click, create a
same-kind/style/size node a fixed gap beyond that edge and link it with a rounded association — a
single undo step, then the new node is selected and focused for inline typing. Buttons show on hover
and on single-selection, for shapes + UML nodes (not images/boundaries).

## Steps

1. **Undo-free creation cores** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: extract
   `CreateShapeNode`/`CreateClassNode`/`CreateUseCaseNode`/`CreateConnector` (no `CaptureUndo`, no
   select, no `MarkModified`; `CreateConnector` takes a `RouteStyle`) from the public `Add*` methods,
   which now wrap them. Public behaviour unchanged.

2. **Quick-add command + geometry** — same file: `QuickAddConnectedNode(source, direction)` (one
   `CaptureUndo`; rejects image/boundary sources; clones `source.Model.Style`; selects + edits the
   child), `CreateChildLike` (source VM type → matching core), `ComputeQuickAddBounds` (size = source,
   `QuickAddGap = 48`, grid-snap, then `NudgeToFreeSlot` via `Rect2D.IntersectsWith`). New enum
   `Draw.App/ViewModels/QuickAddDirection.cs`.

3. **Buttons: render + hover + hit-test** — `Draw.App/Views/DiagramView.axaml.cs`, mirroring the
   resize-handle pattern: `UpdateQuickAddButtons` draws four world-space `+` discs on the `Overlay`
   (folded into `UpdateHandles`, plus called on hover/tool changes); `QuickAddTarget` (hovered, else
   single-selected candidate; null while dragging or a tool is armed); `UpdateHover` + `PointerExited`
   with sticky-region tracking so the off-node buttons don't flicker; `HitTestQuickAdd` +
   a click branch in `OnPointerPressed` (after the resize handle, before tool/marquee).

4. **Focus on create** — same file: `TryFocusNodeEditor` posts at `DispatcherPriority.Loaded` to focus
   the new node's inline `TextBox` (reuses the `TryFocusMemberEditor` approach).

5. **Build** `dotnet build Draw.slnx` clean (nullable-as-error); then manual verification on Windows
   (no GUI under WSL2).

## Status

- [x] 1 Creation cores · [x] 2 Command + geometry · [x] 3 Buttons render/hover/hit-test ·
  [x] 4 Focus on create · [x] 5 Build (clean)

Implemented on `feature/mindmap-quick-add`; build is clean (0 warnings / 0 errors). Pending: manual
verification on Windows/macOS (no GUI under WSL2) — see the checklist below.

## Manual verification (Windows/macOS)

1. Add a rectangle, select it → four **+** buttons on the edges; hover a different unselected shape →
   its buttons appear too.
2. Click the right **+** → a same-kind/style/size node appears to the right with a rounded connector;
   it's selected and focused — type a label, it commits.
3. Repeat near other nodes → new nodes are nudged to avoid overlap.
4. `Ctrl+Z` once removes both node and connector (single undo step); `Ctrl+Y` restores both.
5. Custom-fill a shape, then quick-add → child inherits the fill.
6. Try a class node and a use-case node (clone kind; use-case auto-edits its text). Image nodes and
   system boundaries show **no** + buttons.
7. Zoom/pan → buttons stay correctly placed and sized.
