# Phase 3 — UML Class Diagrams: Design

Status: approved 2026-06-01. Branch: `feature/phase3-uml-class-diagrams`.

Phase 3 adds UML class-diagram support on top of the Phase 1 editor and Phase 2
connectors: a compartment node for **Class / Interface / Enum** with structured,
editable members, and class relationships that reuse the existing connector set.

## 1. Goals / non-goals

In scope:

- A new `ClassNode` domain type and a `ClassNodeViewModel` that renders a UML
  compartment box (name compartment, attributes compartment, operations compartment).
- Structured members (attributes + operations) carrying visibility, name, free-text
  type, static/abstract flags; enum literals as a single compartment.
- Member editing: inline raw-text row editing on the canvas + rich structured editing
  (add / remove / reorder / per-field) in the inspector.
- Type-field autocomplete sourced from diagram type names plus common primitives.
- Class relationships (generalization, realization, dependency, association,
  aggregation, composition) reusing the **existing** Phase 2 connectors.
- Extraction of a shared `NodeViewModelBase` so node-agnostic concerns (selection,
  resize, style editing, canvas placement) serve both shapes and class nodes.

Out of scope (later phases):

- Crow's-foot / cardinality decorations (Phase 5 ER).
- Class → table mapping and DDL generation (Phase 5).
- Structured, per-parameter method editing — method parameters stay free-text.
- Diagram-type gating of the toolbox (class tools are always available).
- Code generation / reverse engineering.

## 2. Model layer (`Jcl.Draw.Model`)

New types under `src/Jcl.Draw.Model/Nodes/`:

- `ClassNode : NodeBase` (sealed). Adds:
  - `ClassNodeKind Kind`
  - `string Name`
  - `List<ClassMember> Members`
  - `bool IsAbstract`
  - `Clone()` performs a deep copy (new list, copied member records) and calls
    `CopyBaseTo`.
- `ClassMember` (record):
  - `MemberVisibility Visibility`
  - `string Name`
  - `string? Type` — free text; also the return type for operations.
  - `string? Parameters` — free text; operations only (e.g. `amount: decimal`).
  - `MemberKind Kind` — `Field`, `Operation`, or `EnumLiteral`.
  - `bool IsStatic`
  - `bool IsAbstract`
- Enums:
  - `ClassNodeKind { Class, Interface, Enum }`
  - `MemberVisibility { Public, Private, Protected, Package }` (rendered `+ - # ~`)
  - `MemberKind { Field, Operation, EnumLiteral }`

Serialization: add one attribute to `NodeBase`
(`src/Jcl.Draw.Model/Nodes/NodeBase.cs`):

```csharp
[JsonDerivedType(typeof(ClassNode), "class")]
```

No other serialization change is required. The existing `JsonDocumentSerializer`
(camelCase, `JsonStringEnumConverter`, polymorphic `$type` list, null-omitting)
already covers the new type and its members. `DiagramType.Class` already exists.

## 3. Member signature parser/formatter (`Jcl.Draw.Diagramming/Uml/`)

A pure, display-free unit — the central reusable piece, fully unit-tested:

- `string Format(ClassMember member)`:
  - Field → `"+ balance: decimal"` (omits `: type` when `Type` is null/empty).
  - Operation → `"+ deposit(amount: decimal): void"`.
  - Enum literal → `"ACTIVE"` (name only).
- `ClassMember TryParse(string text, MemberKind context)`:
  - Leading visibility marker (`+ - # ~`) optional → defaults to `Public`.
  - `name(params): ret` ⇒ `Operation`; `name: type` or bare `name` ⇒ `Field`;
    context `EnumLiteral` ⇒ literal (name only).
  - Tolerant of whitespace and missing type/return; never throws on user input
    (returns a best-effort member; truly empty input yields `false`/no member).
- Round-trips: `TryParse(Format(m)) == m` for all well-formed members.

Lives in `Jcl.Draw.Diagramming` (logic layer, no Avalonia dependency) so both the
view models and the unit tests can use it without a display.

## 4. View-model layer (`Jcl.Draw.App/ViewModels`)

### 4.1 Extract `NodeViewModelBase`

Pull the node-agnostic surface out of `ShapeNodeViewModel` into a new
`NodeViewModelBase : ViewModelBase`:

