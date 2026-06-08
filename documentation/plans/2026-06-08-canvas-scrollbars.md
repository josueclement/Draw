# Canvas scrollbars + fit-to-content: Plan

Branch: `feature/canvas-scrollbars`.

Panning/zooming the canvas gave no indication that shapes existed outside the visible region (pan is
hand-rolled and unbounded). This adds **content-aware scrollbars** that appear only when content
overflows an axis, plus a **Fit to content** command — so off-screen shapes are always visible and
recoverable. The infinite-pan/zoom model is kept; scrollbars *reflect* position, they do not
constrain panning (no `ScrollViewer` — that would fight the 0×0-measuring world `Canvas`).

## Behaviour

- **Extent** = padded bounding box of all content (node `Bounds` + connector `GetFlattenedPoints`,
  so manual bends count) **unioned with the current visible world rect** (standard infinite-canvas:
  thumb shows position relative to content; panning into empty space pins the thumb toward content).
- **Visibility**: each bar shows only when content overflows that axis, in a reserved gutter
  (`AllowAutoHide="False"`); the `Auto` grid row/column collapses to 0 when hidden so the canvas
  reclaims the space.
- **Fit to content**: View ribbon Zoom group + a corner button where the bars meet (visible when
  both bars show) + `Ctrl+Shift+F`. Centres all content; zoom capped at 100% (never enlarges).

## Steps

1. **VM** — `DiagramDocumentViewModel`: `public Rect2D? GetContentBounds()` (null when no nodes;
   unions node `Bounds` then folds in every `ConnectorViewModel.GetFlattenedPoints()` point);
   `FitToContentCommand` (`RelayCommand`, CanExecute `Nodes.Count > 0`, registered in
   `RaiseSelectionChanged`); `FitToContent()` (inflate by `FitMargin`, `zoom = min(fitX, fitY, 1)`
   clamped, then centre via `PanX/PanY`).

2. **View layout** — `DiagramView.axaml`: wrap the existing `Viewport`/`World` in a
   `Grid ColumnDefinitions="*,Auto" RowDefinitions="*,Auto"`; add `VScroll` (R0C1), `HScroll` (R1C0)
   both `AllowAutoHide="False" IsVisible="False"`, and a corner `Button x:Name="FitCorner"` (R1C1)
   bound to `FitToContentCommand` with an `arrows_in` `PathIcon`.

3. **View logic** — `DiagramView.axaml.cs`: `UpdateScrollBars()` maps content∪view onto the bars and
   sets per-axis visibility + the corner; a `_updatingScrollBars` guard and the user-only `Scroll`
   event (`OnHScroll`/`OnVScroll` → set `PanX`/`PanY` from `_scrollOrigin*` + `e.NewValue`) prevent
   feedback loops. Called from `UpdateHandles()` (covers zoom/pan/selection/content moves/undo-redo)
   and the `Viewport.SizeChanged` handler (resize).

4. **Shortcut + ribbon** — `MainWindow.axaml`: `Fit to content` `RibbonButton` after Reset;
   `Ctrl+Shift+F` `KeyBinding` to `ActiveDocument.FitToContentCommand`.

## Status

Implemented; `dotnet build Draw.slnx` clean. Visual verification pending on Windows/macOS (WSL has
no display): bars appear/collapse with overflow, thumb tracks pan/zoom, scrollbar drag scrolls,
Fit (ribbon/corner/Ctrl+Shift+F) frames all shapes ≤100%, per-tab independence.
