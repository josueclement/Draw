# Member & column editing UX — tidy panel + modal editor — Implementation plan

Branch: `feature/member-column-editing-ux` (off `main`).

The inspector's class-member and table-column list editors were cramped (2–3 stacked sub-rows,
hard-coded pixel widths, awkward add/remove placement, hidden reorder commands). This delivers two
editing surfaces so they can be compared: (a) a **tidy inline editor** in the right panel — one
aligned row per item (shared-size columns), header labels, a per-row toolbar (move up/down +
remove), and Add/Edit buttons in the section header; and (b) a spacious **modal editor** opened from
the panel's `Edit…` button or by double-clicking an empty/label area of the section. The dialog
edits a working copy of clones and applies on Save as a **single** undo step; Cancel discards.

Decisions: dialog hosted in the existing Carbon `ContentDialog` overlay; editor built as a
hand-rolled aligned grid (no `Avalonia.Controls.DataGrid` dependency). Panel inline edits remain
live (per-field, bracketed by `INodeEditContext`). The approved panel layout is single-line per
member, so `static`/`abstract`/operation-`params` editing now lives in the dialog (the panel keeps
column flags, which are core ER semantics).

## Steps

1. **Dialog VMs** (`ViewModels/`) — `ClassMemberEditRow`, `EntityColumnEditRow` (wrap working-copy
   clones); `ClassMembersEditorViewModel` (Fields/Operations split, enum→literals-only),
   `EntityColumnsEditorViewModel`. Each exposes Add/Remove/MoveUp/MoveDown + `BuildResult()`.
2. **Dialog views** (`Views/`) — `ClassMembersEditorView.axaml`, `EntityColumnsEditorView.axaml`:
   aligned grid via `Grid.IsSharedSizeScope` + `SharedSizeGroup`, header row, scrollable body.
3. **`Services/IDialogService.cs`** — `EditClassMembersAsync` / `EditEntityColumnsAsync`; impl builds
   the editor VM/view, shows it as the `ContentDialog` content (Save/Cancel), returns the edited list
   or `null`.
4. **`ClassNodeViewModel.ReplaceMembers` / `EntityNodeViewModel.ReplaceColumns`** — one-gesture swap
   (clone, rebuild collections, single undo via `BeginMemberEdit`/`EndMemberEdit`).
5. **`InspectorViewModel`** — inject `IDialogService`; `EditMembersCommand` / `EditColumnsCommand`
   call the service and apply the result.
6. **`MainWindow.axaml`** — replace the cramped member/column templates with the tidy aligned rows,
   header Add/Edit buttons, and per-row reorder/remove toolbar (binds the previously-unused
   `MoveMember*`/`MoveColumn*` commands). **`MainWindow.axaml.cs`** — `DoubleTapped` handlers on the
   member/column sections that open the dialog only on a non-interactive spot.
7. **Build** `dotnet build Draw.slnx` clean (nullable-as-error); then manual verification on
   Windows/macOS.

## Status

- [x] 1 Dialog VMs · [x] 2 Dialog views · [x] 3 DialogService · [x] 4 Node Replace · [x] 5 Inspector commands · [x] 6 Panel + double-tap · [x] 7 Build (clean)

Implemented; `dotnet build Draw.slnx` clean (0 warnings, 0 errors). Pending: manual verification on
Windows/macOS (no GUI under WSL2).

**Resolved (2026-06-09): the modal editor was dropped.** After reviewing both surfaces, the modal
`ContentDialog` member/column editor was **removed entirely** — it duplicated the inline inspector
editor, which is now the single editing surface. The brief experiment that widened Carbon's
`ContentDialog` to 750 (a local `ControlTheme` override) was reverted and the dialog-only editor
views/VMs (`ClassMembersEditorView`/`EntityColumnsEditorView` + their VMs and edit-row types) were
deleted. The inspector's own layout problems (panel max-width cap, scrollbar overlapping the fields /
row buttons) were fixed in its place — see
`documentation/plans/2026-06-09-inspector-panel-editing.md`.