- Model reference: `NodeBase Model`.
- Geometry/placement: `X`, `Y`, `Width`, `Height`, `ZIndex` (read/write, with the
  existing record-`with` mutation + `OnPropertyChanged` pattern).
- Interaction state: `IsSelected`, `IsEditing`.
- Style-derived (read-only, computed from `Model.Style`): `Fill`, `Stroke`,
  `StrokeThickness`, `StrokeDashArray`, `Foreground`, `FontFamily`, `FontSize`,
  `FontWeight`, `FontStyle`, `TextAlignment`.
- `RaiseStyleChanged()`.
- `Geometry` — exposed on the base; `ShapeNodeViewModel` returns its shape geometry,
  `ClassNodeViewModel` returns its (plain) bounding rectangle.

`ShapeNodeViewModel` keeps `Kind`, `Text`, and its shape `Geometry` override.

Ripples (mechanical retypes from `ShapeNodeViewModel` to `NodeViewModelBase`):

- `DiagramDocumentViewModel.Nodes` → `ObservableCollection<NodeViewModelBase>`;
  `SelectedNodes` → `IEnumerable<NodeViewModelBase>`.
- `ConnectorViewModel.Source` / `Target` → `NodeViewModelBase`.
- `DiagramView.axaml.cs` `HandlePositions(...)` and node hit-testing → base type.

Consequence: the inspector's shared style section (fill / stroke / font) operates on
`NodeViewModelBase` and therefore works on class nodes with no extra code.

### 4.2 New VMs

- `ClassNodeViewModel : NodeViewModelBase`:
  - `Name`, `Kind`, `IsAbstract`.
  - Projected collections: `ObservableCollection<ClassMemberViewModel> Attributes`
    and `Operations` (for Class/Interface), or `Literals` (for Enum), kept in sync
    with `Model.Members`.
  - `AddMember`, `RemoveMember`, `MoveMember(from, to)`.
  - Auto-bumps `Height` to a computed minimum when members are added past current
    content height (see Assumption A1).
- `ClassMemberViewModel : ViewModelBase`:
  - Wraps a `ClassMember`.
  - `DisplayText` (via `Format`) for read mode.
  - Editable `RawText` + `IsEditing` for inline editing; commit runs `TryParse`.
  - Structured fields (`Visibility`, `Name`, `Type`, `IsStatic`, `IsAbstract`,
    `Kind`) for the inspector editors.

### 4.3 Inspector

`InspectorViewModel` gains a third selection mode `IsClassNodeSelected` alongside
`IsShapeSelected` / `IsConnectorSelected`. `LoadFromSelection()` dispatches on the
concrete type of the selected node. The shared style editors already apply via the
base. A new **member editor** section provides:

- A member list with add / remove / move-up / move-down.
- Per-row editors: visibility dropdown, name, type via `AutoCompleteBox`,
  static/abstract toggles, field-vs-operation selector, and (operations) a
  free-text parameters field.

All member/name/kind mutations route through `NotifyStyleEditStarting()` (memento
undo capture) → mutate model → `RaiseStyleChanged()` → `MarkModified()`, matching
the existing style-edit pattern.

### 4.4 Toolbox & document

- `ToolboxViewModel` gains a `ClassNodeToolItem(string Name, ClassNodeKind Kind)`
  collection with Class / Interface / Enum tools.
- `DiagramDocumentViewModel.AddClassNode(ClassNodeKind kind, Point2D point)` mirrors
  `AddShape`: capture undo, create a `ClassNode` with the document default style and
  a default name, add to model + `Nodes`, select, mark modified, return the VM.
- `RebuildNodes()` instantiates `ClassNodeViewModel` vs `ShapeNodeViewModel` by
  inspecting the model type, so undo/redo and open/save reconstruct class nodes.

## 5. Rendering (`Jcl.Draw.App`)

- Add a second `DataTemplate DataType="vm:ClassNodeViewModel"` to the nodes
  `ItemsControl` in `Views/DiagramView.axaml`. Avalonia selects the template by data
  type; the existing `ContentPresenter` style binding `Canvas.Left/Top` ← `X/Y`
  works unchanged (base properties).
- Template structure (a `Grid` of auto-height rows inside the positioned panel):
  - Name compartment: optional stereotype line (`«interface»` / `«enumeration»`),
    then the name (italic when `IsAbstract`).
  - Divider line.
  - Attributes compartment: `ItemsControl` over `Attributes`.
  - Divider line.
  - Operations compartment: `ItemsControl` over `Operations`.
  - Enum variant: name compartment + a single literals compartment (no operations);
    operations compartment and its divider are collapsed.
