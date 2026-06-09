# Align to reference: Design

Status: approved 2026-06-09. Branch: `feature/align-to-reference`.

A cross-cutting editor enhancement and the deferred follow-up to **Align & distribute shapes**
(`2026-06-04-align-distribute-shapes`). Today every alignment action moves *all* selected shapes
to their common bounding box, so there is no way to line one group up against another that should
stay put. This adds **relative alignment**: capture a set of shapes as a fixed *reference*, then
line a later selection (the *movers*) up against the reference's combined bounding box. It reuses
the existing multi-selection, whole-document memento undo, automatic connector re-routing, and the
`AlignmentMode` enum + arrange pattern.

## 1. Goals / non-goals

In scope:

- **Capture a reference**: "Set as reference" remembers the current selection as the fixed anchor
  group (≥1 shape; a single shape works and behaves like an anchor). Capturing clears the live
  selection so the next pick is the movers; the reference shows a distinct highlight + a banner.
- **Align to reference** (the six existing modes — left / center-h / right / top / center-v /
  bottom) lines the **movers** (the selection minus any reference shapes) up against the
  **reference's union bounding box**. Reference shapes never move.
- **Move as a block**: when there are multiple movers they keep their relative layout and shift by
  a single offset so the movers' union box lines up; they do not collapse onto one line.
- **Sticky** reference: it persists so several mover-sets can be aligned to the same reference in
  turn. Cleared on **Esc**, a **Clear reference** action, a **bare click on empty canvas**, **setting
  a new reference**, and when a reference shape is **deleted** (or removed by undo/redo).
- **Separate** from the existing Align/Distribute actions — those are unchanged. New surfaces: a
  **Reference** group on the **Arrange** ribbon tab (*Set as reference* button, *Align to reference*
  dropdown, *Clear reference* button) and the same three entries on the **canvas context menu**.
- One undo step per align; connectors re-route automatically; positions applied exactly (not
  re-snapped to the grid), matching the existing align/distribute.

Out of scope (follow-ups):

- **Keyboard shortcuts** for *Align to reference* (the existing `Ctrl+Shift+…` align shortcuts keep
  their same-selection meaning; *Esc-clears-reference* is wired, but no new chord is added).
- **Distribute to reference** (spacing movers relative to the reference) — the chosen scope is the
  six align actions only.
- Persisting the reference to the `.draw` file — it is transient, per-tab UI state.

## 2. Reference semantics

- **Reference set** = a transient, non-serialized `HashSet<Guid>` of node ids on
  `DiagramDocumentViewModel` (per-tab, like undo). It is **not** node selection — a node can be a
  reference whether or not it is selected. Stale ids (node deleted / gone after undo/redo) are pruned
  whenever the selection-changed refresh runs, so `HasReference` always reflects live shapes.
- **Movers** = `SelectedNodes` **minus** any reference nodes. So selecting a reference shape never
  moves it; only genuine non-reference selection moves.
- **Alignment** is relative to the **union bounding box of the reference shapes**. The movers'
  union box is shifted by a single `(dx, dy)` so the chosen line meets the reference box's same
  line: Left → `box.Left → ref.Left`; CenterHorizontal → centers' X; Right → right edges; Top /
  CenterVertical / Bottom are the Y-axis analogues. Only one axis changes; sizes are preserved;
  every mover translates by the same offset (block move).
- **Enable/disable**: *Set as reference* needs ≥1 selected node (`CanSetReference`). *Align to
  reference* needs a captured reference **and** ≥1 mover (`CanAlignToReference`). *Clear reference*
  is enabled whenever a reference exists.

## 3. Layer placement

- The math is pure and UI-agnostic, so it lives beside `Align`/`Distribute` in
  `Draw.Diagramming/Layout/ShapeArranger.cs`: a new
  `AlignToReference(IReadOnlyList<Rect2D> movers, Rect2D reference, AlignmentMode mode)` that returns
  the movers' new positions in input order (single-delta block shift). Reuses the existing
  `AlignmentMode` enum.
- `DiagramDocumentViewModel` owns the reference state + the three commands (`SetReferenceCommand`,
  `ClearReferenceCommand`, `AlignToReferenceCommand`) and the gating `Can*`/`HasReference`/
  `ReferenceStatusText` properties, mirroring the existing `AlignCommand`/`ArrangeSelected` flow.
  The single `RaiseSelectionChanged()` refresh hub is extended to notify the new properties +
  commands and to prune stale reference ids. No `ShellViewModel` change is needed.

## 4. Interaction notes

- **Visual**: reference shapes get a dashed **amber** outline drawn on the world-space `Overlay`
  canvas — the same code-behind pattern as the blue selection handles/marquee, zoom-scaled — folded
  into `UpdateHandles()` so it stays in sync on zoom/pan/selection. Amber distinguishes the fixed
  reference from the blue selection accent. A thin **banner** strip at the top of the canvas (visible
  only while a reference is active) shows the reference count + hint + a *Clear reference* button,
  bound to the document VM.
- **Capture clears selection**: after *Set as reference* the live selection is cleared so the next
  pick is the movers; the reference keeps its outline independently.
- **Esc**: cleared via the window-level key handler (Escape is consumed there for `tool.select`),
  delegating to `ActiveDocument.ClearReference()`; the select tool still activates as before.
- **Empty-canvas click**: a *bare* left click on empty canvas (movement below the existing
  context-click threshold, non-additive) clears the reference. A marquee **drag** to select movers
  does not — so selecting the movers by rubber-band still works.
- **Context menu**: the arrange menu (built in code) opens with ≥1 shape selected; it gains *Set as
  reference*, an *Align to reference ▸* submenu (six modes), and *Clear reference*, which
  enable/disable via their commands' `CanExecute`.

## 5. Deviation from the initial spec (noted for review)

The initial agreed spec listed "switching tabs" as a clear trigger. Reference state is **per-tab**
(stored on each document VM), so it never bleeds across tabs and naturally stays with its tab when
you switch away and back; it is cleared when the tab/document is closed. We do **not** actively wipe
it on tab switch — that would need cross-cutting shell wiring for no real benefit. This is the
narrowest reasonable interpretation of "reference is per-tab".
