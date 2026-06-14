# Phase 4 — UML structural nodes

Adds 3 UML structural node types as labeled shapes (single editable title + decoration; no nesting
or compartments), mirroring the `SystemBoundaryNode` end-to-end pattern. Exposed via a new
**Structure** dropdown in the Insert ▸ UML ribbon group and the Shift+S **UML** tool-menu submenu.

- **Package** — folder-tab box (small tab + body), title in the body.
- **Component** — box with the «component» stereotype, a name, and two port tabs on the left edge.
- **Deployment** — a 3D box (cuboid), name centred on the front face.

All three route connectors as `ShapeKind.Rectangle`. Package and Component render via composed
Avalonia `Border`s in the data template; Deployment uses a `Geometry` built in
`App/Rendering/UmlNodeGeometry`.

## Changes

**Model** — `PackageNode`/`ComponentNode`/`DeploymentNode` (title/name + Clone), and 3
`[JsonDerivedType]` discriminators in `NodeBase.cs` (`NodeKindContractTests` auto-covers them).

**App** — `UmlNodeKind` enum; `PackageNodeViewModel`/`ComponentNodeViewModel`/`DeploymentNodeViewModel`;
`UmlNodeGeometry` (Deployment cuboid); 3 `NodeKindRegistry` descriptors;
`ToolboxViewModel` (`UmlToolItem`, `UmlNodes`, `SelectedUmlNode`/`IsUmlNodeMode`/`SelectUmlToolCommand`,
`StructureHeader`, hint + RaiseModes); `DiagramDocumentViewModel.AddUmlNode`; a `SelectedUmlNode`
branch in `DiagramView.axaml.cs.TryPlaceTool`.

**UI** — 3 `DataTemplate`s in `DiagramView.axaml`; the `StructureDropDown` in `MainWindow.axaml` +
its `WireDropdown` and the `UmlNodeKind` case in `ArmCommandFor`; 3 items in the `ToolMenus.axaml`
UML submenu; `ToolIcon.Package/Component/Deployment` glyphs.

## Verification

- `dotnet build Draw.slnx` clean; `dotnet test --solution Draw.slnx` green (incl. the reflection-based
  `NodeKindContractTests` serialization round-trip for the 3 new kinds).
- Visual (user, Windows/macOS): each places from the Structure dropdown, renders its decoration,
  edits its title, connects, and round-trips through save/reopen + SVG export.
</content>
