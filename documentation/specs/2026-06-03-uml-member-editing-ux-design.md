# UML member editing UX — canvas-first rapid entry: Design

Status: approved 2026-06-03. Branch: `feature/uml-member-editing-ux`.

Makes the canvas the primary, keyboard-driven surface for adding and editing members (fields,
operations, enum literals) on `Class` / `Interface` / `Enum` nodes. Before this, the only way to
*add* a member was the Inspector (`+ Field/Literal` / `+ Operation`, then fill separate boxes — one
round-trip per member); the canvas could only *edit existing* rows. Reuses the existing inline-edit
+ `MemberSignature` infrastructure; the Inspector is unchanged and remains the precise editor.

## 1. Behaviour (locked with the user)

- **Add on the canvas:** double-click empty space in a compartment adds a member there and starts
  editing it. A subtle `+` button appears per compartment on node hover (top-right) as the
  discoverable affordance.
- **Rapid sequential entry:** while editing a member, `Enter` commits it and opens a new empty
  member of the **same kind** directly below, ready to type. `Enter` on an empty row finishes.
  `Esc` (or clicking away) finishes and drops a trailing unnamed blank.
- **Keyboard:** `Tab` / `Shift+Tab` commit and move to the next / previous member (past the end ⇒
  add a new one); `Alt+↑` / `Alt+↓` reorder the current member within its compartment.
- **Context menu (right-click a row):** Insert below · Move up · Move down · Visibility ▸
  Public/Private/Protected/Package (hidden for enum literals) · Delete.
- **Undo:** one step per member — capture happens *before* each insert; a commit that changes
  nothing (navigation) or that names a just-added member does not add a step. Editing an existing
  row captures one step only if the text actually changed.
- **No auto-reclassify:** inline text editing never moves a row between the fields/operations
  compartments. A member's kind is fixed by how it was created; parsed text is mapped onto the
  existing kind. (This also fixes the prior latent bug where typing `()` set `Kind=Operation` while
  the row stayed in the fields compartment.)

## 2. Model / parsing

No change. `ClassNode` + `ClassMember` (`Draw.Model/Nodes`) and `MemberSignature`
(`Draw.Diagramming/Uml`) are reused as-is. Commit calls `MemberSignature.Parse` then coerces the
result onto the member's existing `Kind` rather than overwriting it.

## 3. View models

- `ClassMemberViewModel`: adds `IsNewlyAdded` (true from insert until first non-empty commit) and
  `IsEnumLiteral`. `BeginEdit` seeds blank for new members / formatted signature for existing.
  `CommitEdit` reworked: kind-preserving `Apply`, lazy undo capture (`changed && !IsNewlyAdded`),
  clears `IsNewlyAdded` once named.
- `ClassNodeViewModel`: adds `InsertNewMember(kind, index)` (captures undo once, inserts at a
  clamped index, begins edit, returns the VM for the View to focus), `Locate(member)`, and
  `DiscardEmptyNewMembers()` (silent removal of abandoned blanks). Existing `AddPrimaryMember` /
  `AddOperation` / `MoveMember` / `RemoveMember` / `CommitPendingEdits` are kept (Inspector still
  uses the first two).

## 4. View (`DiagramView.axaml` + code-behind)

- Each compartment is wrapped in a transparent `Panel` (`Tag` = `primary` / `operations`) with
  `DoubleTapped` for empty-space add, plus a hover `+` `Button` (`Classes="memberAdd"`, shown via a
  `Panel:pointerover` style in the template). Member rows gain a `ContextMenu` (binds only to the
  member's own properties — popup items can't reach `$parent` — actions are code-behind `Click`
  handlers) and `KeyDown` on the edit `TextBox`.
- Code-behind: `TryFocusMemberEditor` (posts at `Loaded` priority, finds the row's `TextBox` by
  `DataContext` in the visual tree), `OnCompartmentDoubleTapped`, `OnAddField/OperationClick`,
  `OnMemberEditKeyDown` (Enter/Tab/Alt-arrows), context-menu handlers, and a `LostFocus` deferred
  discard that keeps the session alive while focus stays on another member editor of the same node.
  `OnPointerPressed` gains a guard so right-clicking a member opens its menu instead of panning.

## 5. Out of scope

Drag-to-reorder; Inspector reorder buttons; changing a member's kind via inline text; member-level
keyboard shortcuts outside edit mode.
