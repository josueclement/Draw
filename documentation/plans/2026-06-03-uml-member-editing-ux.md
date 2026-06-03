# UML member editing UX — canvas-first rapid entry — Implementation plan

Branch: `feature/uml-member-editing-ux` (off `main`).
Design: `documentation/specs/2026-06-03-uml-member-editing-ux-design.md`.

Canvas becomes the primary surface for adding/editing class/interface/enum members: double-click a
compartment to add, `Enter` adds the next of the same kind, `Tab`/`Alt+arrows` navigate/reorder,
right-click for a context menu. Inspector unchanged. One undo step per member; no auto-reclassify.

## Steps

1. **`ClassMemberViewModel.cs`** — add `IsNewlyAdded`, `IsEnumLiteral`, `_editSeed`; `BeginEdit`
   blank-vs-formatted seed; rework `CommitEdit` (kind-preserving `Apply`, lazy capture only when
   `changed && !IsNewlyAdded`, clear `IsNewlyAdded` once named).
2. **`ClassNodeViewModel.cs`** — add `InsertNewMember(kind, index)`, `Locate(member)`,
   `DiscardEmptyNewMembers()`. Keep existing add/move/remove for the Inspector.
3. **`DiagramView.axaml`** — `nodes:` namespace; per-template hover-`+` styles; wrap each
   compartment in a transparent `Panel` (`DoubleTapped` + `Tag`) with a `+` button; add per-row
   `ContextMenu` and `KeyDown` on the edit `TextBox`.
4. **`DiagramView.axaml.cs`** — `TryFocusMemberEditor`, `OnCompartmentDoubleTapped`,
   `OnAddField/OperationClick`, `OnMemberEditKeyDown`, context-menu `Click` handlers,
   `CommitAndAddNext` / `NavigateMember` / `AddMemberAndEdit`, `OwningNode`/`NodeOf`/`MemberOf`
   helpers; update `OnMemberDoubleTapped` (lazy undo), `OnMemberEditCommitted` (deferred discard),
   `EndEditing` (discard empties); right-click-member guard in `OnPointerPressed`.
5. **Build** `dotnet build Draw.slnx` clean (nullable-as-error); then manual verification on
   Windows/macOS.

## Status

- [x] 1 MemberVM · [x] 2 NodeVM · [x] 3 AXAML · [x] 4 Code-behind · [x] 5 Build (clean)

Implemented; build clean (0 errors; the 5 warnings are pre-existing `Watermark`-obsolete notices in
`MainWindow.axaml`, untouched). Pending: manual verification on Windows/macOS (no GUI under WSL2) —
see the design spec's behaviour list and the verification steps in the session plan.
