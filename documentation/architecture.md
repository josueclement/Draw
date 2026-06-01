# Architecture

## Layers

| Project | TFM | Responsibility |
|---|---|---|
| `Jcl.Draw.Model` | net10 | Framework-agnostic document model (`DiagramDocument`, `NodeBase`/`ShapeNode`, `Connector`), value primitives (`Point2D`/`Rect2D`/`ArgbColor`), styling, and `System.Text.Json` serialization with polymorphic `$type` discriminators. |
| `Jcl.Draw.Diagramming` | net10 | UI-agnostic behavior: memento `IUndoService`, grid-snapping extensions. |
| `Jcl.Draw.App` | net10 / Avalonia 12 | `IHost` bootstrap, DI, MVVM view models, services, and the canvas editor. |

> **TFM deviation:** the libraries target `net10` rather than the house default of
> `netstandard2.0` because they have no external consumers (documented in each `.csproj`).

## Rendering — hybrid retained-mode MVVM

Nodes are templated Avalonia controls positioned on a `Canvas` inside a zoom/pan
transform; selection handles and the marquee live on an overlay canvas. Avalonia renders
with Skia under the hood, so no raw `SKCanvas` is needed. `ShapeNodeViewModel` exposes
Avalonia media (geometry/brushes) derived from the model; `ShapeGeometryBuilder` produces
the outline for each of the 7 shape kinds (sharing `ShapeOutline` with the router).

## Connectors (Phase 2)

`Jcl.Draw.Diagramming.Routing` computes connector geometry independently of the UI:
`ShapeBoundary` finds where a ray from a shape's centre crosses its outline (the floating
attachment point), and `IConnectorRouter` dispatches to a `StraightRouter` /
`OrthogonalRouter` / `BezierRouter` strategy (registered in DI) returning a `ConnectorRoute`
(polyline or cubic) plus non-zero endpoint direction vectors. `ConnectorViewModel` turns
that into Avalonia geometry, builds the per-relationship UML decorations
(`ConnectorDecorationBuilder`) and label positions, and recomputes whenever an endpoint
node moves. Connectors render as `Path` controls in a layer behind the nodes; selection and
hit-testing use point-to-segment distance against the flattened route.

## Bootstrapping

`Program.Main` builds an `IHost`, starts it, runs Avalonia's classic desktop lifetime to
completion, then stops the host. The Avalonia UI loop owns the foreground; the host
provides DI, configuration, logging and (later) hosted services. `App` resolves
`MainWindow` from the container in `OnFrameworkInitializationCompleted`.

## Key decisions (from the requirements interview)

- **Document = one diagram per file** (`.jcld`, JSON). Multiple open files via tabs.
- **Undo = memento snapshots** (whole-document clones via the serializer), captured once
  per completed gesture; depth-capped via `UndoOptions`.
- **Each document tab owns its own `IUndoService`** (created by `DiagramDocumentViewModelFactory`).
- **View models avoid `Avalonia.Controls`** (use `Avalonia.Media` value types only), so
  editor logic is unit-tested without a display.
- **PanAndZoom dropped:** `Avalonia.Controls.PanAndZoom` 11.3.0 depends on Avalonia 11 and
  has no Avalonia 12 build, so zoom/pan is implemented in-house with a `MatrixTransform`.
- **Clipboard image** uses Avalonia 12's `IClipboard.SetBitmapAsync` (platform-dependent).

## Testing

xUnit v3 on the Microsoft Testing Platform (opted in via `global.json`). Coverage targets
non-UI logic: serialization round-trips, memento undo/redo, snapping math, and view-model
behavior (add/select/move/delete/undo, file commands, inspector). No pixel tests.
