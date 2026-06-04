# Copy/paste (shapes + connectors) and image support: Design

Status: approved 2026-06-04. Branch: `feature/copy-paste-and-images`.

A cross-cutting editor enhancement delivering two related capabilities in one feature:

1. **Clipboard editing** — Copy / Cut / Paste / Duplicate of the selected nodes, automatically carrying
   any connector whose **both** endpoints are in the selection (so "two connected shapes" copy together
   with their connector).
2. **Image nodes** — a new `ImageNode` that displays a raster bitmap on the canvas, inserted by pasting a
   bitmap, picking a file, or dropping a file, with full clipboard interop with other applications.

It reuses the existing `NodeBase.Clone()` / `Connector` model, the polymorphic `IDocumentSerializer`, the
whole-document memento undo, the per-tab `DiagramDocumentViewModel`, and the service-abstraction pattern
(`IImageExportService`, `IFileDialogService`, …).

## 1. Goals / non-goals

In scope:

- Copy/Cut/Paste/Duplicate of every node kind (shape, class, actor, use-case, boundary, image), one undo
  step each. A copied selection carries a connector only when both its endpoints are also copied.
- Paste places content centred on the visible viewport; Duplicate offsets in place. Pasted/duplicated
  content becomes the new selection. Pasted nodes get fresh GUIDs (connectors are re-linked to them).
- A new `ImageNode` with bytes **embedded** (base64) in the `.draw` JSON, so a document stays
  self-contained. Insert via clipboard paste, a file picker, or drag-&-drop of an image file.
- Full clipboard interop: pasting an external bitmap creates an image node; copying a single image node
  also puts a bitmap on the OS clipboard for other apps.
- Images resize with a **locked aspect ratio** (they can't be distorted); they are full nodes, so
  connectors attach to them and align/distribute/space apply.

Out of scope (noted as follow-ups):

- Pasting plain text as a label or shape; SVG / vector clipboard formats.
- Image cropping, filters, or rotation; de-duplicating repeated embedded images by content hash.
- A model-schema migration — `ImageNode` is purely additive (a new `$type` discriminator); older
  documents are unaffected and forward-compatible.

## 2. Clipboard model

- **Payload** — a throwaway `DiagramDocument` is built from clones of the selected nodes plus the
  included connectors and serialised with the existing `JsonDocumentSerializer`. It is written to the OS
  clipboard under a custom application format, `DataFormat.CreateStringApplicationFormat("draw-clip")`
  (Avalonia 12 typed clipboard API). This gives cross-tab and cross-instance paste with full fidelity
  (styles, labels, anchors, image bytes), and never pollutes the plain-text clipboard.
- **Paste resolution order** — read the custom format first (full fidelity); if absent, fall back to a
  bitmap (`IClipboard.TryGetBitmapAsync`) which becomes a new `ImageNode`. Anything else is ignored.
- **Connector inclusion** — when copying, a connector is included only if both `SourceNodeId` and
  `TargetNodeId` are in the copied node set. Dangling connectors are dropped. (Node selection and
  connector selection are mutually exclusive in the editor, so a lone connector is never "copied"; Copy
  operates on the node selection and is a no-op when no node is selected.)
- **Id remapping** — on paste/duplicate each cloned node is assigned a fresh `Guid`; a map from old to
  new id rewrites the cloned connectors' endpoints (and each connector gets a fresh `Guid` too). Pasted
  nodes get top `ZIndex` so they land in front.
- **Placement** — Paste centres the bounding box of the pasted content on the current viewport centre
  (world space, derived from `PanX`/`PanY`/`Zoom` + the viewport pixel size the view pushes into the VM).
  Duplicate offsets by a small fixed delta. Both honour grid snapping if enabled.

## 3. Image model & rendering

- `ImageNode : NodeBase` holds `byte[] Data` (original encoded bytes — `System.Text.Json` writes a
  `byte[]` as base64 with no custom converter), a `string Format` (informational, e.g. `png`/`jpeg`),
  and native `PixelWidth`/`PixelHeight` (used for initial sizing and aspect-lock). `Clone()` copies these
  plus the base members.
- `ImageNodeViewModel : NodeViewModelBase, IDisposable` decodes the bytes once into an
  `Avalonia.Media.Imaging.Bitmap` exposed as `Image`. `Bitmap` is part of the base Avalonia media stack
  (not `Avalonia.Controls`), consistent with view models already exposing `IBrush`/`FontFamily`. The VM
  reports `BoundaryKind = Rectangle` (rectangular connector attachment), no inline label, and
  `LocksAspectRatio = true`. Decoded bitmaps are disposed when node view models are rebuilt (undo/redo,
  load) and when the tab closes, so native/GPU resources don't leak.
- A `DataTemplate` for `ImageNodeViewModel` renders an `<Image Source="{Binding Image}" Stretch="Fill"/>`
  sized to the node bounds, plus the same dashed selection rectangle the other node templates use.

## 4. Layer placement

- `ImageNode` lives in `Draw.Model` (framework-agnostic; just bytes + ints). `IClipboardService` and
  `ImageNodeViewModel` live in `Draw.App` (they touch Avalonia clipboard / `Bitmap`).
- `DiagramDocumentViewModel` owns the operations (`CopySelectionAsync`, `CutSelectionAsync`, `PasteAsync`,
  `DuplicateSelection`, `AddImageNode`) and exposes `HasNodeSelection`; it receives the viewport size from
  the view and gets `IClipboardService` through `DiagramDocumentViewModelFactory`.
- `ShellViewModel` exposes Copy/Cut/Paste/Duplicate/InsertImage commands (for the menu + ribbon),
  delegating to `ActiveDocument` — mirroring how `DeleteCommand` works today.

## 5. Interaction notes

- **Keyboard** (`Ctrl+C`/`X`/`V`/`D`) is handled in `DiagramView.OnKeyDown`, after its existing
  `e.Source is TextBox` guard, so the shortcuts edit text normally while a label/member editor is focused
  and only act on the diagram otherwise — exactly how `Delete` already behaves. The menu items show the
  gestures as display-only `InputGesture` hints (no second handler).
- **Enable/disable** — Copy/Cut/Duplicate require ≥1 selected node; Paste is always enabled when a
  document is open (it silently no-ops if the clipboard holds nothing usable — probing clipboard formats
  on every `CanExecute` isn't worth it).
- **Insert image** — a ribbon button + menu item open an image file picker (PNG/JPG/BMP/GIF) and place the
  image at the viewport centre. **Drag-&-drop** of image files onto the canvas places each at the drop
  point. Both decode the bytes to size the node at native pixels, capped to fit the viewport.
- **Resize** — image corner/edge handles preserve the original aspect ratio; everything else (min-size
  clamp, grid snap on release) is unchanged.

## 6. Risks

- External bitmap interop is platform-dependent (the existing `CopyPngToClipboardAsync` carries the same
  caveat). Targeted at Windows; verified there. If a single `DataObject` can't expose both the custom
  string and an external bitmap, the lone-image copy uses `SetBitmapAsync` and mixed selections stay
  Draw-only — kept explicit, never silently dropped.
- Embedding image bytes inflates whole-document memento snapshots (undo history) and `.draw` size.
  Accepted as the cost of self-contained documents.
- WSL2 is headless: build verifies compilation only; all visual/interaction checks run on Windows.
