# Align & distribute selected shapes: Design

Status: approved 2026-06-04. Branch: `feature/align-distribute-shapes`.

A cross-cutting editor enhancement. Today shapes are positioned only by free drag (with grid snap);
there is no way to line up or evenly space a group. This adds the standard "Arrange" toolkit — six
alignment actions and two distribution actions — exposed through a new ribbon tab, keyboard
shortcuts, and a canvas context menu. It reuses the existing multi-selection, the whole-document
memento undo, and the automatic connector re-routing.

## 1. Goals / non-goals

In scope:

- **Align** the selected shapes to the selection's bounding box: left / center-horizontal / right
  (X axis) and top / center-vertical / bottom (Y axis). Needs ≥2 selected.
- **Distribute** the selected shapes so the edge-to-edge gaps are equal along an axis (horizontal or
  vertical). The two outermost shapes stay put. Needs ≥3 selected.
- Three entry points: a new **Arrange** ribbon tab (Align dropdown + two Distribute buttons),
  **keyboard shortcuts** (`Ctrl+Shift+L/C/R/T/M/B` align, `Ctrl+Shift+H/V` distribute), and a
  **right-click context menu** on the canvas.
- One undo step per action. Connectors re-route automatically.

Out of scope (noted as follow-ups):

- "Align to canvas / page" and align-to-a-primary/anchor-shape modes (the selection model has no
  ordering, so there is no natural anchor).
- Re-snapping results to the grid — alignment/distribution positions are applied **exactly** so
  centers stay pixel-perfect (centering can legitimately produce off-grid coordinates).
- Adding other actions (Delete, etc.) to the new context menu, and right-click auto-selecting the
  node under the cursor.

## 2. Reference semantics

- **Alignment** is relative to the union bounding box of the whole selection. Left → `X = box.Left`;
  Right → `X = box.Right − width`; CenterHorizontal → `X = box.Center.X − width/2`; Top/Bottom/
  CenterVertical are the Y-axis analogues. Only one coordinate changes; sizes are preserved.
- **Distribution** orders the shapes by leading edge, keeps the first and last fixed, and repositions
  the inner ones so every gap equals `((lastTrail − firstLead) − Σ extents) / (n − 1)`. Gaps may be
  negative when shapes overlap — the spacing is still made uniform. Operates on nodes only
  (connectors are not part of multi-selection); a `SystemBoundaryNode` is treated as a normal node.

## 3. Layer placement

- The math is pure and UI-agnostic, so it lives in `Draw.Diagramming` (`Layout/ShapeArranger.cs`),
  beside `Routing` and `SnapExtensions`. It takes `IReadOnlyList<Rect2D>` and returns new positions
  in input order; callers map back by index.
- `DiagramDocumentViewModel` owns the operations and the commands — like the existing zoom commands.
  The ribbon, key bindings, and context menu all reference `ActiveDocument.AlignCommand` /
  `DistributeCommand`; no `ShellViewModel` change is needed.

## 4. Interaction notes

- Enable/disable: the Align dropdown binds `IsEnabled` to `CanAlignSelection` (≥2); the Distribute
  buttons + all parameterized commands gate on `CanDistributeSelection` (≥3) via `CanExecute`.
- The canvas had no context menu (right-click pans). To add one without losing right-drag pan: a
  right **click** (movement below a small threshold) with ≥2 nodes selected opens the menu; a right
  **drag** still pans; the middle button never opens the menu.
