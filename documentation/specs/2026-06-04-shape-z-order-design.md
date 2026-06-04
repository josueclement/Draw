# Shape stacking order (Z-order): Design

Status: approved 2026-06-04. Branch: `feature/shape-z-order`.

A cross-cutting editor enhancement. Today the front-to-back order of overlapping shapes is fixed at
creation time (newest on top) and the user cannot change it. This adds the standard stacking
toolkit — **Bring to Front / Bring Forward / Send Backward / Send to Back** — acting on the
selection, exposed through the Arrange ribbon tab, keyboard shortcuts, and the existing canvas
context menu. It reuses the existing multi-selection and whole-document memento undo.

## 1. Goals / non-goals

In scope:

- Four stacking operations on the selected shape(s): jump to the very front/back, or move one level
  forward/backward. Multi-selection keeps the selected shapes' relative order; a contiguous block
  moves as a unit.
- Three entry points mirroring Align/Distribute: an **Order** group on the **Arrange** ribbon tab,
  **keyboard shortcuts** (`Ctrl+]` / `Ctrl+[` forward/backward, `Ctrl+Shift+]` / `Ctrl+Shift+[`
  to front/back), and the right-click canvas **context menu** ("Order" submenu).
- One undo step per action; a pure no-op (e.g. the selection is already frontmost) makes no change
  and adds no undo entry.

Hard constraints (from the request):

- **System boundaries always render behind every other shape.** Enforced structurally: boundaries
  and ordinary nodes occupy two disjoint `ZIndex` bands, repacked on every reorder.
- **Connectors always render on top.** Already true — connectors are a separate `ItemsControl`
  layer above the whole nodes layer (`DiagramView.axaml`), so no shape reorder can affect them. No
  connector work is done; connectors are not individually orderable.

Out of scope (noted as follow-ups):

- Per-connector stacking (connectors have no `ZIndex` and live in their own layer).
- A numeric Z field in the Inspector (relative buttons only; the band numbers are an internal
  detail).

## 2. Reference semantics

Nodes are split into two bands ordered back-to-front by `ZIndex`: **boundaries** then **ordinary**
shapes. Each band is reordered independently, then the whole set is repacked into contiguous
`ZIndex` values `0..n-1` (boundaries `0..b-1`, ordinary `b..n-1`). Because every boundary index is
`< b ≤` every ordinary index, a boundary can be re-stacked relative to other boundaries but can
never rise above an ordinary shape, whichever button is pressed.

Within a band (`ZOrderArranger.Reorder`, index 0 = backmost), given the selected positions:

- **BringToFront** — non-selected (in order) then selected (in order).
- **SendToBack** — selected (in order) then non-selected (in order).
- **BringForward** — scan front→back; swap each selected item past a non-selected neighbour ahead of
  it. A contiguous selected block advances exactly one level as a unit.
- **SendBackward** — the mirror image.

## 3. Architecture

`NodeBase.ZIndex` already exists, is serialized, and is deep-copied by the memento clone — so
persistence to `.draw` and undo/redo of Z-order come for free. **No model or serialization change.**

- **`Draw.Diagramming/Layout/ZOrderArranger.cs`** (new) — pure, UI-agnostic list reordering plus the
  `ZOrderOperation` enum (same namespace as `AlignmentMode`/`DistributionMode`). Mirrors
  `ShapeArranger`.
- **`NodeViewModelBase.ZIndex`** now returns `Model.ZIndex` (was a constant `0`) and exposes
  `RaiseZIndexChanged()`.
- **Render binding fix** — the node container's stacking was bound with
  `<Setter Property="Canvas.ZIndex" .../>`, but **Avalonia has no `Canvas.ZIndex` attached
  property** (Z-order is `Visual.ZIndex`; WPF's `Panel.ZIndex` has no Avalonia equivalent). That
  setter silently no-op'd, so stacking was actually driven entirely by node-collection order
  (boundaries `Insert(0)`, new shapes appended on top). Changed to `<Setter Property="ZIndex" .../>`
  so the real `Visual.ZIndex` is bound.
- **`SystemBoundaryNodeViewModel`** drops its `ZIndex => -1` override — the banding now enforces the
  constraint, and distinct indices let boundaries be reordered among themselves.
- **`DiagramDocumentViewModel`** gains `RelayCommand<ZOrderOperation> OrderCommand` (CanExecute =
  `HasNodeSelection`) and `ReorderSelected(op)`: partition → reorder each band → repack `ZIndex` →
  **reorder the `Nodes` collection in place (`Move`)** to match, so the `ItemsControl` restacks
  (render order follows collection order, and the bound `Visual.ZIndex` agrees) → `RaiseZIndexChanged()`
  → `MarkModified()`, all inside one `CaptureUndo()`. The in-place `Move` preserves the view-model
  instances (selection, decoded image bitmaps).
- **`DiagramView.HitTestNode`** now returns the containing node with the **highest `ZIndex`**
  (`MaxBy`) instead of the last in collection order, so click selection follows the visible
  stacking. For never-reordered diagrams this is the same node as before (no regression).
- **Surfaces** — ribbon `Order` group, `Window.KeyBindings`, and an `Order` submenu added to the
  code-built `BuildArrangeMenu`; all bind straight to `ActiveDocument.OrderCommand` (no
  `ShellViewModel` changes), exactly like Distribute. Four glyphs added to `ToolIcons.axaml`.

## 4. Verification

`dotnet build Draw.slnx` clean (nullable warnings are build errors). Manual on Windows/macOS
(WSL2 is headless): overlap shapes and exercise each operation via all three surfaces; confirm a
boundary never covers a shape (even when a shape is sent to back or a boundary brought to front) and
two boundaries can be reordered behind the shapes; confirm connectors stay on top; click an overlap
selects the front-most shape; undo/redo and save→reopen preserve order; PNG export reflects the
chosen order.
