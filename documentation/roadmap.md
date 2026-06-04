# Roadmap

Delivered MVP-first; each phase is independently runnable.

## Phase 1 — Editor foundation ✅

Solution scaffolding + `IHost`/DI/Fluent theme; JSON document model with save/open/new and
multi-document tabs; full editor UX (zoom/pan, grid + snap, marquee multi-select,
move/resize handles, copy via duplicate, keyboard shortcuts); the 7 basic shapes with
text; per-shape styling + inspector; memento undo/redo; PNG + clipboard export.

## Phase 2 — Connectors ✅

`IConnectorRouter` with straight / orthogonal strategies and shape-boundary
attachment; the full UML relationship decoration set (open arrow, hollow/filled diamond,
hollow triangle, dashed lines for realization/dependency); editable source/center/target
labels; drag-from-node-to-node creation with preview; connector selection + inspector
(kind/route/labels/stroke); undo and orphan-pruning on node delete. Hardened via an
adversarial multi-dimension review pass (degenerate-geometry guards, non-destructive
connector rebuild, theme-aware decoration fills, save-state-aware dirty flag).

## Phase 3 — UML class diagrams ✅

Shared compartment/list node (`Class` / `Interface` / `Enum`); member editor (inline +
inspector) with free-text types + autocomplete; class relationships reuse Phase 2.

## Phase 4 — Use-case diagrams ✅ (current)

Actor, use-case, system boundary; association, include/extend, generalization.

## Phase 5 — ER diagrams + DB schema + vector export

`EntityNode` with columns (PK/FK/nullable/unique) and crow's-foot cardinality;
`Draw.Sql` with `ISqlDialect` for PostgreSQL / SQL Server / SQLite / MySQL,
ER→DDL (primary) and optional class→table mapping; `Draw.Export` adds SVG/PDF
(SkiaSharp).

## Connector editing — forced anchors, waypoints, movable labels ✅ (cross-cutting)

Direct in-canvas control over connector geometry on top of Phase 2: pin either endpoint anywhere on
a shape's outline (tracks move + resize), add/move/remove waypoints on straight + orthogonal
connectors (`Ctrl`+click / drag / `Alt`+click), and drag the source/center/target labels (with
`Alt`+click to reset). Reuses the selection-handle + `Ctrl`/`Alt`+click model and the memento undo.
See `documentation/plans/2026-06-03-connector-editing.md`.

Adds a fourth route style, **Rounded** — a smooth curve averaged from the bend points (rides the
midpoints between consecutive points; a gentle S-curve when there are none), rendered as true cubic
beziers. See `documentation/plans/2026-06-03-connector-rounded-route.md`.

A new connection is now **curved by default**: `AddConnector` creates it with the Rounded route and
auto-pins each end to the centre of its facing side (reusing the Space-connections math), so the
connector bows into a curve the instant two shapes are joined. See
`documentation/plans/2026-06-04-curved-connection-defaults.md`.

## UI shell — Ribbon, icons & theming ✅ (cross-cutting)

Replaces the top toolbar + left tool palette with a `Carbon.Avalonia.Desktop` **Ribbon**
(Home / Insert / View; per-category tool dropdowns); menu bar, inspector, tabs and status bar
stay. Icons come from `PhosphorIconsAvalonia`, with custom vector glyphs for the UML items it
lacks. Adopts Carbon's palette for the chrome and as the new default shape style (soft-grey fill,
accent-blue stroke, dark text). See `documentation/plans/2026-06-02-ui-ribbon-revamp.md`.

## UML member editing — canvas-first rapid entry ✅ (cross-cutting)

Adding members to a class/interface/enum is now keyboard-driven on the canvas: double-click a
compartment (or the hover `+` button) to add, `Enter` adds the next member of the same kind,
`Tab` / `Alt`+arrows navigate and reorder, and a right-click context menu covers
insert/move/visibility/delete. One undo step per member; the Inspector stays as the precise
editor. See `documentation/plans/2026-06-03-uml-member-editing-ux.md`.

## Align & distribute shapes ✅ (cross-cutting)

Tidy a group of shapes: align the selection (left/center/right/top/middle/bottom) to its bounding
box, and evenly distribute it (equal edge-to-edge gaps) horizontally/vertically. New **Arrange**
ribbon tab (Align dropdown + two Distribute buttons), `Ctrl+Shift+L/C/R/T/M/B` and `Ctrl+Shift+H/V`
shortcuts, and a right-click canvas menu (right-drag still pans). Pure geometry in
`Draw.Diagramming/Layout/ShapeArranger.cs`; one undo step per action; connectors re-route
automatically. See `documentation/plans/2026-06-04-align-distribute-shapes.md`.

## Connection point spacing ✅ (cross-cutting)

Tidy the connectors landing on a shape: a one-shot **Space connections** action spreads the connectors
attached to the selected shape(s) evenly along each edge they touch (sides with ≥2 connections),
preserving their current order and pinning them as forced anchors so the layout sticks on move/resize.
A **Connections** group on the Arrange ribbon tab + a right-click menu item (the arrange menu now opens
with ≥1 shape selected). Pure geometry in `Draw.Diagramming/Layout/ConnectionDistributor.cs`; one undo
step per action; reuses the forced-anchor mechanism. See
`documentation/plans/2026-06-04-connection-point-spacing.md`.

The action now **force-pins every end** touching the selection, not just crowded sides: a lone end on a
side is centred on that edge so the whole arrangement is locked. See
`documentation/plans/2026-06-04-force-pin-on-arrange.md`.

A sibling **Merge connections** action (same Connections group + context menu) is the inverse: it
collapses every connector end touching the selected shape(s) onto the centre of the side it lands on,
so the two buttons fan out ↔ regroup a shape's connectors. See
`documentation/plans/2026-06-04-merge-connections.md`.

## Copy/paste + image support ✅ (cross-cutting)

Clipboard editing for diagram content plus embedded images. **Copy/Cut/Paste/Duplicate**
(`Ctrl+C`/`X`/`V`/`D`) of the selected nodes, automatically carrying any connector whose **both**
endpoints are selected; paste centres on the viewport and re-links cloned nodes with fresh ids (one undo
step). A new `ImageNode` embeds its bytes as base64 in the `.draw` file and is inserted by pasting a
bitmap, the **Insert image** picker, or drag-&-drop of an image file; copying a lone image also puts a
bitmap on the OS clipboard for other apps, and pasting an external bitmap creates an image node. Images
resize with a locked aspect ratio and behave as full nodes (connectors/align/distribute apply). Clipboard
access goes through `IClipboardService` (Avalonia 12 typed `DataFormat`); keyboard shortcuts are handled
in `DiagramView` so they don't hijack text editing. See
`documentation/plans/2026-06-04-copy-paste-and-images.md`.

## Quick style palette ✅ (cross-cutting)

A **Styles** group on the Home ribbon tab with an always-visible grid of 10 curated **pastel**
swatches plus **Reset to default** and **No fill** buttons. One click recolours the selection (all
nodes + a selected connector) with a coordinated fill + stroke + text combination. Swatches are
**theme-aware**: each carries a Light and a Dark variant, and styled elements recolour automatically
on theme toggle — generalising the single default-fill sentinel into ~10 named palette tokens
(`Style.PaletteId`, resolved at render time; additive, backward-compatible serialisation). Palette
data is UI-agnostic in `Draw.Diagramming/Styling/StylePalette.cs`; apply reuses the style-edit memento
undo. See `documentation/plans/2026-06-04-quick-style-palette.md`.
