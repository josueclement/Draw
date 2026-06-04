# Shape stacking order (Z-order): Plan

Branch: `feature/shape-z-order`. Design: `documentation/specs/2026-06-04-shape-z-order-design.md`.

Lets the user change the front-to-back order of overlapping shapes via four actions — Bring to
Front / Bring Forward / Send Backward / Send to Back — on the Arrange ribbon tab, keyboard
shortcuts, and the canvas context menu. System boundaries always stay behind ordinary shapes;
connectors always stay on top (already structural — separate render layer).

## Steps

1. **Pure reorder helper** — `Draw.Diagramming/Layout/ZOrderArranger.cs` (new): `ZOrderOperation`
   enum + `Reorder<T>(ordered, isSelected, op)` returning the new back-to-front order. Group-aware
   forward/backward; front/back partition. Mirrors `ShapeArranger`.

2. **Live Z on the node VM** — `NodeViewModelBase.ZIndex` returns `Model.ZIndex` (was `0`) + add
   `RaiseZIndexChanged()`. Remove the `ZIndex => -1` override on `SystemBoundaryNodeViewModel`
   (banding enforces the constraint instead).

3. **Editor command** — `DiagramDocumentViewModel`: `RelayCommand<ZOrderOperation> OrderCommand`
   (CanExecute `HasNodeSelection`, registered in `RaiseSelectionChanged`) + `ReorderSelected(op)`:
   partition into boundary/ordinary bands, reorder each via `ZOrderArranger`, repack `ZIndex`
   `0..n-1`, `RaiseZIndexChanged()` per node; one `CaptureUndo()`; no-op short-circuits before
   capture.

4. **Hit-test follows Z** — `DiagramView.HitTestNode` selects the containing node with the highest
   `ZIndex` (`MaxBy`) instead of last-in-collection.

5. **Surfaces** — `MainWindow.axaml`: `Order` ribbon group (4 buttons) + 4 `KeyBindings`
   (`Ctrl+[`/`]`, `Ctrl+Shift+[`/`]`). `DiagramView.axaml.cs` `BuildArrangeMenu`: `Order` submenu.
   `ToolIcons.axaml`: 4 glyphs. All bind to `ActiveDocument.OrderCommand` (no `ShellViewModel`
   change).

No `Draw.Model`/serialization change — `NodeBase.ZIndex` already persists and is cloned for undo.

## Verify

`dotnet build Draw.slnx` clean; manual run on Windows/macOS (WSL2 headless): each action via all
three surfaces; boundary never covers a shape; connectors stay on top; click selects front-most;
undo/redo + save/reopen preserve order; PNG export reflects order.
