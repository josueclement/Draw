# Arrow-key nudge for the current selection: Implementation plan

Branch: `feature/arrow-key-nudge`. No separate design spec — small, self-contained.

## Problem

Shapes could only be moved by dragging with the mouse. There was no keyboard way to nudge a
selection — the standard fine-positioning gesture in every diagram tool.

## Decision

Arrow keys move the current selection:

- **Step size:** plain arrow = one grid cell (`GridSize`, default 10); **Shift+arrow = 1px** fine
  step.
- **Undo:** a contiguous run of nudges on the same selection **coalesces into one undo entry**
  (like a drag); changing the selection starts a new entry.
- **Scope:** selected **nodes** move together; if a **connector** is selected instead, **all its
  bend points** shift so the whole route moves as a unit.
- **No implicit grid snap on nudge** (unlike a drag release). The move is exactly the requested
  delta — shapes are already grid-aligned in practice, so a `GridSize` step preserves alignment,
  and this keeps Shift+1px meaningful even while snap-to-grid is on.

Handled in the existing canvas `DiagramView.OnKeyDown` (bubble phase). Arrow keys are unbound in
the keymap, so they pass through the window-level chord dispatcher (which is also suppressed while
a text-entry surface has focus) and bubble to the canvas. Not wired into the rebindable keymap —
see follow-up.

## Files touched

1. **`src/Draw.App/ViewModels/ConnectorViewModel.cs`** — add `MoveBendPointsBy(dx, dy)`: shifts
   every entry of `_model.BendPoints` by the delta (via `Point2D.Offset`) and `Recompute()`s once.
   Mirrors the existing `MoveBendPoint`.

2. **`src/Draw.App/Views/DiagramView.axaml.cs`**
   - `OnKeyDown`: on `Left/Right/Up/Down`, call `NudgeSelection(key, shift)` and mark handled
     (the existing `e.Source is TextBox` guard already prevents this while editing a label).
   - `NudgeSelection`: compute the delta, branch on `SelectedConnector` (with bend points) vs
     `SelectedNodes`, capture undo via `EnsureArrowNudgeUndo`, apply
     `MoveBendPointsBy` / `MoveSelectedBy`, then `MarkModified()` + `UpdateHandles()`.
   - `EnsureArrowNudgeUndo` + two fields (`_arrowNudgeUndoCaptured`, `_arrowNudgeSelection`):
     capture one snapshot per run; a selection change (compared as a set) or any pointer gesture
     (`OnPointerPressed` clears the flag) begins a new run.

Reuses `MoveSelectedBy`, `SelectedNodes`/`SelectedConnector`, `CaptureUndo`, `MarkModified`,
`GridSize` on `DiagramDocumentViewModel`, and `UpdateHandles`/`EnsureUndoCaptured` patterns already
in `DiagramView`. No changes to `Draw.Model` or the keymap/chord system.

## Notes / scope

- **Connector with no bend points → no-op** (it's anchored to its endpoints; move its shapes, or
  Ctrl+click the line to add a bend point first). Inherent to the model, not worked around.
- Nothing selected → no-op. Multi-select nodes move together; connectors re-route automatically.
- Negative coordinates are allowed (no clamping); scrollbars follow — unchanged.
- **Known minor edge:** a nudge immediately after an unrelated command that doesn't change the
  selection set or involve the pointer could merge into the prior undo entry. Acceptable for a
  nudge; the selection-set comparison covers the common re-selection cases (e.g. paste).
- **Follow-up (not done):** arrow nudge is hard-coded, not exposed in the rebindable
  `keymap.json`. Could be added to the keymap action registry later if rebinding is wanted.

## Verification

No automated tests in this repo (removed 2026-06-03). `dotnet build Draw.slnx` is the gate
(nullable warnings are build errors) — passes with 0 warnings. WSL2 is headless, so verify
visually on Windows:

- Select one shape → arrows move it one grid cell each press; Shift+arrow → 1px.
- Hold an arrow → continuous movement; a single Ctrl+Z reverts the whole burst.
- Select multiple shapes → all move together; connectors between them re-route live.
- Double-click to edit a label, press arrows → caret moves in text, shape does **not** move.
- Select a connector with bend points (Ctrl+click its line to add one) → arrows shift the whole
  route; Ctrl+Z reverts as one step.
- Change selection between nudges → separate undo entries.
