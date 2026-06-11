# Connector marquee selection + multi-select & bulk styling — Implementation plan

Branch: `feature/connector-multiselect`.

Two related gaps made connectors second-class for selection:

1. **The marquee (rubber-band) skipped connectors entirely.** `DiagramDocumentViewModel.SelectInRect`
   iterated only `Nodes` and called `ClearConnectorSelection()` first, so a dragged box could never grab a
   connector.
2. **Only one connector could be selected at a time** (a single `SelectedConnector`), mutually exclusive with
   node selection — so restyling many connectors meant clicking and editing each one individually.

This change makes connectors first-class selectable objects: marquee-grabbable, multi-selectable together with
shapes, and bulk-stylable from the Inspector and Quick-Style palette in one undo step.

## Decisions

- **Unified selection** — shapes and connectors can be co-selected (the old "nodes XOR connector" rule is gone).
- **Marquee = intersect/touch** — a connector is grabbed when the box overlaps any part of its line (consistent
  with shapes). Uses a true segment-vs-rectangle test, not just "a sampled point is inside".
- **Multi-select key = Shift** (unified): `Shift+click` toggles any item (shape or connector); `Shift+drag` =
  additive marquee. **Ctrl is unchanged**: `Ctrl+click` still toggles shapes and still splits a connector
  (inserts a bend point); `Ctrl+drag` stays additive.
- **Inspector with multiple connectors** = shared editors (kind, route, stroke colour, thickness, dash) shown
  whenever ≥1 connector is selected and applied to all; per-connector fields (cardinality, labels) shown only for
  a lone selection. Connector-priority for mixed selections (node styling then falls back to the palette).
- **Bulk scope** — stroke (colour, thickness, **dash**), route style, relationship kind, and the Quick-Style
  palette swatch all bulk-apply.
- **Dash** — there was no dash editor anywhere; added a dash dropdown to the connector Inspector. (Kinds that are
  inherently dashed — Realizes/Uses/Include/Extend — still force their own dash, so the setting visibly affects
  only Association-style connectors; pre-existing behaviour in `ConnectorViewModel.StrokeDashArray`.)

## Approach

Drive connector selection off the per-connector `IsSelected` flag exactly like nodes, and add
`SelectedConnectors` (plural). Keep the single-connector **editing** machinery (endpoint/waypoint/label handles,
split, snap) untouched by repurposing `SelectedConnector` as a computed **"active connector"**: it returns the
sole selected connector *only when exactly one connector is selected and no node is*, else `null`. Every existing
consumer (handle hit-test, handle drawing, snap, single-connector inspector fields) then engages for a lone
connector and disengages for multi/mixed — multi-selected connectors show only the blue highlight.

## Steps

1. **Geometry** — `Draw.Diagramming/Geometry/MarqueeGeometry.cs`: `IntersectsPolyline(Rect2D, IReadOnlyList<Point2D>)`
   via Liang–Barsky segment/AABB clipping; normalizes the rect (marquee dragged any direction). Tests in
   `tests/Draw.Diagramming.Tests/MarqueeGeometryTests.cs` (inside, crossing, passing-through, outside, corner,
   degenerate, negative-size).
2. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs`: `SelectedConnector` → computed get-only;
   add `SelectedConnectors`; `HasConnectorSelection`/`HasSelection` over the flags; notify
   `SelectedConnector`/`HasConnectorSelection` from `RaiseSelectionChanged`. `SelectInRect` iterates nodes **and**
   connectors (connector test = `MarqueeGeometry.IntersectsPolyline` over `GetFlattenedPoints`). Add
   `ToggleSelectConnector`/`ToggleSelectUnified`. `DeleteSelected` removes all selected connectors **and** nodes
   in one undo step via model-prune + `RebuildConnectors`. `ApplyStyleToSelection` loops `SelectedConnectors`.
3. **View** — `Draw.App/Views/DiagramView.axaml.cs`: read `shift`; gate the single-connector handle-edit on
   `!shift`; `Shift` toggles (unified), `Ctrl` unchanged, marquee additive = `ctrl || shift`.
4. **Inspector** — `Draw.App/ViewModels/InspectorViewModel.cs`: load over `SelectedConnectors` (representative =
   first), add `IsSingleConnectorSelected`, `ConnectorDash` + `DashStyleOptions`; `ApplyConnector`/
   `ApplyConnectorStroke` loop `SelectedConnectors`. `Draw.App/Views/MainWindow.axaml`: add the Dash combo; gate
   cardinality + label rows on `IsSingleConnectorSelected`; shared editors stay on `IsConnectorSelected`.
5. **Build & test** — `dotnet build Draw.slnx` clean (nullable-as-error); `dotnet test --solution Draw.slnx`.

## Out of scope

- Dash editing for **shapes** (dash control added to connectors only).
- Copy/paste of connectors (clipboard handles nodes only).
- Geometry-accurate node hit-testing; hover cursor affordances.

## Status

- [x] 1 Geometry + tests · [x] 2 Document VM · [x] 3 View · [x] 4 Inspector · [x] 5 Build + tests

Build is clean (nullable-as-error); 220/220 unit tests pass (incl. 10 new `MarqueeGeometryTests`). Pending:
manual GUI verification on Windows/macOS (no GUI under WSL2) — see the checklist in the approved session plan
(marquee over connectors/mixed clusters; Shift toggle; single-vs-multi edit handles; Ctrl+click split intact;
bulk colour/thickness/dash/route/kind with single Undo; palette bulk-apply; multi-delete).
