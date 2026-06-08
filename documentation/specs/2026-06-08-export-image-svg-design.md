# Export as Image (PNG/JPEG) + SVG: Design

Status: approved 2026-06-08. Branch: `feature/export-image-svg`.

Part of Phase 5's vector-export goal, narrowed to what the user asked for: shareable diagram exports
that are **zoom-independent** and contain **only shapes + connectors**. The existing **Export PNG…**
renders the live on-screen viewport, so it captures the current zoom/pan and the grid — unusable as a
deliverable. This replaces it with a **File ▸ Export** submenu (**Export Image…**, **Export SVG…**)
and upgrades **Copy as Image** to match.

## 1. Goals / non-goals

In scope:

- **Export Image** to PNG or JPEG, **Export SVG**, and **Copy as Image** — all rendering the whole
  diagram at a fixed **1:1** scale (1 unit = 1px @ 96 DPI), independent of on-screen zoom.
- Exclude the **grid** and the **selection overlay** (handles/marquee/connect-preview).
- A fixed **16px** margin around the content bounding box.
- **PNG** transparent background; **JPEG** white background (no alpha); **SVG** transparent.
- **SVG full parity**: all 7 node kinds, embedded images, connector lines + UML/ER decorations +
  dashes, and every label as a real `<text>` element.

Out of scope (follow-ups):

