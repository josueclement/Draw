# Roadmap

Delivered MVP-first; each phase is independently runnable.

## Phase 1 — Editor foundation ✅

Solution scaffolding + `IHost`/DI/Fluent theme; JSON document model with save/open/new and
multi-document tabs; full editor UX (zoom/pan, grid + snap, marquee multi-select,
move/resize handles, copy via duplicate, keyboard shortcuts); the 7 basic shapes with
text; per-shape styling + inspector; memento undo/redo; PNG + clipboard export.

## Phase 2 — Connectors ✅

`IConnectorRouter` with straight / orthogonal / bezier strategies and shape-boundary
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