- Member row template: a `TextBlock` bound to `DisplayText` (static ⇒ underline via
  `TextDecorations`, abstract ⇒ italic via `FontStyle`) swapped with a `TextBox` when
  the row's `IsEditing` is true. Double-tap is handled on the row element itself,
  setting that row's `IsEditing` — no per-row canvas geometry math. Add / remove /
  reorder are inspector-only.
- Selection border and the 8 resize handles are computed from `Bounds` in the base
  and are reused unchanged. The class box outer boundary is a plain rectangle (drawn
  via a `Border`/`Path`); `ShapeGeometryBuilder` gains a rectangle path if needed.

## 6. Connectors & routing for class nodes

Connectors already reference nodes by `Guid` and already model the full UML
relationship set, so drag-to-connect, the connector inspector, and orphan-pruning
work for class nodes once `ConnectorViewModel.Source/Target` are `NodeViewModelBase`.

For routing/attachment, a class node is treated as its bounding **rectangle**:
`ShapeBoundary` / `ShapeOutline` (`Jcl.Draw.Diagramming/Geometry/`) get a rectangle
path for class nodes, reusing the existing `Rectangle` intersection logic. No new
routing strategy is introduced.

## 7. Autocomplete

`DiagramDocumentViewModel.GetTypeSuggestions()` returns the distinct names of all
Class / Interface / Enum nodes in the current diagram, unioned with a static list of
common primitives (`int, long, string, bool, double, decimal, float, char, byte,
DateTime, DateTimeOffset, Guid, TimeSpan, void, object`). The inspector's type
`AutoCompleteBox` binds to it.

## 8. Undo & dirty-tracking

All member/name/kind/flag mutations route through the existing
`NotifyStyleEditStarting()` → `CaptureUndo()` (whole-document memento clone) →
mutate → `RaiseStyleChanged()` → `MarkModified()` seam. Because undo restores the
document and rebuilds view models, `RebuildNodes()`'s type-aware instantiation
ensures class nodes survive undo/redo and save/open round-trips.

## 9. Testing (TDD)

Follow the existing test layout and the Microsoft.Testing.Platform / xUnit v3 setup.

- Model (`Jcl.Draw.Model.Tests`): `ClassNode` JSON round-trip including members,
  enum literals and flags; `Clone()` deep-copy independence.
- Parser (`Jcl.Draw.Diagramming.Tests`): `Format`/`TryParse` round-trips; tolerant
  parsing (missing visibility, operations vs fields, malformed/empty input).
- View models (display-free): add/remove/reorder members; inline raw-text
  commit → parse; auto-height bump; autocomplete suggestion set (diagram names ∪
  primitives, distinct); inspector third-mode switch; undo/redo of member edits;
  connector endpoint retype against `NodeViewModelBase`.

## 10. Assumptions

- **A1** — New class nodes are user-resizable in both dimensions (reusing the
  8-handle system), with a computed **minimum** size that grows as members are added;
  enlarging the box beyond content distributes slack to the operations compartment.
- **A2** — Class / Interface / Enum tools are always present in the toolbox; no
  diagram-type gating (consistent with shape and connector tools).
- **A3** — A new node starts with a default name (`Class1` / `Interface1` /
  `Enumeration1`) and no members; members are added via the inspector.
- **A4** — This spec lives in `documentation/specs/` to match the repo's existing
  `documentation/` folder (rather than the brainstorming default `docs/`).

## 11. Implementation ordering (high level)

1. Model types + `[JsonDerivedType]` + serialization/clone tests.
2. Member parser/formatter + tests.
3. `NodeViewModelBase` extraction + retype ripples (shapes/connectors keep passing).
4. `ClassNodeViewModel` / `ClassMemberViewModel` + document `AddClassNode`,
   `RebuildNodes`, autocomplete + VM tests.
5. Inspector third mode + member editor.
6. Rendering template + inline row editing.
7. Connector/routing rectangle attachment for class nodes.
8. End-to-end pass: create, edit, connect, save/open, undo/redo; run full suite.

Risks: the `NodeViewModelBase` extraction touches Phase 1/2 code (step 3) — keep it
mechanical and green before adding class-node behavior. Inline row double-tap routing
must not regress shape double-tap editing.
