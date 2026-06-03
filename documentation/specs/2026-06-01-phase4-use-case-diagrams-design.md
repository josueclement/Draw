# Phase 4 — Use-Case Diagrams: Design

Status: approved 2026-06-01. Branch: `feature/phase4-use-case-diagrams`.

Phase 4 adds UML use-case-diagram support on top of the Phase 1–3 editor: three new node
types (actor, use-case, system boundary) and the use-case relationships. It reuses the
`NodeViewModelBase` foundation, the connector/relationship machinery, and the
node-type/toolbox/inspector patterns established in Phase 3 — so it is deliberately small.

## 1. Goals / non-goals

In scope:

- `ActorNode` — a stick-figure node with a name label below the figure.
- `UseCaseNode` — an ellipse node with centered text.
- `SystemBoundaryNode` — a titled rectangle drawn behind use-cases (visual-only).
- View-models for each (deriving `NodeViewModelBase`), DataTemplates, toolbox tools,
  per-type creation, undo/redo reconstruction, and inline label editing.
- Surfacing the **Include** and **Extend** relationships in the toolbox connector palette.
- A small generalization of inline label editing onto `NodeViewModelBase`.

Out of scope (later or never):

- Real containment/grouping for the system boundary (dragging it does **not** move its
  contents; membership is not tracked).
- UML well-formedness validation — any relationship kind may connect any two nodes, exactly
  as for shapes and class nodes today.
