# Roadmap

Delivered MVP-first; each phase is independently runnable.

## Phase 1 — Editor foundation ✅

Solution scaffolding + `IHost`/DI/Fluent theme; JSON document model with save/open/new and
multi-document tabs; full editor UX (zoom/pan, grid + snap, marquee multi-select,
move/resize handles, copy via duplicate, keyboard shortcuts); the 9 basic shapes with
text; per-shape styling + inspector; memento undo/redo; PNG + clipboard export.

## Phase 2 — Connectors ✅

`IConnectorRouter` with straight / orthogonal strategies and shape-boundary
attachment; the full UML relationship decoration set (open arrow, hollow/filled diamond,
hollow triangle, dashed lines for realization/dependency); editable source/center/target
labels; drag-from-node-to-node creation with preview; connector selection + inspector
(kind/route/labels/stroke); undo and orphan-pruning on node delete. Hardened via an
adversarial multi-dimension review pass (degenerate-geometry guards, non-destructive
connector rebuild, theme-aware decoration fills, save-state-aware dirty flag).

## Phase 3 — UML class diagrams ✅

Shared compartment/list node (`Class` / `Interface` / `Enum`); member editor (inline +
inspector) with free-text types + autocomplete; class relationships reuse Phase 2.

## Phase 4 — Use-case diagrams ✅ (current)

Actor, use-case, system boundary; association, include/extend, generalization.

## Phase 5 — ER diagrams + DB schema + vector export 🚧

`EntityNode` with columns (PK/FK/nullable/unique) and crow's-foot cardinality;
`Draw.Sql` with `ISqlDialect` for PostgreSQL / SQL Server / SQLite / MySQL,
ER→DDL (primary) and optional class→table mapping; `Draw.Export` adds SVG/PDF
(SkiaSharp).

**ER shapes ✅ (visual slice):** `EntityNode` + `EntityColumn` (name/type/PK/FK/nullable/unique),
mirroring the UML class node — a titled box over a flat column list with inline editing (`name: type PK`
grammar via `Draw.Diagramming/Er/ColumnSignature.cs`), context-menu flag toggles, and the shared
theme-aware styling. A new **Relationship** connector with **per-end crow's-foot cardinality**
(One/Many/ZeroOrOne/OneOrMany/ZeroOrMany — `Connector.Source/TargetCardinality`, rendered by
`ConnectorDecorationBuilder`). A dedicated **ER** ribbon group (Table + Relationship) and a *New ER
diagram* entry wiring `DiagramType.Er`. See `documentation/plans/2026-06-04-er-database-shapes.md`.
**Still pending:** `Draw.Sql` ER→DDL generation and `Draw.Export` SVG/PDF.

## Phase 6 — Mind maps 🚧 (pending visual verification)

A mind-map workflow on top of the existing canvas. Two new shape kinds — **Mind map topic** and
**Rounded topic** (`ShapeKind.MindMapTopic`/`MindMapTopicRounded`, rendered as plain/rounded
rectangles) — gain a special affordance: hovering shows a **`+` on each of the four sides** that
spawns a **linked child** on that side (inheriting the parent's shape + style, opening for inline
typing, one undo step), nudged clear of existing nodes. The branch connectors are **organic tapered
ribbons** — filled variable-width geometry, **thick near the central topic and tapering thinner with
tree depth and toward each child** (`RelationshipKind.MindMapBranch`; depth from
`MindMap/MindMapHierarchy`, widths from `MindMapBranchStyle`, outline from `TaperedStroke` — all
UI-agnostic + headless-tested; rendered as a filled `Path`, with SVG/PNG export parity). A new
**`DiagramType.MindMap`** with a *New Mind Map* command (File menu + Home ribbon) pre-seeds a central
topic; a **Mind map** ribbon group + Shift+S submenu arm the two tools. Branch taper is explicit
(the `MindMapBranch` kind), so ordinary connectors are unaffected. See
`documentation/plans/2026-06-14-mind-maps.md`.

### Refinement — drawable branch connector, status markers, fixes 🚧 (pending visual verification)

