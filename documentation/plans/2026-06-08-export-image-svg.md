# Export as Image (PNG/JPEG) + SVG: Plan

Branch: `feature/export-image-svg`.

The only diagram export today is **Export PNG…**, which renders the live `Viewport`
(`view.ExportTarget`) with `RenderTargetBitmap`. That bakes in the current **zoom/pan** and the
**grid**, which is wrong for a shareable export. This replaces it with a **File ▸ Export** submenu
offering **Export Image…** (PNG/JPEG) and **Export SVG…**, both rendered **zoom-independently at 1:1**
and containing **only shapes + connectors** (no grid, no selection overlay). **Copy as Image** adopts
the same renderer.

## Behaviour

- **Content only, fixed scale.** Output covers the whole diagram (selection ignored) at 1 diagram
  unit = 1px (96 DPI), regardless of on-screen zoom. Extent = `GetContentBounds()` inflated by a
  fixed **16px** margin.
- **No grid, no handles.** The grid rectangle and the selection/marquee overlay are excluded.
- **Image**: PNG (transparent) and JPEG (white background — no alpha). Format chosen by the picked
  file extension.
- **SVG**: full vector parity — every node kind (basic shapes, UML class, ER entity, actor,
  use-case, system boundary, embedded images), connectors with arrowheads / crow's-foot, dashes,
  and all labels as real `<text>` elements.

## Approach — one laid-out tree, two outputs

Both outputs read the already-laid-out node/connector controls in `DiagramView`'s `World` canvas, so
resolved theme colours (`SolidColorBrush`) and composite layout (class/entity compartments, member
rows) come for free and match the app exactly. No template duplication, no second view.

- **Raster + clipboard** — `DiagramView.RenderContentBitmap(double scale = 1)`: a guarded,
  synchronous swap on `World` (hide `GridBackground` + `Overlay`, set `RenderTransform` to
  `translate(-min.X+pad, -min.Y+pad)` at identity scale), `RenderTargetBitmap` sized to
  `content + 2·pad`, `Render(World)`, then restore in `finally` (`UpdateTransform()`/`UpdateHandles()`).
  Runs in one sync block *after* the file picker returns, so the screen never repaints the
  intermediate state. `null` when the diagram is empty.

- **SVG** — `DiagramView.BuildSvgDocument()`: walks only the **Nodes** and **Connectors**
  `ItemsControl` subtrees (grid/overlay never visited, so no mutation). Per visual:
  `TextBlock`→`<text>`, `Border`→`<rect>` (rx = corner radius), separator `Rectangle`→`<line>`,
  `Image`→`<image>` (base64 data URI from `ImageNode.Data`), positions via
  `TransformToVisual(nodesContainer)`. `Path` geometry is **re-emitted from source data** (Avalonia
  `StreamGeometry` is not introspectable) via new SVG path builders.

## Dependency

`SkiaSharp` 3.119.4 (pinned to Avalonia.Skia 12.0.4's transitive version) — JPEG encoder only.
Avalonia's `Bitmap.Save` is PNG-only. PNG keeps using Avalonia.

## Steps

1. **Dependency** — `Directory.Packages.props` + `Draw.App.csproj`: add `SkiaSharp`. ✅
2. **Raster** — `IImageExportService`: `enum ImageExportFormat { Png, Jpeg }`;
   `SaveAsync(RenderTargetBitmap, path, fmt)` (PNG via `bitmap.Save`; JPEG via SkiaSharp,
   white-composited); `CopyToClipboardAsync(RenderTargetBitmap)`. `DiagramView.RenderContentBitmap`.
3. **Dialogs** — `IFileDialogService`: `PickSaveImageAsync` (PNG+JPEG types, default `.png`),
   `PickSaveSvgAsync` (`*.svg`); drop `PickSavePngAsync`.
4. **Commands/menu** — `ShellViewModel`: `ExportImageCommand`/`ExportSvgCommand` (+ events), keep
   `CopyImageCommand`; `MainWindow.axaml` Export submenu; `MainWindow.axaml.cs` handlers
   (image: pick→format-from-ext→render→save; svg: build→`File.WriteAllTextAsync`; copy: render→clipboard).
5. **SVG** — `DiagramView.BuildSvgDocument()` walker + `Rendering/` helpers
   (`ShapeSvgPathBuilder`, `SvgConnectorBuilder`, `SvgWriter`, ARGB→hex). Expose the route
   directions + end-decoration kinds the writer needs on `ConnectorViewModel`.

## Status

Implemented; `dotnet build Draw.slnx` clean (0 warnings/errors). Visual verification pending on
Windows/macOS (WSL has no display).

## Verification (Windows — WSL has no display)

Diagram with several shape kinds + a UML class + ER entity + actor + use-case + boundary + an image +
connectors with decorations/labels:

- Export Image PNG at multiple zoom levels → identical output, no grid, transparent.
- Export Image JPEG → white background, no grid.
- Export SVG → opens in a browser; all node kinds, text, dashes, decorations, image render and match
  the app; `viewBox` fits content + 16px.
- Copy as Image → paste externally → grid-free, zoom-independent.
- Empty diagram → no crash, no file written.
- `dotnet build Draw.slnx` clean (nullable = error).
