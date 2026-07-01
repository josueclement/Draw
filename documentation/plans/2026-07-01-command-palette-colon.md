# `:` command palette overlay + rebind style picker to Shift+T — Implementation plan

Branch: `feature/command-palette-and-shift-t`. Turns the vim `:` command line introduced in
`documentation/plans/2026-06-30-vim-mode-shortcuts.md` into a discoverable overlay palette matching the
Shift+S/C/I/Y/A/H surfaces, and moves the style picker's shortcut from Shift+Y to the easier Shift+T.

## Context

The editor already understood the vim `:` commands `:w` / `:q` / `:wq` / `:qa` (with a trailing `!` to
force) via `Draw.App/Input/VimExCommand.cs`, but rendered them as a thin **text bar docked at the bottom
of the window** (`CommandLineViewModel` + `CommandLineBar`/`CommandLineBox`). Every other quick-access
surface is a **centered overlay card** implementing `IOverlayPalette` (at most one open at a time,
orchestrated by `ShellViewModel.OpenExclusive`). This change surfaces the `:` commands the same way.

## Decisions

- **Type + live list** (agreed with the user). The palette keeps the vim typing model — a focused input
  prefilled `:`, Enter to run — and adds a **live-filtered list** of the commands with descriptions for
  discoverability. Not letter-mnemonic picking (couldn't express `:q!` / `:wq` / the `!` flag).
- **Existing four commands only.** `:w`, `:q`, `:wq`, `:qa` (+ `!`). No new command vocabulary; the
  parser (`VimExCommand`) is unchanged and remains the source of truth for what a typed command means.
- **Replace the bottom bar.** One consistent `:` experience via the overlay; the old `CommandLineBar` /
  `CommandLineBox` and `CommandLineViewModel` are removed.
- **Execution stays in the window.** `:qa` / `:qa!` must call `Window.Close()`, so the palette does not
  own execution: it raises `RunRequested(text)` (mirroring the shell's existing `ExportImageRequested`
  idiom) and `MainWindow` runs it through the unchanged `ExecuteCommandLineAsync` / `QuitAllAsync`.
- **Rebind Shift+Y → Shift+T** for `menu.styles` (full replace, no alias). `Shift+T` was free (only
  `Ctrl+Shift+T` = `align.top` existed).

## Design

- **`CommandPaletteViewModel : ViewModelBase, IOverlayPalette`** (`ViewModels/`, replacing the deleted
  `Input/CommandLineViewModel.cs`). Holds the four immutable `CommandPaletteEntry(Syntax, Description)`
  rows and an `ObservableCollection<CommandPaletteEntry> FilteredEntries` rebuilt whenever `Text`
  changes: prefix-match the typed word (leading `:` and trailing `!` stripped) against each syntax, so
  `:` shows all, `:w` shows `:w`/`:wq`, `:q` shows `:q`/`:qa`. `Open()` prefills `:`; `Close()` clears;
  `Back()` closes; `HandleLetter` swallows while open (real input comes through the focused box, not the
  window's overlay letter route). `Run(text)` raises `RunRequested` without closing (the window's handler
  closes first, so the captured text survives the clear).
- **`CommandPaletteView.axaml` (+ `.cs`)** mirrors `StylePickerView` chrome (dim `#80000000` backdrop →
  dismiss, centered `CarbonSurfaceBrush` card, heading + hint). Hosts a monospace `TextBox` bound to
  `Text`; its `KeyDown` runs on Enter / closes on Esc. The command list is an `ItemsControl` over
  `FilteredEntries`, each row a button that runs its command. `FocusInput()` is called by the window on
  open (deferred a layout pass, as the old bar did).
- **`ShellViewModel`** gains the injected `CommandPalette`, adds it to `_overlays` + `ActiveOverlay`, and
  exposes `OpenCommandPalette()` (`OpenExclusive`, so opening `:` closes any other overlay). Registered
  as a singleton in `ServiceCollectionExtensions`.
- **`MainWindow`** drops the bottom-bar XAML and the `OnCommandLineKeyDown` / `EndCommandLine`
  code-behind; `:` (matched on the typed character, layout-independent) now opens the palette; the window
  subscribes to `RunRequested` (run) and to the palette's `IsOpen` going false (return focus to the
  canvas).
- **`DefaultKeymap`**: `Shift+Y` → `Shift+T` for `menu.styles`, plus the matching `ExampleJsonc` comment.
  `ShortcutHelpViewModel` reads `menu.styles` from the live keymap, so the Shift+H help overlay reflects
  the new binding automatically (no hardcoded literal).

No model/serialization change. No change to `VimExCommand` parsing or the four commands' behaviour.

## Acceptance criteria

- `:` opens a centered palette card, input prefilled `:`, all four commands listed; canvas shortcuts are
  suppressed while typing. Typing filters the list (`:w` → `:w`/`:wq`; `:q` → `:q`/`:qa`).
- Enter runs the typed command (incl. `:q!` / `:qa!`); clicking a row runs that command; Esc and a
  backdrop click both close and return focus to the canvas. Opening another overlay closes the palette,
  and `:` is ignored while another overlay is open. The old bottom bar is gone.
- `Shift+T` opens the style picker; `Shift+Y` does nothing; the Shift+H overlay shows `Shift+T` for the
  style picker.

## Status

Implemented on `feature/command-palette-and-shift-t`; build clean, 421 tests green. UI verification is
manual (Avalonia GUI, not covered by the headless Model/Diagramming suite, and the pure `VimExCommand`
parser is unchanged). Pending merge.
