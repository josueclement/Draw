# Merge connections (inverse of Space connections) — Implementation plan

Branch: `feature/merge-connections`. Inverse of
`documentation/plans/2026-06-04-connection-point-spacing.md` /
`documentation/plans/2026-06-04-force-pin-on-arrange.md`.

A new **Merge connections** button in the Arrange ribbon tab's **Connections** group (and a matching
right-click context-menu item). It force-pins every connector end touching the selected shape(s)
onto the **centre** of the bounding-box side it lands on — so every end on a given side collapses to
that edge's midpoint (they stack, by design). It's the exact inverse of **Space connections**
(which spreads those same ends evenly along each edge), so the two buttons fan out ↔ regroup a
shape's connectors. Enabled when ≥1 shape is selected (same as Space).

The merge differs from spacing by exactly one value: the target anchor is `EvenAnchor(side, 0, 1)`
(= 0.5, edge centre) for **every** end, instead of `EvenAnchor(side, i, count)`. All other logic is
shared, so the old `SpaceSelectedConnections` body was extracted into a helper parameterised by the
anchor selector.

## Steps

1. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`:
   - Extracted the collect/group/read-routes-first/apply/single-undo/skip-no-op body of
     `SpaceSelectedConnections` into `private void PinSelectedConnectionEnds(Func<BoxSide,int,int,Point2D> anchorFor)`.
   - `SpaceSelectedConnections()` now delegates via method group: `PinSelectedConnectionEnds(ConnectionDistributor.EvenAnchor)`.
   - New `MergeSelectedConnections() => PinSelectedConnectionEnds((side, _, _) => ConnectionDistributor.EvenAnchor(side, 0, 1))`.
   - Added `MergeConnectionsCommand` (RelayCommand, instantiated next to `SpaceConnectionsCommand`
     before `RebuildNodes()`), `CanMergeConnections => SelectedNodes.Any()`, and the matching
     `OnPropertyChanged`/`NotifyCanExecuteChanged` lines in `RaiseSelectionChanged()`.

2. **Ribbon** — `Draw.App/Views/MainWindow.axaml`: sibling `RibbonButton` "Merge connections" in the
   Connections group, bound to `ActiveDocument.MergeConnectionsCommand`, icon `ToolIcon.MergeConnections`.

3. **Context menu** — `Draw.App/Views/DiagramView.axaml.cs` `BuildArrangeMenu`: `MenuItem`
   "Merge connections" bound to `vm.MergeConnectionsCommand`, after the Space item.

4. **Icon** — `Draw.App/Resources/ToolIcons.axaml`: new `ToolIcon.MergeConnections` GeometryGroup —
   the left edge bar plus three connectors converging to the edge centre (fill-based geometry, like
   every other glyph in the file), the visual inverse of `ToolIcon.SpaceConnections`.

5. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows (no GUI under WSL2).

No model change; reuses `ConnectionDistributor` and the forced-anchor mechanism. Note: Space → Merge
→ Space is not a guaranteed exact positional round-trip (after merge all fractions are equal, so
re-spacing order is stable-sort order) — the buttons are conceptual opposites, not strict inverses.

## Status

- [x] 1 Document VM (helper + Merge command) · [x] 2 Ribbon · [x] 3 Context menu · [x] 4 Icon · [x] 5 Build (clean, 0 warnings)

Implemented on `feature/merge-connections`; build clean (nullable-as-error). Pending: manual
verification on Windows (no GUI under WSL2):
1. Fan several connectors onto one side of a shape (or **Space connections** to spread them) →
   **Merge connections**: all ends on that side collapse to the edge midpoint (stacked); a shape with
   ends on two sides keeps them per-side, each centred.
2. **Space connections** → they spread evenly again (conceptual round-trip).
3. Merge again with nothing changed → no extra undo step (idempotent).
4. Undo once after a Merge → ends return to their pre-merge anchors in one step.
5. Button disabled with no shape selected, enabled with ≥1; the right-click Arrange menu shows the
   new item too.
