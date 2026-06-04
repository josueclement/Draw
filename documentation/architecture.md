# Architecture

## Layers

| Project | TFM | Responsibility |
|---|---|---|
| `Draw.Model` | net10 | Framework-agnostic document model (`DiagramDocument`, `NodeBase`/`ShapeNode`/`ClassNode`, `Connector`), value primitives (`Point2D`/`Rect2D`/`ArgbColor`), styling, and `System.Text.Json` serialization with polymorphic `$type` discriminators. |
| `Draw.Diagramming` | net10 | UI-agnostic behavior: memento `IUndoService`, grid-snapping, connector routing (`Routing`) + shape-boundary geometry, and UML member-signature parsing (`Uml.MemberSignature`). |
| `Draw.App` | net10 / Avalonia 12 | `IHost` bootstrap, DI, MVVM view models, services, and the canvas editor. |

> **TFM deviation:** the libraries target `net10` rather than the house default of
> `netstandard2.0` because they have no external consumers (documented in each `.csproj`).

## Rendering — hybrid retained-mode MVVM

Nodes are templated Avalonia controls positioned on a `Canvas` inside a zoom/pan
transform; selection handles and the marquee live on an overlay canvas. Avalonia renders
with Skia under the hood, so no raw `SKCanvas` is needed. `ShapeNodeViewModel` exposes
Avalonia media (geometry/brushes) derived from the model; `ShapeGeometryBuilder` produces
the outline for each of the 7 shape kinds (sharing `ShapeOutline` with the router).

## Connectors (Phase 2)

`Draw.Diagramming.Routing` computes connector geometry independently of the UI:
`ShapeBoundary` finds where a ray from a shape's centre crosses its outline (the floating
attachment point), and `IConnectorRouter` dispatches to a `StraightRouter` /
`OrthogonalRouter` / `RoundedRouter` strategy (registered in DI) returning a `ConnectorRoute`
(polyline or cubic curve) plus non-zero endpoint direction vectors. `ConnectorViewModel` turns
that into Avalonia geometry, builds the per-relationship UML decorations
(`ConnectorDecorationBuilder`) and label positions, and recomputes whenever an endpoint
node moves. Connectors render as `Path` controls in a layer behind the nodes; selection and
hit-testing use point-to-segment distance against the flattened route.

## Class diagrams (Phase 3)

`NodeViewModelBase` factors out the bindable concerns shared by every node kind —
placement, selection, and style — so `ShapeNodeViewModel` and `ClassNodeViewModel` differ
only in content; connectors, resize handles and the inspector's shared-style editors operate
on the base. A `ClassNode` (class / interface / enum) carries structured `ClassMember`s split
into attribute and operation compartments and renders as a multi-compartment box via its own
`DataTemplate`. Members round-trip to UML text (`+ name: Type`, `+ op(params): ret`) through
`Uml.MemberSignature`, edited either inline on the canvas (double-tap a row) or field-by-field
in the inspector; both paths route undo capture and dirty-marking through `INodeEditContext`.
Class nodes attach connectors as a plain rectangle (`BoundaryKind == Rectangle`), reusing the
Phase 2 router unchanged.

## Use-case diagrams (Phase 4)

Actor, use-case and system-boundary nodes are `NodeBase` subtypes on the same
`NodeViewModelBase` foundation. Inline label editing is generalized via `HasInlineLabel` /
`Label`, so shapes, actors, use-cases and boundaries all edit a single label the same way. The
system boundary is visual-only: it carries the lowest z-index and is inserted at the front of
the node collection so it renders behind the use cases it frames (no parent-child grouping).
Use-case relationships (association, include / extend, generalization) reuse the Phase 2
connectors unchanged; the toolbox now surfaces Include and Extend.

## Bootstrapping

`Program.Main` builds an `IHost`, starts it, runs Avalonia's classic desktop lifetime to
completion, then stops the host. The Avalonia UI loop owns the foreground; the host
provides DI, configuration, logging and (later) hosted services. `App` resolves
`MainWindow` from the container in `OnFrameworkInitializationCompleted`.

## Key decisions (from the requirements interview)

- **Document = one diagram per file** (`.draw`, JSON). Multiple open files via tabs.
- **Undo = memento snapshots** (whole-document clones via the serializer), captured once
  per completed gesture; depth-capped via `UndoOptions`.
- **Each document tab owns its own `IUndoService`** (created by `DiagramDocumentViewModelFactory`).
- **View models avoid `Avalonia.Controls`** (use `Avalonia.Media` value types only), so
  editor logic stays decoupled from the rendering layer.
- **PanAndZoom dropped:** `Avalonia.Controls.PanAndZoom` 11.3.0 depends on Avalonia 11 and
  has no Avalonia 12 build, so zoom/pan is implemented in-house with a `MatrixTransform`.
- **Clipboard image** uses Avalonia 12's `IClipboard.SetBitmapAsync` (platform-dependent).

## Verification

There is no automated test suite. Verify changes with `dotnet build Draw.slnx` (compiled XAML
validates bindings and types) plus a manual run on Windows/macOS — the headless Linux/WSL2
environment cannot render the GUI.
