# Align to reference: Implementation plan

Spec: `documentation/specs/2026-06-09-align-to-reference-design.md`. Branch:
`feature/align-to-reference`.

Relative alignment: capture a fixed *reference* set, then line the *movers* (selection minus
reference) up against the reference's union box, moving them as a block. Purely additive — existing
Align/Distribute untouched.

## Files to touch (in order)

1. **`src/Draw.Diagramming/Layout/ShapeArranger.cs`** — add pure
   `AlignToReference(IReadOnlyList<Rect2D> movers, Rect2D reference, AlignmentMode mode)`: compute
   movers' union box, derive a single `(dx, dy)` so the chosen line meets `reference`'s same line,
   `Translate` every mover by it. No-op (returns input) when there are no movers. Reuses
   `AlignmentMode`; mirrors the existing `Align` switch.

2. **`src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`** — the hub:
   - Field `private readonly HashSet<Guid> _referenceIds = new();` (transient, per-tab).
   - Properties: `ReferenceNodes` (live nodes whose id ∈ set), `HasReference` (`ReferenceNodes.Any()`),
     private `MoverNodes` (`SelectedNodes` minus reference), `CanSetReference` (`HasNodeSelection`),
     `CanAlignToReference` (`HasReference && MoverNodes.Any()`), `ReferenceStatusText` (banner text).
   - Commands (declared with the others, **constructed before `RebuildNodes()`**):
     `SetReferenceCommand`, `ClearReferenceCommand`, `AlignToReferenceCommand` (`RelayCommand<AlignmentMode>`).
   - Methods: `SetReference()` (capture selection ids → clear set → repopulate → `ClearSelection()`
     which refreshes), `ClearReference()` (clear set + `RaiseSelectionChanged()`),
     `AlignSelectedToReference(AlignmentMode)` (mirror `ArrangeSelected`: guard movers/reference
     non-empty → `CaptureUndo` → `ShapeArranger.AlignToReference` over mover bounds + reference union
     box → apply X/Y → `MarkModified` → `RaiseSelectionChanged`).
   - Extend `RaiseSelectionChanged()`: prune stale reference ids at the top; add `OnPropertyChanged`
     for `HasReference`/`CanSetReference`/`CanAlignToReference`/`ReferenceStatusText` and
     `NotifyCanExecuteChanged()` for the three new commands.

3. **`src/Draw.App/Views/DiagramView.axaml`** — add a top banner strip in the canvas grid cell
   (sibling after the `Viewport` border, `VerticalAlignment=Top`), `IsVisible="{Binding HasReference}"`,
   with an amber dot, `{Binding ReferenceStatusText}`, and a *Clear reference* button bound to
   `ClearReferenceCommand`.

4. **`src/Draw.App/Views/DiagramView.axaml.cs`** —
   - `_referenceOutlines` list + `ReferenceAccentColor/Brush` (`#F2A93B`).
   - `UpdateReferenceOutlines()`: dashed amber rectangle just outside each `_vm.ReferenceNodes`
     bound, zoom-scaled (mirror `UpdateHandles`/`StartMarquee`); call it at the end of
     `UpdateHandles()` so it always tracks zoom/pan/selection.
   - `BuildArrangeMenu`: append a separator + *Set as reference* (`SetReferenceCommand`), *Align to
     reference ▸* submenu (six `AlignToReferenceCommand` items), *Clear reference*
     (`ClearReferenceCommand`).
   - `_marqueeStartScreen` field set in the marquee press branch; in `OnPointerReleased`'s
     `DragMode.Marquee` case, clear the reference on a bare non-additive click (movement ≤
     `ContextClickThresholdSquared`).

5. **`src/Draw.App/Views/MainWindow.axaml`** — new `Reference` RibbonGroup in the Arrange tab (after
   Align): *Set as reference* button (`SetReferenceCommand`), *Align to reference* dropdown
   (`x:Name="AlignToReferenceDropDown"`, `IsEnabled="{Binding ActiveDocument.CanAlignToReference}"`,
   six `RibbonMenuItem`s with `{x:Static layout:AlignmentMode.*}`), *Clear reference* button
   (`ClearReferenceCommand`).

6. **`src/Draw.App/Views/MainWindow.axaml.cs`** — `WireAlignToReferenceDropdown()` (mirror
   `WireAlignDropdown`, calling `AlignSelectedToReference`), called from the constructor; and in
   `OnGlobalKeyDown`, after the text-focus guard, clear the active document's reference on `Escape`.

## Order of operations

Geometry (1) → VM contract (2) → views/UI (3–6). The VM is the contract every UI piece binds to, so
it lands before the ribbon/menu/banner. Build after each cluster.

## Risks / watch-outs

- **Nullable-as-error / no `var` / no source generators / explicit usings** — match the surrounding
  code exactly.
- **Command construction order** — the three new commands must exist before `RebuildNodes()` runs
  (it calls `RaiseSelectionChanged()` → `NotifyCanExecuteChanged()`).
- **Compiled bindings** — banner binds `HasReference`/`ReferenceStatusText`/`ClearReferenceCommand`;
  they must be public with change notification (raised in `RaiseSelectionChanged`).
- **Don't remove `ClipToBounds="False"`** on the node/connector layers (Avalonia 12 trap); the banner
  is a new sibling, not a change to those.
- **Overlay outlines** live under the `World` transform (world coords, `1/Zoom` stroke), same as
  handles — keep them in sync by calling from `UpdateHandles()`.
- **Phosphor icon names** (`push_pin`, `x`) must be valid `Icon` enum members — the build verifies.

## Verification

No automated tests in this repo (removed 2026-06-03). Verify by `dotnet build Draw.slnx` (nullable
errors fail the build) + an adversarial multi-lens review, then manual run on Windows (WSL2 is
headless): set 3 stacked rects as reference, align a loose rect's V-center → it lands on the column's
middle; 2-rect reference → loose rect lands on the gap midpoint; multiple movers move as a block;
reference never moves; sticky across several aligns; Esc / Clear / bare empty-click / delete clear it;
one undo step per align; existing Align/Distribute unchanged.
