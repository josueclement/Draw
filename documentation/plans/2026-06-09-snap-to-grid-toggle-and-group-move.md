# Snap-to-grid toggle + group-coherent move: Implementation plan

Branch: `feature/snap-toggle-and-group-move`. No separate design spec — small, self-contained.

## Problem

Shapes laid out with **Align/Distribute** lost their even spacing the moment the group was
moved. During a drag the whole selection translates by one delta (spacing preserved), but on
pointer release `SnapSelectionToGrid` snapped **each shape's top-left corner independently** to
the nearest grid line. `Distribute` deliberately produces even gaps that are *not* grid
multiples (and never re-snaps), so independent rounding pushed each shape onto a different grid
line and broke the layout (e.g. grid 10: x = 0, 53, 107, 160 → 0, 50, 110, 160 → gaps 50/60/50).

## Decision

Keep the grid (the visible background stays). Two changes:

1. **Snap a move as a single unit** so relative spacing is always preserved.
2. **Add an app-wide Snap-to-grid toggle** so the user can place freely when needed (e.g. while
   fine-tuning a distribution). The toggle governs *all* snapping through the existing
   `SnapEnabled` guard (move, resize, create, paste, connector waypoints/labels).

## Files touched

1. **`src/Draw.App/ViewModels/DiagramDocumentViewModel.cs`** — rewrite `SnapSelectionToGrid`:
   instead of snapping each node, derive **one** offset from the selection's bounding-box
   top-left (`min X`/`min Y` → `Point2D.SnappedToGrid`) and apply that delta to every selected
   node. A lone shape still lands on the grid (anchor = itself); a multi-shape group keeps its
   relative spacing. Reuses `SnappedToGrid` from `Draw.Diagramming/Geometry/SnapExtensions.cs`.

2. **`src/Draw.App/ViewModels/ShellViewModel.cs`** — inject `IOptions<EditorOptions>` and expose
   a bindable `bool SnapToGrid` backed by the shared `EditorOptions` singleton (get/set on
   `_editorOptions.SnapToGrid` + `OnPropertyChanged`). Because every tab's `SnapEnabled` reads
   that same singleton live, the toggle takes effect everywhere with no extra plumbing.

3. **`src/Draw.App/Views/MainWindow.axaml`** — add a `RibbonToggleButton` ("Snap to grid",
   `Icon=grid_four`) in the **View ▸ Appearance** group, `IsChecked="{Binding SnapToGrid,
   Mode=TwoWay}"`, mirroring the existing self-toggling "Properties" button.

## Notes / scope

- The visible grid background is **unchanged** (toggle = snapping only).
- Snap on resize/create/paste/connectors is **unchanged** in behaviour; it is simply gated by
  the same toggle (when off, the existing `if (!SnapEnabled) return;` guards short-circuit).
- The toggle is **session-only** — there is no settings-write mechanism in the app
  (`EditorOptions` is read from config at startup), so it resets to the default (on) on restart.
  Persisting across restarts would need new settings-save infrastructure; out of scope.

## Verification

No automated tests in this repo (removed 2026-06-03). `dotnet build Draw.slnx` is the gate
(nullable warnings are build errors) — passes with 0 warnings. WSL2 is headless, so verify
visually on Windows:

- Snap on: distribute 4 shapes → even gaps; select all + drag → gaps stay even; single shape
  still snaps to the grid.
- Toggle off (View ▸ Appearance): move/resize/create/paste land exactly where placed; a
  distributed group can be nudged by any amount with spacing preserved.
- Toggle reflects state across tabs; grid background stays visible throughout.
