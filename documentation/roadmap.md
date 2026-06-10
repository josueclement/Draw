# Roadmap

Delivered MVP-first; each phase is independently runnable.

## Phase 1 — Editor foundation ✅

Solution scaffolding + `IHost`/DI/Fluent theme; JSON document model with save/open/new and
multi-document tabs; full editor UX (zoom/pan, grid + snap, marquee multi-select,
move/resize handles, copy via duplicate, keyboard shortcuts); the 7 basic shapes with
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

## Code-review remediation 🚧 (cross-cutting, in progress)

A full code-review pass (2026-06-10) produced a prioritized, impact-first refactor roadmap.
**Done so far:** Priority 1 — the regression test safety net (xUnit v3 / MTP over the pure-logic
layers) — and the correctness quick-wins, items **6a** (signature-parse validation via `TryParse`,
with the edit VMs reverting invalid inline edits) and **6b** (`ImageNode.Clone` deep-copies its byte
buffer). Still pending: the two big decompositions and the remaining hardening/dedup/service items.
The debt concentrates in two oversized files: `DiagramView.axaml.cs` (2032 lines —
monolithic pointer handlers over ~13 loose gesture-state fields acting as an implicit state machine)
and `DiagramDocumentViewModel.cs` (a 1540-line god VM with ~9 responsibilities). The plan recommends a
regression **test safety net first** — reintroducing one focused test project for the pure-logic
routing/parsing/serialization layers (tests were removed 2026-06-03) — then **phased** extraction of
the two giants (split mega-handlers, lift a gesture-state object, peel interaction controllers and
coordinator collaborators out of the VM), de-duplication (`ClassMemberViewModel`/`EntityColumnViewModel`
edit pattern + mirrored XAML templates; actor geometry shared between canvas and SVG), dialog-mechanism
consistency in `DialogService`, and correctness hardening (signature-parse validation, `ImageNode.Clone`
deep copy, centralized geometry epsilons, a schema-migration seam). Each item is labelled with effort and
risk. See `documentation/plans/2026-06-10-code-review-remediation.md`.
