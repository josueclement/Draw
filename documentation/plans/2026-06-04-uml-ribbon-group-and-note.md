# UML ribbon group + Note shape — Implementation plan

Branch: `feature/uml-ribbon-group-and-note`.

The **Insert** ribbon tab previously scattered UML node tools across separate **Class diagram** and
**Use case** groups. This consolidates them under a single **UML** group and adds a UML **Note**
shape (a rectangle with a folded top-right corner) as a standalone button in that group. The
**Connectors** group is left as-is. New Insert tab order: `Shapes | Connectors | UML | Image`.

`Note` is a new `ShapeKind`; the existing insert → `ShapeNode` → `ShapeNodeViewModel` → DataTemplate
→ JSON (`$type:"shape"`) pipeline handles it with no new node type, factory, view model, or template
change. It uses the generic default shape style.

## Steps

1. **Model** — `Draw.Model/Nodes/ShapeKind.cs`: append `Note = 8`.

2. **Connector outline** — `Draw.Diagramming/Geometry/ShapeOutline.cs`: `GetPolygon` routes
   `ShapeKind.Note` to the full rectangle vertices (the dog-ear is ignored for connector attachment).

3. **Render geometry** — `Draw.App/Rendering/ShapeGeometryBuilder.cs`: explicit `ShapeKind.Note`
   arm + `NoteGeometry(width, height)` (body figure with cut corner + a stroked fold figure;
   `fold = Clamp(min(w,h) * 0.22, 6, 18)`). The explicit arm is required — the default branch would
   otherwise draw a plain rectangle. Render (dog-ear) and routing (rectangle) deliberately differ.

4. **Toolbox** — `Draw.App/ViewModels/ToolboxViewModel.cs`: add `ShapeToolItem("Note", ShapeKind.Note)`
   to the `Shapes` collection so `SelectShapeToolCommand` resolves it and the status hint reads
   "Click on the canvas to place Note." Not added to the Shapes dropdown (its menu items are static
   XAML, not bound to this collection), so Note stays exclusive to the UML group button.

5. **Icon** — `Draw.App/Resources/ToolIcons.axaml`: `ToolIcon.Note` — a folded-corner sheet glyph
   (even-odd silhouette with the dog-ear cut out), in the shared 0–24 space.

6. **Ribbon** — `Draw.App/Views/MainWindow.axaml`: replace the `Class diagram` and `Use case`
   `RibbonGroup`s with one `RibbonGroup Header="UML"` holding both dropdowns (unchanged, same
   `x:Name`s so code-behind wiring still resolves) plus a `RibbonButton Header="Note"` bound to
   `Toolbox.SelectShapeToolCommand` with `CommandParameter` `ShapeKind.Note`.

No change to `MainWindow.axaml.cs`, the node factory, view models, or `DiagramView.axaml`.

## Verification

- `dotnet build Draw.slnx` (nullable warnings are build errors).
- Visual checks on Windows/macOS (WSL2 is headless): UML group shows Class + Use case + Note,
  Connectors still separate; Note places a folded-corner rectangle with editable text, resizes
  (fold scales), accepts connectors on its edges, and round-trips through save/reload.
