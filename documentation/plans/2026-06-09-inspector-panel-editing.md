# Inspector-only member/column editing + right-panel layout fixes

**Date:** 2026-06-09
**Status:** Implemented (pending visual verification on Windows/macOS — no GUI under WSL2)
**Branch:** `feature/member-column-editing-ux`

## Problem

Member/operation/column editing existed on two surfaces: a modal `ContentDialog` editor and the
inline editor in the right-panel inspector. The dialog was decided against — editing lives in the
inspector. Removing the dialog also surfaced three inspector layout issues to fix.

## Changes

### 1. Removed the modal editor (the inspector inline editor stays)
- Deleted dialog-only files: `Views/ClassMembersEditorView.axaml`(+`.axaml.cs`),
  `Views/EntityColumnsEditorView.axaml`(+`.axaml.cs`), `ViewModels/ClassMembersEditorViewModel.cs`,
  `EntityColumnsEditorViewModel.cs`, `ClassMemberEditRow.cs`, `EntityColumnEditRow.cs`.
- Reverted the prior dialog-widening: deleted `Resources/ContentDialogTheme.axaml` and its
  `App.axaml` include (Carbon's `ContentDialog` is back to its stock 600px width).
- `Services/IDialogService.cs`: removed `EditClassMembersAsync`/`EditEntityColumnsAsync` (interface +
  impl) and the usings they needed. Kept `_contentDialog` + `ConfirmUnsavedAsync` (still uses the
  Carbon dialog) and `ShowErrorAsync`/`ConfirmAsync` (plain `Window`).
- `ViewModels/InspectorViewModel.cs`: removed `EditMembersCommand`/`EditColumnsCommand`,
  `EditMembersAsync`/`EditColumnsAsync`, the `_dialogs` field + `IDialogService` ctor param (it's a DI
  singleton — graph adapts), and the write-only `TypeSuggestions` (inline type-ahead reads
  `GetTypeSuggestions()` via the row VMs, unaffected).
- `Views/MainWindow.axaml(.cs)`: removed the two `Edit…` buttons, the `DoubleTapped` wiring on the
  members/columns panels, and the now-dead `OnClassMembersDoubleTapped` /`OnEntityColumnsDoubleTapped`
  / `OpenEditorOnDoubleTap` / `IsInteractiveSource` handlers.

### 2. Inspector resize cap removed + canvas floor
`MainWindow.axaml` right-panel `Grid.ColumnDefinitions`: dropped the inspector column's
`MaxWidth="560"` (kept `MinWidth="220"`) so it resizes freely; added `MinWidth="320"` to the canvas
(`*`) column so the splitter can't drag the canvas to nothing.

### 3 + 4. Scrollbar gutter (one fix, both bugs)
The single inspector `ScrollViewer` overlays its vertical scrollbar on the content's right edge
(Avalonia reserves no gutter), so it covered the top fill/stroke fields and the per-row remove
buttons. Fix: `Margin="0,0,14,0"` on the inner content `StackPanel` reserves a right gutter for the
scrollbar across every section. (Gutter width tunable.)

## Verification

- `dotnet build Draw.slnx` clean (0/0). Note: stale build-server file handles on the `/mnt/c` DrvFs
  mount can cause spurious `Access denied` on `obj`/`bin` DLLs — `dotnet build-server shutdown` then
  rebuild with `-nodeReuse:false` clears it.
- Manual (Windows/macOS): select a class node and an ER table — inline add/remove/move + type-ahead
  still work; no `Edit…` buttons; double-clicking the panel no longer opens a dialog. Drag the
  splitter well past 560px; the canvas stops at ~320px. The scrollbar no longer overlaps the
  fill/stroke fields or the row remove buttons. Unsaved-changes prompt still appears.
