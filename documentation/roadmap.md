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

## UI shell — Ribbon, icons & theming ✅ (cross-cutting)

Replaces the top toolbar + left tool palette with a `Carbon.Avalonia.Desktop` **Ribbon**
(Home / Insert / View; per-category tool dropdowns); menu bar, inspector, tabs and status bar
stay. Icons come from `PhosphorIconsAvalonia`, with custom vector glyphs for the UML items it
lacks. Adopts Carbon's palette for the chrome and as the new default shape style (soft-grey fill,
accent-blue stroke, dark text). See `documentation/plans/2026-06-02-ui-ribbon-revamp.md`.