Follow-up making mind maps editable beyond the `+` button. The tapered branch is now a **dedicated
`RelationshipKind.MindMapBranch` connector** (replacing the former `Connector.IsMindMapBranch` bool;
no migration — the bool only existed on this unmerged branch): it has a palette tool (Mind map ribbon
group + Shift+C menu) so branches can be **drawn between any two shapes**, and its taper recomputes
when reconnected. Every node can carry **status markers** (`NodeMarker`: Todo / In progress / Done /
Stuck / Important / Idea / Question) — multiple, independent, stacked — rendered as coloured Phosphor
icon badges floating above the node's top-right, set via the right-click **Markers** menu and an
inspector toggle row. Fixes: the hover-`+` buttons sit outside the edge **and** grabbing a selected
connector's endpoint now wins the press over the button (the canvas handler runs `handledEventsToo`
and re-captures the pointer — ZIndex can't fix an event-routing conflict); thick branches near the
root render smooth (denser sampling + a round-joined ribbon edge); and connectors/branches now
**default to gray** (`ConnectorStyle.DefaultStrokeColor`, node outlines stay blue). See
`documentation/plans/2026-06-25-mind-map-improvements.md`.

## Connector editing — forced anchors, waypoints, movable labels ✅ (cross-cutting)

Direct in-canvas control over connector geometry on top of Phase 2: pin either endpoint anywhere on
a shape's outline (tracks move + resize), add/move/remove waypoints on straight + orthogonal
connectors (`Ctrl`+click / drag / `Alt`+click), and drag the source/center/target labels (with
`Alt`+click to reset). Reuses the selection-handle + `Ctrl`/`Alt`+click model and the memento undo.
See `documentation/plans/2026-06-03-connector-editing.md`.

Adds a fourth route style, **Rounded** — a smooth curve averaged from the bend points (rides the
midpoints between consecutive points; a gentle S-curve when there are none), rendered as true cubic
beziers. See `documentation/plans/2026-06-03-connector-rounded-route.md`.

A new connection is now **curved by default**: `AddConnector` creates it with the Rounded route and
auto-pins each end to the centre of its facing side (reusing the Space-connections math), so the
connector bows into a curve the instant two shapes are joined. See
`documentation/plans/2026-06-04-curved-connection-defaults.md`.

## UI shell — Ribbon, icons & theming ✅ (cross-cutting)

Replaces the top toolbar + left tool palette with a `Carbon.Avalonia.Desktop` **Ribbon**
(Home / Insert / View; per-category tool dropdowns); menu bar, inspector, tabs and status bar
stay. Icons come from `PhosphorIconsAvalonia`, with custom vector glyphs for the UML items it
lacks. Adopts Carbon's palette for the chrome and as the new default shape style (soft-grey fill,
accent-blue stroke, dark text). See `documentation/plans/2026-06-02-ui-ribbon-revamp.md`.

## UML member editing — canvas-first rapid entry ✅ (cross-cutting)

Adding members to a class/interface/enum is now keyboard-driven on the canvas: double-click a
compartment (or the hover `+` button) to add, `Enter` adds the next member of the same kind,
`Tab` / `Alt`+arrows navigate and reorder, and a right-click context menu covers
insert/move/visibility/delete. One undo step per member; the Inspector stays as the precise
editor. See `documentation/plans/2026-06-03-uml-member-editing-ux.md`.

## Align & distribute shapes ✅ (cross-cutting)

Tidy a group of shapes: align the selection (left/center/right/top/middle/bottom) to its bounding
box, and evenly distribute it (equal edge-to-edge gaps) horizontally/vertically. New **Arrange**
ribbon tab (Align dropdown + two Distribute buttons), `Ctrl+Shift+L/C/R/T/M/B` and `Ctrl+Shift+H/V`
shortcuts, and a right-click canvas menu (right-drag still pans). Pure geometry in
`Draw.Diagramming/Layout/ShapeArranger.cs`; one undo step per action; connectors re-route
automatically. See `documentation/plans/2026-06-04-align-distribute-shapes.md`.

