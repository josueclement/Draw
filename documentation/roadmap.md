# Roadmap

Delivered MVP-first; each phase is independently runnable.

## Phase 1 — Editor foundation ✅ (current)

Solution scaffolding + `IHost`/DI/Fluent theme; JSON document model with save/open/new and
multi-document tabs; full editor UX (zoom/pan, grid + snap, marquee multi-select,
move/resize handles, copy via duplicate, keyboard shortcuts); the 7 basic shapes with
text; per-shape styling + inspector; memento undo/redo; PNG + clipboard export.

## Phase 2 — Connectors

`IConnectorRouter` (straight / orthogonal / bezier), floating attachment with bend points,
the full UML relationship decoration set, and editable connector labels.

## Phase 3 — UML class diagrams

Shared compartment/list node (`Class` / `Interface` / `Enum`); member editor (inline +
inspector) with free-text types + autocomplete; class relationships reuse Phase 2.

## Phase 4 — Use-case diagrams

Actor, use-case, system boundary; association, include/extend, generalization.

## Phase 5 — ER diagrams + DB schema + vector export

`EntityNode` with columns (PK/FK/nullable/unique) and crow's-foot cardinality;
`Jcl.Draw.Sql` with `ISqlDialect` for PostgreSQL / SQL Server / SQLite / MySQL,
ER→DDL (primary) and optional class→table mapping; `Jcl.Draw.Export` adds SVG/PDF
(SkiaSharp).
