# Quick-style palette swatch chrome removal

**Date:** 2026-06-11
**Status:** Done
**Area:** `src/Draw.App/Views/MainWindow.axaml` (Insert tab → Styles group)

## Problem
Each color swatch in the Insert → Styles quick-style palette was a plain `Button` wrapping a
colored `Border`. The `Button` inherited the default Fluent/Carbon `ControlTheme`, which painted a
gray background/border and a gray hover/press fill *around* the colored rounded rectangle — the
swatches looked like they sat inside "gray boxes".

## Fix
Added a dedicated flat `ControlTheme x:Key="SwatchButton"` scoped to the swatch `ItemsControl`'s
`Resources`, and applied it to the swatch `Button` via `Theme="{StaticResource SwatchButton}"`.

The theme:
- Transparent background and zero border in every state (normal / pointerover / pressed / disabled),
  so no gray chrome ever shows — only the colored rounded rectangle.
- Template is a single `PART_ContentPresenter`; the existing `Padding="2"` is honored, keeping the
  prior 26×26 hit area, 1px margin, and inner inset unchanged (the inset is now transparent).
- Subtle, color-free affordance: `scale(1.1)` on `:pointerover`, `scale(0.92)` on `:pressed`
  (`RenderTransformOrigin=50%,50%`, `ClipToBounds=False`).

Scope is isolated: only the 10 color swatches use this theme. "Reset" and "No fill" are
`ribbon:RibbonButton` and are untouched.

## Verification
- `dotnet build Draw.slnx` succeeds (only check runnable headless under WSL2).
- Manual (Windows/macOS): Insert tab → select a shape (group enabled only with a selection) →
  swatches show no gray box; flat at rest, subtle pop on hover, slight shrink on press; click still
  recolors the selection.