## Align to reference 🚧 (cross-cutting, pending visual verification)

Relative alignment, the deferred follow-up to Align & distribute: capture a set of shapes as a fixed
**reference**, then line a later selection (the **movers**) up against the reference's combined
bounding box — so a loose shape snaps to the middle of a column, or to the midpoint of a two-shape
gap, without disturbing the reference. Movers **move as a block** (relative layout preserved);
multiple mover-sets can be aligned to the same **sticky** reference in turn (cleared on Esc / Clear
reference / bare empty-canvas click / new reference / deleting a reference shape). Purely additive —
the existing six Align + two Distribute actions are unchanged. A new **Reference** group on the
**Arrange** ribbon tab (*Set as reference*, an *Align to reference* dropdown, *Clear reference*) plus
the same three entries on the canvas context menu; reference shapes get a dashed amber overlay outline
and a top-of-canvas banner. Transient, per-tab state on `DiagramDocumentViewModel`; pure geometry
(`ShapeArranger.AlignToReference`); one undo step per align; connectors re-route automatically. See
`documentation/plans/2026-06-09-align-to-reference.md`.

## Snap-to-grid toggle + group-coherent move 🚧 (cross-cutting, pending visual verification)

Fixes distributed shapes losing their spacing when moved, and makes snapping optional. On
drag-release the selection now snaps **as a single unit** — one offset derived from the
bounding-box top-left applied to every node — so a lone shape still lands on the grid while a
multi-shape group keeps its relative spacing (Align/Distribute layouts no longer drift). A new
**Snap to grid** toggle in the **View ▸ Appearance** ribbon group turns snapping on/off app-wide
(backed by the shared `EditorOptions` singleton; governs move/resize/create/paste/connectors via
the existing `SnapEnabled` guard). The visible grid background is unaffected; the toggle is
session-only (resets to on at startup). See
`documentation/plans/2026-06-09-snap-to-grid-toggle-and-group-move.md`.

## Connection point spacing ✅ (cross-cutting)

Tidy the connectors landing on a shape: a one-shot **Space connections** action spreads the connectors
attached to the selected shape(s) evenly along each edge they touch (sides with ≥2 connections),
preserving their current order and pinning them as forced anchors so the layout sticks on move/resize.
A **Connections** group on the Arrange ribbon tab + a right-click menu item (the arrange menu now opens
with ≥1 shape selected). Pure geometry in `Draw.Diagramming/Layout/ConnectionDistributor.cs`; one undo
step per action; reuses the forced-anchor mechanism. See
`documentation/plans/2026-06-04-connection-point-spacing.md`.

The action now **force-pins every end** touching the selection, not just crowded sides: a lone end on a
side is centred on that edge so the whole arrangement is locked. See
`documentation/plans/2026-06-04-force-pin-on-arrange.md`.

## Image + SVG export 🚧 (cross-cutting, pending visual verification)

Shareable diagram export, replacing the zoom-and-grid-capturing **Export PNG…** with a **File ▸ Export**
submenu — **Export Image…** (PNG/JPEG) and **Export SVG…** — plus an upgraded **Copy as Image**. All
render the whole diagram **zoom-independently at 1:1**, containing **only shapes + connectors** (no grid,
no selection overlay), with a 16px margin; PNG/SVG are transparent, JPEG is white-backed. Both outputs
read `DiagramView`'s already-laid-out node/connector controls (resolved colours + class/entity layout for
free): raster does a guarded grid-free identity-transform `RenderTargetBitmap` render (`RenderContentBitmap`,
JPEG encoded via SkiaSharp), and **full-parity SVG** (`BuildSvgDocument`) walks the same controls — text
as real `<text>`, boxes/separators/images directly, and `Path` geometry re-emitted from source builders
(`ShapeSvgPathBuilder`/`SvgConnectorBuilder`) since Avalonia `StreamGeometry` isn't introspectable. This
delivers the `Draw.Export` SVG slice of Phase 5 (in `Draw.App`, no separate project; PDF/`Draw.Sql` still
pending). See `documentation/plans/2026-06-08-export-image-svg.md`.

