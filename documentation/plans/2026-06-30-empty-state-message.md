# Empty-state message + global Shift+H help

## Goal

Guide a user who has no document open. Two small, related changes:

1. Show a centered call-to-action in the canvas region when no tab is open, telling the user to
   create a new document or open one, with a pointer to the Shift+H help overlay.
2. Make **Shift+H** (the keyboard-shortcut help overlay) work with no document open — previously it
   was gated on having an active document, so it was unavailable exactly when a new user would want
   it.

No document-model changes; this is a presentation overlay plus one `CanExecute` removal, reusing
existing commands and the existing `HasActiveDocument` flag.

## Empty-state overlay

- A `Panel` in `EditorGrid` (`MainWindow.axaml`), at `Grid.Column="0"` (the canvas region, left of
  the Properties panel), declared after the `TabControl` so it draws on top.
- `IsVisible="{Binding !HasActiveDocument}"` — `HasActiveDocument` (`ShellViewModel`) already raises
  `PropertyChanged` from the `ActiveDocument` setter, so the overlay appears/disappears as tabs open
  and close. It collapses when a tab is active, so it never intercepts canvas input.
- Content: a "No document open" heading, an instruction line, **New document** / **Open…** buttons
  wired to the existing `NewCommand` / `OpenCommand`, and a "Press **Shift+H** to view keyboard
  shortcuts" hint. Theme-aware (`CarbonBackgroundBrush`); icons reuse `file_plus` / `folder_open`.

## Global Shift+H

- `ShellViewModel.ShowHelpCommand` drops its `() => HasActiveDocument` `CanExecute` guard (now
  always enabled, matching `NewCommand` / `OpenCommand`).
- The corresponding `ShowHelpCommand.NotifyCanExecuteChanged()` is removed from
  `NotifyDocumentCommands()` (dead once the command is no longer document-gated).
- Safe because the overlay (`ShortcutHelpView`) is a full-window overlay at the outer `Grid` level
  (not inside `DiagramView`), and `ShortcutHelpViewModel.Open()` builds its list purely from the
  keymap service — no dependency on `ActiveDocument`.

## Out of scope

- No "New mind map" button in the empty state (kept to "create a document or open one").
- No change to ribbon/inspector visibility when no document is open.

## Acceptance criteria

- With no document open: centered message with New document / Open buttons and the Shift+H hint
  appears in the canvas region; Properties panel still on the right.
- Pressing Shift+H with no tab open opens the shortcut-help overlay; Esc closes it.
- Creating or opening a document hides the message; closing all tabs brings it back.
- `dotnet build Draw.slnx` stays clean (nullable warnings are build errors).
