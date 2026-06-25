# Mind-map improvements (Phase 6 refinement)

## Problem

Real use of the Phase 6 mind maps (unmerged `phase06-mind-maps` branch) surfaced four gaps:

1. The tapered branch only existed as a `Connector.IsMindMapBranch` bool set by the hover-`+` button —
   there was no way to *draw* a branch between two existing shapes, and reconnecting didn't re-taper.
2. No way to flag a topic's status (done / todo / stuck / …).
3. The hover-`+` buttons straddled the shape edge and stole the pointer from connector endpoints.
4. Thick branches near the root looked faceted ("multiple connected lines"), not smooth.

## Decisions

- **Dedicated connector kind.** Add `RelationshipKind.MindMapBranch` and **delete** the
  `IsMindMapBranch` bool. Safe with no migration: the bool was introduced in this same unmerged phase,
  so no persisted document depends on it. Branch-ness is now one source of truth (the `Kind`).
- **Markers on every node.** `List<NodeMarker>` on `NodeBase` (all node kinds). Set:
  Todo / InProgress / Done / Stuck / Important / Idea / Question. Multiple, independent, stacked.
  Rendered as coloured Phosphor icon badges floating outside the node's top-right.
- **Setting markers:** right-click **Markers** submenu (built into the existing arrange menu, so it
  works for all node kinds and the whole selection) + an inspector toggle row. Purely visual metadata.
- **Source = parent (thick) end** for a branch; depth/taper come from `MindMapHierarchy` over all
  `MindMapBranch` connectors, so a hand-drawn branch tapers from its root just like a `+`-created one.

## What changed

### Model (`Draw.Model`)
- `Connectors/RelationshipKind.cs`: `+ MindMapBranch = 10`.
- `Connectors/Connector.cs`: removed `IsMindMapBranch` (and its `Clone` copy).
- `Nodes/NodeMarker.cs` (new): the marker enum.
- `Nodes/NodeBase.cs`: `+ List<NodeMarker> Markers`, deep-copied in `CopyBaseTo` (so every node kind
  inherits markers + clone). Serialized as a JSON string array via the existing `JsonStringEnumConverter`.

### Pure logic (`Draw.Diagramming`)
- `MindMap/MindMapHierarchy.cs`: branch filter now `Kind == MindMapBranch` (was the bool).

### App (`Draw.App`)
- `Rendering/NodeMarkerVisuals.cs` (new): marker → filled Phosphor `Geometry` + colour + label
  (`IconService.CreateGeometry`), cached; `Order` defines badge/toggle order.
- `Rendering/ConnectorDecorationBuilder.cs`: explicit `MindMapBranch` → no end decorations.
- `ViewModels/ConnectorViewModel.cs`: `IsMindMapBranch => Kind == MindMapBranch`; flattening density
  raised (16 → 24 samples/segment) so a thick ribbon's outline is smooth.
- `ViewModels/NodeViewModelBase.cs`: `MarkerBadges` (ordered, set markers only), `HasAnyMarker`,
  `RaiseMarkersChanged`.
- `ViewModels/DiagramDocumentViewModel.cs`: `LinkNodesCore` drops the bool param (kind drives it);
  `CreateChildNode` and a hand-drawn `AddConnector(MindMapBranch)` both link with the kind and
  `RefreshMindMapBranches`; `+ SelectionHasMarker` / `ToggleNodeMarker` (one undo step over the selection).
- `ViewModels/InspectorViewModel.cs`: `MarkerToggles` (one `MarkerToggleViewModel` per marker), refreshed
  from the selection, applying via the document VM.
- `ViewModels/ToolboxViewModel.cs`: "Mind-map branch" connector tool; excluded from the UML dropdown header.
- `Views/DiagramView.axaml(.cs)`: a non-interactive **MarkersLayer** badge layer; the `+` buttons moved
  ~outside the edge (margin −18, ~2px overlap to keep hover unbroken); the branch `Path` (and its halo)
  stroked in its own colour with round joins/caps; a **Markers** checkable submenu in `BuildArrangeMenu`.
- `Views/MainWindow.axaml`: a **Branch** button in the Mind map ribbon group; a status-marker toggle
  row in the inspector's shared-node section.
- `Resources/ToolIcons.axaml` + `Resources/ToolMenus.axaml`: the `MindMapBranch` palette glyph + Shift+C
  menu entry.

## Tests & verification

- `tests/Draw.Model.Tests/JsonDocumentSerializerTests.cs`: round-trip `MindMapBranch` kind; round-trip
  node markers (order preserved); markers serialize as strings; marker clone independence.
- `tests/Draw.Diagramming.Tests/MindMapHierarchyTests.cs`: branch factory uses the kind.
- `dotnet build Draw.slnx` clean (nullable = error); `dotnet test --solution Draw.slnx` → 367 pass.
- GUI (Windows, user-run — not verifiable under WSL2): draw + reconnect a tapered branch; stack/toggle
  markers across node kinds and reload; `+` buttons clear of the edge; thick root branches smooth.

## Follow-up (same day): endpoint-grab priority + gray default

Two further fixes after testing the above:

- **Endpoint grab now wins over the hover `+` button.** Moving the buttons out wasn't enough: the
  endpoint handles live on the `IsHitTestVisible=False` overlay and are hit-tested geometrically in
  `OnPointerPressed`, but a hovered `+` `Button` consumed the press so that handler never ran. (ZIndex
  can't fix this — it's event routing, not render order.) `Views/DiagramView.axaml.cs` now subscribes
  `PointerPressed` with `handledEventsToo: true` and, for an already-handled press, only runs the
  selected-connector endpoint edit (which re-captures the pointer, suppressing the button's click);
  every other already-handled press is left to the child untouched.
- **Connectors default to gray.** `Styling/ConnectorStyle.cs` gets its own `DefaultStrokeColor`
  (`#9A9AA0`, the palette Gray swatch stroke) on its `Stroke` initializer — node outlines keep the
  shared blue `StrokeStyle.DefaultColor`. New connectors and (via `Fill="{Binding Stroke}"`) new
  mind-map branches are gray; quick-palette/custom colors and existing connectors are unaffected (no
  migration). Tests: a fresh connector is gray, a fresh node outline stays blue, and the gray
  round-trips (`tests/Draw.Model.Tests/JsonDocumentSerializerTests.cs`).

## Not done / follow-ups

- **Cross-node re-targeting** of an existing connector (drag an endpoint onto a *different* shape) is
  unsupported for *any* connector and was out of scope; draw a new branch instead. Easy follow-up.
- No JSON migration for old `isMindMapBranch:true` test files (unknown prop ignored on load → redraw).
- Catmull-Rom edge smoothing for the ribbon only if the stroke + denser-sampling fix proves insufficient
  on a real display.