A sibling **Merge connections** action (same Connections group + context menu) is the inverse: it
collapses every connector end touching the selected shape(s) onto the centre of the side it lands on,
so the two buttons fan out ↔ regroup a shape's connectors. See
`documentation/plans/2026-06-04-merge-connections.md`.

## Copy/paste + image support ✅ (cross-cutting)

Clipboard editing for diagram content plus embedded images. **Copy/Cut/Paste/Duplicate**
(`Ctrl+C`/`X`/`V`/`D`) of the selected nodes, automatically carrying any connector whose **both**
endpoints are selected; paste centres on the viewport and re-links cloned nodes with fresh ids (one undo
step). A new `ImageNode` embeds its bytes as base64 in the `.draw` file and is inserted by pasting a
bitmap, the **Insert image** picker, or drag-&-drop of an image file; copying a lone image also puts a
bitmap on the OS clipboard for other apps, and pasting an external bitmap creates an image node. Images
resize with a locked aspect ratio and behave as full nodes (connectors/align/distribute apply). Clipboard
access goes through `IClipboardService` (Avalonia 12 typed `DataFormat`); keyboard shortcuts are handled
in `DiagramView` so they don't hijack text editing. See
`documentation/plans/2026-06-04-copy-paste-and-images.md`.

## Shape stacking order (Z-order) ✅ (cross-cutting)

Control the front-to-back order of overlapping shapes: **Bring to Front / Bring Forward / Send
Backward / Send to Back** on the selection, via a new **Order** group on the Arrange ribbon tab,
`Ctrl+]`/`Ctrl+[` and `Ctrl+Shift+]`/`Ctrl+Shift+[` shortcuts, and the right-click canvas menu.
System boundaries stay in a reserved lower band (always behind ordinary shapes, reorderable among
themselves); connectors keep their dedicated top layer. Reuses the existing `NodeBase.ZIndex`
(already serialized + cloned for undo) bound to `Canvas.ZIndex`; pure reordering in
`Draw.Diagramming/Layout/ZOrderArranger.cs`; one undo step per action. See
`documentation/plans/2026-06-04-shape-z-order.md`.

## Quick style palette ✅ (cross-cutting)

A **Styles** group on the Home ribbon tab with an always-visible grid of 10 curated **pastel**
swatches plus **Reset to default** and **No fill** buttons. One click recolours the selection (all
nodes + a selected connector) with a coordinated fill + stroke + text combination. Swatches are
**theme-aware**: each carries a Light and a Dark variant, and styled elements recolour automatically
on theme toggle — generalising the single default-fill sentinel into ~10 named palette tokens
(`Style.PaletteId`, resolved at render time; additive, backward-compatible serialisation). Palette
data is UI-agnostic in `Draw.Diagramming/Styling/StylePalette.cs`; apply reuses the style-edit memento
undo. See `documentation/plans/2026-06-04-quick-style-palette.md`.

## JSON-configured keyboard shortcuts (vim/blender chords) 🚧 (cross-cutting, pending visual verification)

Replaces the two hard-coded shortcut sites (the `<Window.KeyBindings>` block and the `OnKeyDown`
clipboard switch) with a single **data-driven keymap**. One window-level `ChordInputDispatcher` reads a
merged keymap — baked-in defaults plus an optional user override at `%APPDATA%/Draw/keymap.json` (a
commented `keymap.example.json` is written on first run) — and handles **single gestures** (`Ctrl+S`)
and **multi-key chords** (`a s r` → arm rectangle, `a c a` → arm association). "Add" chords arm the
matching tool; other actions invoke immediately. A `KeymapActionRegistry` resolves action ids
(`tool.shape.*`, `align.*`, `file.save`, …, generated from the enums) to commands lazily against the
live `ActiveDocument`. Pending chords show in the status bar with a ~1 s idle timeout; the dispatcher is
suppressed while a text-entry surface has focus, so in-place editing keys are untouched. New code in
`src/Draw.App/Input/`; reuses the `IOptions<T>`/DI and `RecentFilesService` APPDATA/fallback patterns.
Rather than one chord per shape/connector, the defaults now bind **Shift+S** / **Shift+C** to open category-grouped tool menus (Standard/UML/Use case/ER submenus, ribbon icons + access keys; picking an item arms the tool) via `menu.shapes`/`menu.connectors`; the granular `tool.*` ids stay bindable. See `documentation/plans/2026-06-08-keyboard-chord-shortcuts.md`.

