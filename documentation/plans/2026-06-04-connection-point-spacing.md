# Evenly space connection points on a shape edge — Implementation plan

Branch: `feature/connection-point-spacing`. Design: `documentation/specs/2026-06-04-connection-point-spacing-design.md`.

Cross-cutting editor enhancement: a one-shot **Space connections** action that spreads the connectors
attached to a selected shape evenly along each edge they touch (sides with ≥2 ends), pinned as forced
anchors. Exposed via a new Arrange-tab ribbon button and the existing right-click canvas menu.

## Steps

1. **Spacing math** — `Draw.Diagramming/Layout/ConnectionDistributor.cs` (new): `BoxSide` enum +
   pure `ClassifySide(bounds, point)`, `FractionAlong(side, bounds, point)`, and
   `EvenAnchor(side, index, count)` (point `i` of `N` at `(i+1)/(N+1)`). Works on bounds alone; the
   `(u,v)` is resolved to the outline elsewhere (`ShapeBoundary.ResolveAnchor`), so no shape kind needed.

2. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: `SpaceConnectionsCommand`
   (`RelayCommand`), `CanSpaceConnections` (≥1 selected), and `SpaceSelectedConnections` — collect the
   connector ends touching the selection (read current `RouteStart`/`RouteEnd`), bucket by (node, side),
   then for each side with ≥2 ends: `CaptureUndo()` once → order by current fraction → re-pin via
   `SetSourceAnchor`/`SetTargetAnchor` → `MarkModified()`. No-op (no undo) when nothing qualifies.
   Refresh the command + flag in `RaiseSelectionChanged()`.

3. **Ribbon** — `Draw.App/Views/MainWindow.axaml`: new **Connections** group on the Arrange tab with a
   **Space connections** `RibbonButton` bound directly to `ActiveDocument.SpaceConnectionsCommand`
   (no code-behind needed; the command's `CanExecute` drives `IsEnabled`).

4. **Context menu** — `Draw.App/Views/DiagramView.axaml.cs`: lower `MaybeShowArrangeMenu`'s gate from
   ≥2 to ≥1 selected node, and append a **Space connections** item (bound to `vm.SpaceConnectionsCommand`)
   to `BuildArrangeMenu`.

5. **Icon** — `Draw.App/Resources/ToolIcons.axaml`: custom `ToolIcon.SpaceConnections` (a shape edge
   bar with three evenly-spaced connector stubs).

6. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows (no GUI under WSL2).

## Status

- [x] 1 Spacing math · [x] 2 Document VM · [x] 3 Ribbon · [x] 4 Context menu · [x] 5 Icon ·
  [x] 6 Build (clean)

Implemented on `feature/connection-point-spacing`; build is clean (nullable-as-error; only pre-existing
`Watermark`-obsolete warnings remain). Pending: manual verification on Windows (no GUI under WSL2) —
see the checklist in the design's interaction notes.