- PDF export, and any separate `Draw.Export` project (the roadmap's earlier sketch). Kept in
  `Draw.App` alongside the existing rendering.
- Choosing a non-1:1 scale / DPI, exporting only the selection, configurable margin or background —
  decided against during the requirements interview.
- SVG font embedding: `<text>` references `font-family` by name (Inter); viewers without it
  substitute a similar font.

## 2. Why one laid-out tree drives both outputs

The node/connector view models already resolve all theme/palette colours to `SolidColorBrush`
(`NodeViewModelBase.Fill/Stroke/Foreground`, `ConnectorViewModel.Stroke/LabelForeground`), and the
class/entity compartment layout (header height, separator positions, per-member row positions) is
produced by Avalonia's layout engine at render time — it is **not** precomputed in the model. Re-deriving
that layout for SVG would risk divergence from the on-screen result. So both exports read the live,
already-arranged controls inside `DiagramView`'s `World` canvas. No second view is constructed and no
DataTemplate is duplicated (the templates carry inline event handlers bound to `DiagramView`'s
code-behind, which a shared `ResourceDictionary` could not resolve).

## 3. Raster path

`DiagramView.RenderContentBitmap(double scale = 1)`:

1. `bounds = DiagramDocumentViewModel.GetContentBounds()`; return `null` if no content. Inflate by
   `pad = 16`.
2. Save `World.RenderTransform`, `GridBackground.IsVisible`, `Overlay.IsVisible`.
3. Hide grid + overlay; set `World.RenderTransform = translate(-bounds.X+pad, -bounds.Y+pad)`
   (identity scale — zoom-independent).
4. `RenderTargetBitmap` of `ceil((bounds.W+2·pad)·scale) × ceil((bounds.H+2·pad)·scale)` at
   `96·scale` DPI; `bitmap.Render(World)`.
5. **`finally`**: restore transform + visibilities, `UpdateTransform()` / `UpdateHandles()`.

The mutate→render→restore block is synchronous and runs after the (async) file picker has returned,
so no intermediate frame reaches the screen.

`IImageExportService` shifts from "render a control" to "encode a bitmap":

- `enum ImageExportFormat { Png, Jpeg }`
- `Task SaveAsync(RenderTargetBitmap bmp, string path, ImageExportFormat fmt)` — PNG: `bmp.Save(stream)`.
  JPEG: copy pixels into an `SKBitmap`, draw onto a white `SKSurface` (flatten alpha), `SKImage.Encode(Jpeg, 92)`.
- `Task CopyToClipboardAsync(RenderTargetBitmap bmp)` — `clipboard.SetBitmapAsync(bmp)` (unchanged
  mechanism, new source bitmap).

## 4. SVG path

`DiagramView.BuildSvgDocument()` returns the SVG document string (or `null` when empty). It walks only
the Nodes and Connectors `ItemsControl` subtrees; the grid and overlay are siblings that are never
visited. Output is offset so `bounds` top-left + `pad` maps to the SVG origin; `width`/`height`/`viewBox`
= content + 2·pad; no background rect (transparent).

Per-visual conversion (positions via `TransformToVisual` into the content frame):

- `TextBlock` → `<text>` with `font-family/size/weight/style`, `fill` from `Foreground`, baseline
  adjusted from the run's box; `text-anchor` from alignment. Covers shape/use-case/actor/boundary
  labels, class & entity members, and connector labels.
- `Border` → `<rect>` (+ `rx` from `CornerRadius`) with `fill` (Background) and `stroke` +
  `stroke-width` (BorderBrush/BorderThickness) — class/entity/boundary bodies.
- compartment-separator `Rectangle` → `<line>`.
- `Image` → `<image>` with `data:` base64 URI from `ImageNode.Data` + `width`/`height`.
- `Path` → `<path d=…>`. Avalonia `StreamGeometry` cannot be read back, so the `d` is produced from
  the same source data, not the control's `Geometry`:
  - **Shapes / use-case**: `ShapeSvgPathBuilder.Build(ShapeKind, w, h, cornerRadius)` mirrors
    `ShapeGeometryBuilder` (rect, rounded-rect, ellipse, circle, note, and polygons via
    `ShapeOutline.GetPolygon`).
  - **Actor**: mirror `ActorGeometry`.
  - **Connector line**: polyline / cubic path from the route — `ConnectorViewModel.GetFlattenedPoints()`
    (already public, curve-sampled).
  - **Connector decorations**: `SvgConnectorBuilder` mirrors `ConnectorDecorationBuilder`, fed the
    end-decoration kind + tip + direction. This requires a small public surface on
    `ConnectorViewModel`: `SourceEndDecoration` / `TargetEndDecoration` (the resolved
    `ConnectorEndDecoration`) and `RouteStartDirection` / `RouteEndDirection`. Fill/hollow follows the
    same rule as `DecorationFill`.

Helpers live in `src/Draw.App/Rendering/`: `ShapeSvgPathBuilder`, `SvgConnectorBuilder`, a `SvgWriter`
(element/attribute formatting, invariant-culture numbers, XML/text escaping), and an `ArgbColor`/`Color`
→ `#rrggbb` + `fill-opacity` helper beside `StyleMappingExtensions`.

## 5. UI / wiring

- `MainWindow.axaml`: replace the `Export PNG…` `MenuItem` with an **E_xport** submenu containing
  **Export _Image…** (`ExportImageCommand`) and **Export _SVG…** (`ExportSvgCommand`); keep
  **_Copy as Image**.
- `ShellViewModel`: replace `ExportPngCommand`/`ExportPngRequested` with
  `ExportImageCommand`/`ExportImageRequested`; add `ExportSvgCommand`/`ExportSvgRequested`; keep
  `CopyImageCommand`/`CopyImageRequested`. All gated on `HasActiveDocument`; update
  `NotifyDocumentCommands()`.
- `IFileDialogService`: `PickSaveImageAsync` (PNG + JPEG `FilePickerFileType`s, default `.png`) and
  `PickSaveSvgAsync` (`*.svg`); remove `PickSavePngAsync`.
- `MainWindow.axaml.cs`:
  - `OnExportImageRequested` — pick path → `ImageExportFormat` from extension (`.jpg`/`.jpeg`→Jpeg,
    else Png) → `view.RenderContentBitmap()` → `_exporter.SaveAsync`.
  - `OnExportSvgRequested` — `view.BuildSvgDocument()` → `File.WriteAllTextAsync`.
  - `OnCopyImageRequested` — `view.RenderContentBitmap()` → `_exporter.CopyToClipboardAsync`.
  - Keep the `IOException`/`UnauthorizedAccessException` → `ShowErrorAsync` guard; `null` render
    (empty diagram) is a silent no-op.

## 6. Risks

- **Raster swap flicker** — mitigated by the synchronous post-picker block; verify on Windows.
- **SVG text baseline / wrapping fidelity** — single-line labels are exact; wrapped shape text is
  approximated by the run's measured box. Verify visually.
- **SVG breadth** — full parity across all node kinds + ER crow's-foot is the bulk of the work;
  driven by the per-visual walker + source-data path builders above.