## Unsaved-changes warning dialog 🚧 (cross-cutting, pending visual verification)

Stops silent loss of work: a **Save / Don't Save / Cancel** warning (Carbon `Carbon.Avalonia.Desktop`
**ContentDialog**, an in-window overlay) now guards every path that discards a modified document —
tab close (`Ctrl+W` / tab X) *and* the window X / File ▸ Exit / quit, which previously did no check at
all. Dirty = `DiagramDocumentViewModel.IsModified`; on exit each modified document is prompted in turn
(Cancel aborts the whole quit). One `ShellViewModel.EnsureSavedBeforeDiscardAsync` helper backs both the
tab-close command and a new `TryCloseAllAsync()`; `MainWindow.OnClosing` cancels-then-async-confirms. The
dialog host is registered with `IContentDialogService`; `New`/`Open` are unaffected (they open new tabs).
See `documentation/plans/2026-06-08-unsaved-changes-dialog.md`.

## Canvas scrollbars + fit-to-content 🚧 (cross-cutting, pending visual verification)

Content-aware **scrollbars** on each document canvas plus a **Fit to content** command, so shapes
panned off-screen are discoverable and recoverable. Each bar appears only when content overflows that
axis (reserved gutter, no auto-hide); its range is the padded content extent (node bounds + connector
geometry) unioned with the current view, so the thumb reveals where shapes sit relative to the
viewport. **Fit to content** (View ribbon, a corner button where the bars meet, and `Ctrl+Shift+F`)
centres all content, zoom-capped at 100%. The hand-rolled unbounded pan/zoom is preserved — bars only
reflect it. Scrollbar geometry lives in `DiagramView` code-behind beside the grid/handle logic;
`DiagramDocumentViewModel.GetContentBounds()`/`FitToContentCommand` back it. See
`documentation/plans/2026-06-08-canvas-scrollbars.md`.

## Arrow-key nudge 🚧 (pending visual verification)

Move the current selection with the **arrow keys**: plain arrow = one grid cell (`GridSize`),
**Shift+arrow** = a 1px fine step. Selected nodes move together; a selected connector instead
shifts **all its bend points** so the whole route moves as a unit. A contiguous run of nudges on
the same selection **coalesces into one undo entry** (like a drag), and the move is exactly the
requested delta (no implicit grid snap), so the fine step stays meaningful with snap-to-grid on.
Handled in `DiagramView.OnKeyDown` (arrow keys are unbound in the keymap and bubble past the
suppressed-while-typing chord dispatcher); reuses `MoveSelectedBy`/`CaptureUndo`/`MarkModified` and
a new `ConnectorViewModel.MoveBendPointsBy`. See `documentation/plans/2026-06-09-arrow-key-nudge.md`.

## Connector marquee selection + multi-select & bulk styling 🚧 (cross-cutting, pending visual verification)