- Diagram-type gating of the toolbox (use-case tools are always available, consistent with
  Phase 3's decision).
- Vector export / DDL (Phase 5).

## 2. Model layer (`Draw.Model/Nodes`)

Three new sealed `NodeBase` subtypes, each mirroring `ClassNode`/`ShapeNode`: a label field,
a `Clone()` that copies it and calls `CopyBaseTo`, and one `[JsonDerivedType]` registration
on `NodeBase`.

- `ActorNode` — `string Name`. Discriminator `"actor"`.
- `UseCaseNode` — `string Text`. Discriminator `"useCase"`.
- `SystemBoundaryNode` — `string Title`. Discriminator `"systemBoundary"`.

`NodeBase` gains three attributes alongside the existing `shape`/`class` registrations:

```csharp
[JsonDerivedType(typeof(ActorNode), "actor")]
[JsonDerivedType(typeof(UseCaseNode), "useCase")]
[JsonDerivedType(typeof(SystemBoundaryNode), "systemBoundary")]
```

The existing `JsonDocumentSerializer` (camelCase, enum-as-string, polymorphic `$type` list)
covers these with no other change. `DiagramType.UseCase` already exists.

## 3. View-models (`Draw.App/ViewModels`)

### 3.1 Inline-label generalization on `NodeViewModelBase`

Add two virtual members so inline editing and the inspector's label field work generically
instead of type-by-type:

- `public virtual bool HasInlineLabel => false;`
- `public virtual string Label { get => string.Empty; set { } }`

Overrides:

- `ShapeNodeViewModel`: `HasInlineLabel => true`; `Label` reads/writes `Text`.
- `ActorNodeViewModel`: `HasInlineLabel => true`; `Label` reads/writes `Name`.
- `UseCaseNodeViewModel`: `HasInlineLabel => true`; `Label` reads/writes `Text`.
- `SystemBoundaryNodeViewModel`: `HasInlineLabel => true`; `Label` reads/writes `Title`.
- `ClassNodeViewModel`: leaves `HasInlineLabel => false` (members are edited per-row; the
  class name is edited in the inspector class panel).

### 3.2 The three new VMs

Each derives `NodeViewModelBase`, shadows its typed `Model`, exposes its label property
(write-through with `OnPropertyChanged`, like `ShapeNodeViewModel.Text`), overrides `Label`
and `HasInlineLabel`, and provides `BoundaryKind` + a `Geometry`:

- `ActorNodeViewModel` — `Name`; `BoundaryKind => ShapeKind.Rectangle`; `Geometry` =
  stick-figure (see §4).
- `UseCaseNodeViewModel` — `Text`; `BoundaryKind => ShapeKind.Ellipse` (connectors attach to
  the ellipse outline via the existing `ShapeBoundary`); `Geometry` = ellipse.
- `SystemBoundaryNodeViewModel` — `Title`; `BoundaryKind => ShapeKind.Rectangle`; the template
  draws a `Border`, so no `Geometry` is strictly required (the VM may omit it).

## 4. Rendering (`Draw.App`)

Three new `DataTemplate`s added to the nodes `ItemsControl.DataTemplates` (alongside the
Shape and Class templates). Canvas placement (`Canvas.Left/Top` ← `X`/`Y`) is unchanged.

- **Actor** (`ActorNodeViewModel`): a `Path` for the stick figure + a name `TextBlock` below
  it + an inline `TextBox` (visible on `IsEditing`) + the selection rectangle.
  - New `ActorGeometry` builder (`Draw.App/Rendering`): given the node width/height,
    builds head (circle), body, arms and legs scaled to fit the **upper** region, reserving a
    fixed bottom strip (e.g. 18px) for the name label. Pure geometry, unit-testable in spirit
    (returns an Avalonia `Geometry`); stroke from the node style, no fill.
- **Use-case** (`UseCaseNodeViewModel`): an ellipse `Path`
  (`ShapeGeometryBuilder.Build(ShapeKind.Ellipse, …)`) with fill/stroke + a centered
  wrapping `TextBlock` + inline `TextBox` + selection rectangle — structurally the shape
  template.
- **System boundary** (`SystemBoundaryNodeViewModel`): a `Border` (transparent fill by
  default — the boundary reads as an outline with the canvas/grid visible inside; use-cases
  render on top of it regardless, since it sits behind; stroke from the node style) with a
  title `TextBlock` docked
  top-left + inline `TextBox` for the title + selection rectangle. Rendered **behind** the
  other nodes because it is created with a low `ZIndex` (see §5); `RebuildNodes` already
  orders by `ZIndex`, and the existing `HitTestNode` (`LastOrDefault` by z-order) therefore
  lets clicks select use-cases on top while empty interior clicks select the boundary.

## 5. Toolbox, document, inspector (`Draw.App`)

- `UseCaseNodeKind { Actor, UseCase, SystemBoundary }` (App-layer enum, toolbox/creation
  dispatch only — the model uses three distinct types).
- `ToolboxViewModel`: a `UseCaseToolItem(string Name, UseCaseNodeKind Kind)` record + a
  `UseCaseNodes` collection (Actor / Use case / System boundary), a `SelectedUseCaseNode`
  property and `IsUseCaseNodeMode` flag, with mutual exclusion against `SelectedShape`,
  `SelectedConnector` and `SelectedClassNode` (each setter clears the others;
  `ActivateSelectTool` clears all; `IsSelectTool` accounts for it). **Add `Include` and
  `Extend`** `ConnectorToolItem`s to the existing `Connectors` collection.
- `DiagramView.OnPointerPressed`: after the class-node placement block, a use-case-node
  placement block — when `toolbox.SelectedUseCaseNode` is set, call
  `_vm.AddUseCaseNode(tool.Kind, world)` then `ActivateSelectTool`.
- `DiagramDocumentViewModel.AddUseCaseNode(UseCaseNodeKind kind, Point2D center)`: mirrors
  `AddClassNode` — capture undo, create the right node type with a default size and the
  document default style, assign `ZIndex`, add, select, mark modified, return the VM.
  Default sizes: actor 48×84, use-case 130×72, boundary 320×220. A **system boundary gets a
  low z-index** (`min(existing ZIndex) − 1`, or 0 when empty) so it renders behind; the other
  two use `NextZIndex()`.
- `CreateNodeViewModel` gains `ActorNode`/`UseCaseNode`/`SystemBoundaryNode` arms.
- Inspector: the existing single-line text editor (currently shown only for
  `IsShapeSelected`) is broadened to all label-bearing nodes. Add an `IsLabelNodeSelected`
  flag (true when the selected node `HasInlineLabel`), bind the text field's visibility to it,
  and have `ApplyText`/`LoadFromSelection` read/write `node.Label` rather than
  `ShapeNodeViewModel.Text`. Shared style (fill/stroke/font) already reaches the new nodes via
  `NodeViewModelBase` and the existing `ApplyShapeStyle`.

## 6. Inline editing & undo

- `DiagramView.OnDoubleTapped`: generalize the current `is ShapeNodeViewModel` check to
  `node is { HasInlineLabel: true }` → `CaptureUndo()` + `node.IsEditing = true`. New node
  templates bind their inline `TextBox` to `Label` (`Mode=TwoWay`).
- `EndEditing`: clear `IsEditing` on every editing node (not just shapes), still commit
  pending class-member edits, and `MarkModified()` when any inline label edit or member edit
  was committed (this also makes shape inline-text edits dirty-correct).

## 7. Connectors

No new rendering. `Association` (plain line), `Include`/`Extend` (dashed + «include»/«extend»
stereotype via `ConnectorViewModel.DefaultStereotype` + `ConnectorDecorationBuilder`), and
`Generalization` (hollow triangle) all already exist and render. Phase 4 only adds Include and
Extend to the toolbox palette.

## 8. Testing

Follow the existing layout and the Microsoft.Testing.Platform / xUnit v3 setup.

- Model (`Draw.Model.Tests`): JSON round-trip for `ActorNode`/`UseCaseNode`/
  `SystemBoundaryNode` (label preserved, correct `$type`); `Clone()` deep-copy.
- View models (`Draw.App.Tests`, display-free): `AddUseCaseNode` creates the correct type,
  selects it, marks modified; a system boundary receives a z-index below all existing nodes;
  `BoundaryKind` per type (Actor=Rectangle, UseCase=Ellipse, Boundary=Rectangle); `Label`
  round-trips to the underlying field; `HasInlineLabel` per type; undo/redo reconstructs the
  right VM types via `RebuildNodes`; toolbox mutual exclusion and `IsUseCaseNodeMode`; Include
  and Extend present in the connector palette.
- Connector attachment to a use-case: a `ConnectorRouteRequest` with an `Ellipse` source/target
  boundary produces an on-outline attachment (already covered for ellipses; add a use-case-
  framed assertion if cheap).
- AXAML/pointer-code-behind tasks (templates, placement, `ActorGeometry` visuals): build-
  verified (compiled XAML) + manual; no UI unit harness in this repo.

## 9. Assumptions

- **A1** System boundary is visual-only: low z-index, drawn behind, no parent-child grouping.
- **A2** No diagram-type gating; use-case tools are always available in the toolbox.
- **A3** No UML well-formedness validation; any relationship kind connects any two nodes.
- **A4** Inline label editing is generalized via `HasInlineLabel`/`Label`; inline commit marks
  the document modified (also tightening shape inline-text editing).
- **A5** Actor connector attachment uses its bounding rectangle; use-case uses the ellipse
  outline.
- **A6** This spec/plan live under `documentation/specs|plans/`.

## 10. Implementation ordering (high level)

1. Model types + `[JsonDerivedType]` + serialization/clone tests.
2. `HasInlineLabel`/`Label` on `NodeViewModelBase`; `ShapeNodeViewModel` override (green;
   existing tests pass).
3. The three node VMs + `AddUseCaseNode` + `CreateNodeViewModel` arms + VM tests.
4. Toolbox use-case tools + Include/Extend; placement in `OnPointerPressed`; palette AXAML.
5. Inspector label-field generalization (`IsLabelNodeSelected`, `Label`-based apply/load).
6. Rendering: `ActorGeometry` + the three DataTemplates; generalized inline edit in
   `OnDoubleTapped`/`EndEditing`.
7. End-to-end pass: place all three, label them, connect (association / include / extend /
   generalization), save/open, undo/redo; run the full suite.

Risk: steps 2 and 5 touch Phase 1 code (`ShapeNodeViewModel`, inspector) — keep them
mechanical and green. Verify the boundary's low z-index actually renders it behind and that
clicks still select use-cases on top.
