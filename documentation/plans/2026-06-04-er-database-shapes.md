# Database ER shapes — Implementation plan

Branch: `feature/er-database-shapes`. Design:
`documentation/specs/2026-06-04-er-database-shapes-design.md`.

The visual slice of Phase 5: entity (table) shapes that list columns with full ER metadata
(name/type/PK/FK/nullable/unique), plus crow's-foot relationship connectors with per-end cardinality.
DDL generation and vector export stay out of scope. The work mirrors the UML class-node machinery and
extends the connector decoration system; styling is inherited for free from `NodeViewModelBase`.

## Steps

1. **Model** — `Draw.Model/Nodes/EntityNode.cs` + `EntityColumn.cs` (new, mirror `ClassNode`/
   `ClassMember`); `[JsonDerivedType(typeof(EntityNode), "entity")]` on `NodeBase`;
   `Connectors/Cardinality.cs` (new); `Connector.cs` gains `SourceCardinality`/`TargetCardinality`
   (+`Clone()`); `RelationshipKind.Relationship = 9`.

2. **ColumnSignature** — `Draw.Diagramming/Er/ColumnSignature.cs` (new): `Format`/`Parse` with the
   `name: type` + `PK`/`FK`/`UNIQUE`/`NOT NULL`/`NULL` grammar; round-trips; PK ⇒ implicitly NOT NULL.

3. **Crow's-foot decorations** — `Draw.App/Rendering/ConnectorDecorationBuilder.cs`: 5 new
   `ConnectorEndDecoration` members, `FromCardinality`, `IsStrokeOnly`, bar/crow's-foot/circle geometry
   (combined via `GeometryGroup`). `ConnectorViewModel` resolves each end from its cardinality (falling
   back to the kind cap) and treats crow's-foot symbols as no-fill.

4. **Entity view models** — `EntityNodeViewModel` + `EntityColumnViewModel` (new, mirror the class VMs,
   single flat list); `DiagramDocumentViewModel.AddEntityNode` + `CreateNodeViewModel` case +
   `GetTypeSuggestions` includes entity names.

5. **Toolbox + ribbon** — `ToolboxViewModel`: `EntityToolItem`/`SelectedEntity`/`SelectEntityToolCommand`/
   `IsEntityNodeMode`, mutual exclusion across the existing tools, `Relationship` connector added.
   `MainWindow.axaml`: **ER** ribbon group (Table button + Relationship connector); `ShellViewModel.
   NewErCommand` + a *New ER diagram* File-menu entry.

6. **Canvas editing** — `DiagramView.axaml`: `EntityNodeViewModel` `DataTemplate` (header + single
   `ItemsControl` + hover `+`, per-row context menu with PK/FK/Nullable/Unique toggles + insert/move/
   delete). `DiagramView.axaml.cs`: `OnColumn*` handlers mirroring `OnMember*`; entity-placement
   canvas-click branch; right-click guard + `EndEditing` cover columns; shared focus helper.

7. **Inspector** — `InspectorViewModel`: connector `SourceCardinality`/`TargetCardinality` +
   `CardinalityOptions`; `IsEntityNodeSelected`/`SelectedEntityNode` (folded into `IsNodeSelected` so the
   shared style block shows); column add/remove/move commands. `MainWindow.axaml`: Source/Target
   cardinality dropdowns on the connector panel + an entity section (Name + column grid).

8. **Build** — `dotnet build Draw.slnx` clean (nullable-as-error); manual verification on Windows.

## Reuses, not rebuilds

- `ClassNode`/`ClassMember` + `Clone()`; `Uml/MemberSignature` (parsing shape); `ClassNodeViewModel`/
  `ClassMemberViewModel` + `INodeEditContext` (inline edit + memento undo); the class-node DataTemplate
  and `OnMember*` handlers; `AddClassNode`/`CreateNodeViewModel`; `ConnectorDecorationBuilder` geometry
  helpers; `InspectorViewModel.RelationshipOptions` pattern.
- Centralized styling: inheriting `NodeViewModelBase` gives palette/theme styling with no extra code.

## Status

- [x] 1 Model · [x] 2 ColumnSignature · [x] 3 Crow's-foot · [x] 4 Entity VMs · [x] 5 Toolbox+ribbon ·
  [x] 6 Canvas editing · [x] 7 Inspector · [x] 8 Build (clean, 0 warnings)

Implemented on `feature/er-database-shapes`; build clean (nullable-as-error) and, with compiled bindings
on, every new XAML binding path resolved at compile time. Pending manual verification on Windows (no GUI
under WSL2):
1. Insert ▸ ER ▸ Table (or File ▸ New ER diagram, then Table) → a titled table renders with the same
   palette styling as other nodes and recolours on theme toggle.
2. Add columns inline (`id: int PK`, `email: varchar(255) NOT NULL UNIQUE`, `user_id: int FK`): parse →
   row markers render; PK rows are bold + underlined; re-editing round-trips the text.
3. Right-click a column → toggle PK/FK/Nullable/Unique; Enter adds the next, Tab/Alt-arrows navigate/
   reorder; one undo step per gesture.
4. Draw a Relationship between two tables; set Source/Target cardinality in the inspector → crow's-foot /
   bar / circle symbols render at the correct ends and follow routing on move/resize.
5. Save a `.draw` with an entity + relationship, reopen → `$type:"entity"`, columns and both cardinalities
   persist; a pre-existing `.draw` opens unchanged.