Connectors are now first-class for selection. The **marquee** grabs connectors whose line the box overlaps
(`MarqueeGeometry.IntersectsPolyline`, a Liang–Barsky segment/AABB test over the connector's flattened route),
alongside shapes. Selection is **unified**: **Shift+click** toggles any item (shape or connector) in/out and
**Shift+drag** is an additive marquee, so shapes and connectors can be co-selected; **Ctrl** is unchanged
(toggles shapes, still splits a connector on click). Connector selection moved from a single `SelectedConnector`
to the per-connector `IsSelected` flag plus `SelectedConnectors`; `SelectedConnector` is now a computed
"active connector" (the lone selected connector when no node is selected) that keeps the single-connector edit
handles/snap engaging only for a focused connector. The Inspector shows **shared editors** (kind, route, stroke
colour, thickness, and a new **dash** dropdown) for any connector selection and applies them to all selected,
with per-connector fields (cardinality, labels) shown only for a lone selection; the Quick-Style palette and
`DeleteSelected` likewise act on every selected connector in one undo step. See
`documentation/plans/2026-06-11-connector-marquee-and-multiselect.md`.

## Code-review remediation ✅ (cross-cutting, complete)

A full code-review pass (2026-06-10) produced a prioritized, impact-first refactor roadmap.
**Done:** Priority 1 — the regression test safety net (xUnit v3 / MTP over the pure-logic
layers); the correctness quick-wins, items **6a** (signature-parse validation via `TryParse`,
with the edit VMs reverting invalid inline edits) and **6b** (`ImageNode.Clone` deep-copies its byte
buffer); **2a** (the `DiagramView.axaml.cs` pointer handlers split into intention-named
`Begin*`/`Handle*`/`Finalize*` methods — pure extraction, no behavior change); and **3a** (the three
longest `DiagramDocumentViewModel` methods' pure cores lifted into testable `Draw.Diagramming.Layout`
helpers — `CloneArranger`, `ConnectionDistributor.PlanPinning`, `ZOrderArranger.ReorderInBands` — with
headless tests). **3b** is done — moving the VM's orchestration clusters behind
coordinator collaborators it composes (one branch per coordinator, clean-seam first), reaching the VM
through a shared `IDocumentEditContext` seam: `ClipboardCoordinator` (copy/cut/paste/duplicate +
image insertion + `PlaceClones`), `ConnectorSpacingCoordinator` (space/merge/pin), `ZOrderCoordinator`
(`ReorderSelected`) and `AlignmentCoordinator` (align/distribute + the reference subsystem) are all
extracted, dropping the VM from ~1480 to ~1050 lines while it stays the façade the view binds to (it
keeps the commands and selection-changed notifications). **4** (de-duplication) is done too — a generic
`EditableItemViewModelBase<TModel>` now owns the shared member/column edit/commit/cancel lifecycle and
its undo-capture contract (the two mirrored class-member XAML row templates collapsed into one shared
`ClassMemberRowTemplate` resource), and a framework-agnostic `ActorDimensions` is the single source of
the actor stick-figure proportions shared by the canvas and SVG render paths. The rest then landed:
the `DiagramView` decompositions **2b/2c/2d** (gesture state collapsed into `CanvasGestureState`,
interaction logic peeled into `ConnectorEditController`/`ViewportScrollController`, overlay
rebuild/reposition split), **3c** (the split-undo contract documented on the VM), **6c/6d** (named
`ShapeBoundary` tolerance constants + a `JsonDocumentSerializer.Migrate` forward-compat seam), **7**
(router magic-number constants, `RouteHelpers` anchor resolution, `KeymapService` logging), and
finally **5a/5b** — all dialogs routed through Carbon's `IContentDialogService` for one consistent
look, and the `FileDialogService` picker prologue folded into two helpers. The effort is complete;
only the optional secondary-VM decomposition pass (`ConnectorViewModel`, `ShellViewModel`,
`InspectorViewModel`) remains, deferred as off the critical path. **5c** is left as-is by design.
The debt concentrates in `DiagramView.axaml.cs` (2032 lines — monolithic pointer handlers over
~13 loose gesture-state fields acting as an implicit state machine); `DiagramDocumentViewModel.cs`
has since been reduced to ~1121 lines by peeling the clipboard/alignment/z-order/connector-spacing
coordinators out into focused collaborators. The plan recommends a
regression **test safety net first** — reintroducing one focused test project for the pure-logic
routing/parsing/serialization layers (tests were removed 2026-06-03) — then **phased** extraction of
the two giants (split mega-handlers, lift a gesture-state object, peel interaction controllers and
coordinator collaborators out of the VM), de-duplication (`ClassMemberViewModel`/`EntityColumnViewModel`
edit pattern + mirrored XAML templates; actor geometry shared between canvas and SVG), dialog-mechanism
consistency in `DialogService`, and correctness hardening (signature-parse validation, `ImageNode.Clone`
deep copy, centralized geometry epsilons, a schema-migration seam). Each item is labelled with effort and
risk. See `documentation/plans/2026-06-10-code-review-remediation.md`.

## Ribbon tab split + richer shape context menu 🚧 (cross-cutting, pending visual verification)

Declutters the ribbon for small screens and surfaces common view/style actions on the canvas. The
ribbon goes from 4 dense tabs to **9 focused ones** — **Export** (was Home ▸ Export) and **UML**,
**ER**, **Mind map**, **Styles** (each was an Insert group) become their own tabs, in the order
Home · Insert · UML · ER · Mind map · Styles · Arrange · View · Export; **Insert image** folds into
Insert ▸ **Common**; and **Arrange ▸ Order** collapses from four buttons into a single dropdown
mirroring the Align dropdown (shared command wired in `MainWindow.axaml.cs` `WireOrderDropdown`,
auto-closing on pick). The shape right-click menu gains three submenus reusing existing commands:
**Styles** (the full quick-palette swatches + Reset/No fill, directly under Icons) and, at the bottom,
**Zoom** (View ▸ Zoom) and **Appearance** (Toggle theme + checkable Properties / Snap to grid). The
menu builder (`DiagramView.BuildArrangeMenu`) now also takes the `ShellViewModel`, resolved from the
view's window DataContext, since theme/inspector/snap/styles live on the shell. Pure XAML/markup +
view-layer wiring; no model, VM-pattern, or package changes. See
`documentation/plans/2026-06-25-ribbon-tabs-and-context-menus.md`.

## Tool palette overlay (Shift+S / Shift+C) 🚧 (pending visual verification)

Replaces the static hierarchical `Shift+S`/`Shift+C` context menus with a **neovim-style centered
overlay**: a two-step **letter drill-down** — categories, then the chosen category's items (mnemonic
letter + icon + name) in a multi-column grid over a dim backdrop — that arms the existing toolbox tool
on pick (the arm/place/drag flow is unchanged). Letters are auto-derived per screen
(`Draw.Diagramming/Mnemonics/MnemonicAssigner.cs`, the only unit-tested piece); the catalog is a cached,
icon-resolving transcription of the retired `ToolMenus.axaml` (`App/Rendering/ToolPaletteCatalog.cs`)
that a control-free `ToolPaletteViewModel` drives. `ShowToolMenuCommand` now opens the palette directly;
keys are owned by the window (`MainWindow.OnGlobalKeyDown` — Esc backs out/closes, unmodified letters
navigate, and `Shift+S`/`Shift+C` while open switch family via the normal action path). The old
`ContextMenu` resource and its `WireToolMenus`/`ArmCommandFor`/`OnToolMenuRequested`/`OpenToolMenu` wiring
are removed. See `documentation/plans/2026-06-28-tool-palette-overlay.md`.

## Toggle grid visibility 🚧 (pending visual verification)

Lets the user hide/show the canvas grid from a **ribbon toggle** (View ▸ Appearance), an **Appearance**
context-menu checkbox, and a **`t g`** keyboard chord (listed in the Shift+H help overlay). Unlike the
app-wide, session-only "Snap to grid" flag, grid visibility is **per-document and persisted**: the
source of truth is a new `DiagramDocument.ShowGrid` (serialized as `showGrid`, additive with a `true`
default so pre-feature files open with the grid shown — no schema bump). The ribbon/menu bind through a
`ShellViewModel.ShowGrid` proxy onto the active document; the canvas repaints by reacting to the
document VM's `ShowGrid` change (the Zoom/Pan mechanism). Build clean + 379 tests green; GUI behaviour
pending visual verification. See `documentation/plans/2026-06-29-toggle-grid-visibility.md`.
