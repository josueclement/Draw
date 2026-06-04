# Align & distribute selected shapes — Implementation plan

Branch: `feature/align-distribute-shapes`. Design: `documentation/specs/2026-06-04-align-distribute-shapes-design.md`.

Cross-cutting editor enhancement: align selected shapes (left/center/right/top/middle/bottom) to the
selection bounding box, and evenly distribute them (equal edge-to-edge gaps) horizontally/vertically.
Exposed via a new Arrange ribbon tab, `Ctrl+Shift` shortcuts, and a right-click canvas menu.

## Steps

1. **Layout math** — `Draw.Diagramming/Layout/ShapeArranger.cs` (new): `AlignmentMode` /
   `DistributionMode` enums + pure `Align` / `Distribute` over `IReadOnlyList<Rect2D>`. Uses
   `Rect2D.Union` for the bounding box; returns new positions in input order. No-ops below the
   minimum count (2 align / 3 distribute).

2. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: `AlignCommand` /
   `DistributeCommand` (`RelayCommand<TMode>`), `CanAlignSelection` (≥2) / `CanDistributeSelection`
   (≥3), and `AlignSelected` / `DistributeSelected` (shared `ArrangeSelected`: `CaptureUndo()` →
   apply positions exactly, no snap → `MarkModified()`). Refresh the commands + flags in
   `RaiseSelectionChanged()`.

3. **Ribbon** — `Draw.App/Views/MainWindow.axaml`: `layout` xmlns; new **Arrange** tab between Insert
   and View — Align `RibbonDropDownButton` (6 items, `IsEnabled` ← `CanAlignSelection`) + two
   Distribute `RibbonButton`s. Eight `Ctrl+Shift` `KeyBinding`s → `ActiveDocument.*Command` with the
   mode as `CommandParameter`.

4. **Dropdown wiring** — `Draw.App/Views/MainWindow.axaml.cs`: `WireAlignDropdown` mirrors
   `WireDropdown` (RibbonMenuItem has no DataContext); a shared command resolves `ActiveDocument` at
   click time so it follows tab switches.

5. **Context menu** — `Draw.App/Views/DiagramView.axaml.cs`: record the right-press position; on
   release, a right-**click** (sub-threshold travel) with ≥2 selected opens a code-built menu bound to
   the VM commands; right-**drag** still pans. Menu built in code so items bind straight to the
   commands (correct enable/disable, no DataContext plumbing).

6. **Icons** — `Draw.App/Resources/ToolIcons.axaml`: custom `ToolIcon.DistributeHorizontal` /
   `…Vertical` (three even bars). Align icons reuse Phosphor (`align_left`, `align_center_vertical`, …).

7. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows (no GUI under WSL2).

## Status

- [x] 1 Layout math · [x] 2 Document VM · [x] 3 Ribbon + shortcuts · [x] 4 Dropdown wiring ·
  [x] 5 Context menu · [x] 6 Icons · [x] 7 Build (clean)

Implemented on `feature/align-distribute-shapes`; build is clean (nullable-as-error; only pre-existing
`Watermark`-obsolete warnings remain). Pending: manual verification on Windows (no GUI under WSL2) —
see the checklist in the design's interaction notes.
