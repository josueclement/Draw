# Database ER shapes — Design

Delivers the **visual slice** of roadmap Phase 5: database entity (table) shapes that list columns
with full ER metadata, plus crow's-foot relationship connectors. The SQL/DDL generation (`Draw.Sql`)
and SVG/PDF export parts of Phase 5 are **out of scope** and remain future work.

The feature mirrors the existing UML class-node machinery (a titled box over a list-of-rows with inline
editing, signature parsing and context menus) and extends the connector decoration system additively.
Styling is inherited from `NodeViewModelBase`, so ER tables pick up the theme-aware quick-style palette
with no extra code ("same styles as the others").

## Decisions

1. **Column metadata** — name, type, Primary Key, Foreign Key, nullable, unique.
2. **Connectors** — crow's-foot notation with **per-end** cardinality (source + target chosen
   independently): One / Many / ZeroOrOne / OneOrMany / ZeroOrMany.
3. **Scope** — shapes + connectors only. No DDL generation, no vector export.
4. **Editing** — inline text syntax *and* context-menu toggles (mirrors UML member editing).
5. **Layout** — a single flat column list; PK/FK shown inline (PK bold + underlined, flags in the row text).
6. **UI** — a dedicated **ER** ribbon group; the reserved `DiagramType.Er` is wired via a
   *New ER diagram* File-menu entry (it only sets the default palette — node types are never gated by
   diagram type).
7. **Naming** — `EntityNode` / `EntityColumn` (matches the roadmap's wording).

## Model (`Draw.Model`)

- `Nodes/EntityNode.cs` — `NodeBase` with `string Name` + `List<EntityColumn> Columns`; `Clone()`.
- `Nodes/EntityColumn.cs` — `Name`, `Type?`, `IsPrimaryKey`, `IsForeignKey`, `IsNullable` (default
  `true`), `IsUnique`; `Clone()`.
- `Nodes/NodeBase.cs` — `[JsonDerivedType(typeof(EntityNode), "entity")]` (reflection-based polymorphic
  serialization, so the attribute is all that is needed).
- `Connectors/Cardinality.cs` — `Unspecified | One | Many | ZeroOrOne | OneOrMany | ZeroOrMany`.
- `Connectors/Connector.cs` — `SourceCardinality` / `TargetCardinality` (default `Unspecified`), copied
  in `Clone()`. Additive + backward-compatible (`DefaultIgnoreCondition` omits nothing structural).
- `Connectors/RelationshipKind.cs` — `Relationship = 9`: a plain solid ER line whose end symbols come
  from the per-end cardinality, not the kind.

## Behavior (`Draw.Diagramming`)

- `Er/ColumnSignature.cs` — `Format(EntityColumn)` / `Parse(string)`, modeled on `Uml/MemberSignature`.
  Grammar: `name: type` plus trailing case-insensitive flag tokens `PK`, `FK`, `UNIQUE`/`UQ`,
  `NOT NULL`/`NN`, `NULL`. Flags are pulled off the end first so a multi-word type survives; a PK is
  implicitly NOT NULL (the marker is omitted to keep `Format`↔`Parse` a round-trip).

## View models (`Draw.App/ViewModels`)

- `EntityNodeViewModel` (mirrors `ClassNodeViewModel`, single list) — `Columns`, `Name`, content-driven
  `MinHeight`; `AddColumn`, `InsertNewColumn`, `Locate`, `RemoveColumn`, `MoveColumn`,
  `DiscardEmptyNewColumns`, `CommitPendingEdits`. Inherits all styling from `NodeViewModelBase`.
- `EntityColumnViewModel` (mirrors `ClassMemberViewModel`) — bindable `Name/Type/IsPrimaryKey/
  IsForeignKey/IsNullable/IsUnique`; inline edit lifecycle (`BeginEdit/CommitEdit/CancelEdit`) over
  `ColumnSignature`; `DisplayText`, `RowFontWeight`/`RowDecorations` (PK = bold + underline). Setting
  `IsPrimaryKey` clears nullability. Undo via the shared `INodeEditContext`.
- `DiagramDocumentViewModel` — `AddEntityNode(Point2D)` (clones `DefaultShapeStyle`); `EntityNode` case
  in `CreateNodeViewModel`; `GetTypeSuggestions` also surfaces entity names.
- `ToolboxViewModel` — single `EntityToolItem` + `SelectedEntity`/`SelectEntityToolCommand`/
  `IsEntityNodeMode`; the `Relationship` connector added to the `Connectors` list.
- `InspectorViewModel` — connector `SourceCardinality`/`TargetCardinality` + `CardinalityOptions`;
  entity-node selection (`IsEntityNodeSelected`, `SelectedEntityNode`) + column commands.
- `ShellViewModel` — `NewErCommand` → `CreateNew(DiagramType.Er)`.

## Rendering (`Draw.App/Rendering`)

- `ConnectorDecorationBuilder` — adds `CrowOne/CrowMany/CrowZeroOrOne/CrowOneOrMany/CrowZeroOrMany`
  to `ConnectorEndDecoration`, a `FromCardinality` mapper, `IsStrokeOnly` (crow's-foot symbols are
  stroke-only), and geometry builders (a perpendicular bar, a three-prong crow's foot, a hollow circle,
  combined via a `GeometryGroup`). `Relationship` resolves to no kind-decoration (the default), so the
  cardinality drives both ends.
- `ConnectorViewModel` — each end's decoration is `FromCardinality(cardinality)` when set, else the
  kind-derived cap; `DecorationFill` returns null for stroke-only symbols.

## Views

- `DiagramView.axaml` — an `EntityNodeViewModel` `DataTemplate` (Border + Grid header + single column
  `ItemsControl` + hover `+`), binding `Fill`/`Stroke`/`Foreground` from the base VM; per-row context
  menu toggles PK/FK/Nullable/Unique and insert/move/delete.
- `DiagramView.axaml.cs` — column edit/navigate/insert/move/delete handlers mirroring the `OnMember*`
  set; the canvas-click branch placing an entity when its tool is armed; `EndEditing` commits columns.
- `MainWindow.axaml` — **ER** ribbon group (Table + Relationship); *New ER diagram* File entry; connector
  Source/Target cardinality dropdowns; an entity-node inspector section (Name + column grid).

## Out of scope (future Phase 5)

ER→SQL DDL (`Draw.Sql`, dialects); SVG/PDF vector export (`Draw.Export`); identifying vs non-identifying
(solid/dashed) relationship variants; auto-derived FK relationships.
