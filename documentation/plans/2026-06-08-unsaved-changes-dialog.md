# Unsaved-changes warning dialog (Carbon ContentDialog): Plan

Branch: `feature/unsaved-changes-dialog`.

The editor could lose work silently. The window **X** button, **File ▸ Exit**, and OS quit did **no**
unsaved-changes check at all (`MainWindow.OnExitClick` just called `Close()`; there was no
`Window.Closing` handler). The per-tab close (`Ctrl+W` / tab **X**) *did* prompt, but only **Yes/No**
(no way to back out of the close) via a hand-rolled `Window`, not the Carbon control the rest of the
UI uses.

This adds a single **Save / Don't Save / Cancel** warning, shown via the Carbon
`Carbon.Avalonia.Desktop` **ContentDialog**, on every path that would discard a modified document —
tab close *and* window/app exit — prompting **once per dirty document** on exit.

## Behaviour

- A document is "dirty" when `DiagramDocumentViewModel.IsModified` is true (computed against the
  on-save `_cleanSnapshot`). A brand-new, untouched document is **not** dirty → no prompt.
- **Save** writes the document (untitled ⇒ Save As picker); if the save fails or the picker is
  cancelled, the close is aborted and the document stays open.
- **Don't Save** discards and proceeds. **Cancel** (also Esc / overlay click) aborts the close.
- **Window / app exit:** every modified document is prompted in turn (the tab is activated first so
  the user sees which one). Cancel on any document aborts the whole quit; documents already saved
  this pass stay saved.
- **New / Open** are unaffected — they open *new tabs* and never discard the current document.

## Implementation

- **`Services/IDialogService.cs`** — new `enum UnsavedChangesChoice { Save, Discard, Cancel }` and
  `Task<UnsavedChangesChoice> ConfirmUnsavedAsync(string documentName)`. `DialogService` gains an
  `IContentDialogService` dependency and drives the registered Carbon `ContentDialog` host:
  `ShowAsync(d => { Title / Content / Primary="Save" / Secondary="Don't Save" / Close="Cancel" /
  DefaultButton=Primary })`, mapping `DialogResult` `Primary→Save`, `Secondary→Discard`, else `Cancel`.
  The existing `ShowErrorAsync` / `ConfirmAsync` (hand-rolled `Window`) are unchanged.
- **`ViewModels/ShellViewModel.cs`** — `HasUnsavedChanges` (any dirty doc); a single
  `EnsureSavedBeforeDiscardAsync(doc)` helper (returns false ⇒ caller must abort) used by both the
  rewritten `OnCloseDocumentAsync` (tab close) and the new `TryCloseAllAsync()` (loops every open
  document for app exit).
- **`Views/MainWindow.axaml`** — the root `DockPanel` is wrapped in a `Grid` whose second child is a
  full-window `<dialog:ContentDialog x:Name="DialogHost" />` overlay
  (`xmlns:dialog="using:Carbon.Avalonia.Desktop.Controls.ContentDialog"`).
- **`Views/MainWindow.axaml.cs`** — injects `IContentDialogService`, calls `RegisterHost(DialogHost)`
  once at construction, and overrides `OnClosing`: synchronous `e.Cancel = true` when there are
  unsaved changes, then `ConfirmAndCloseAsync()` runs the prompts and re-issues `Close()` (guarded by
  a `_forceClose` flag) once the user commits. `OnExitClick` still just calls `Close()` — now guarded.
- **`Hosting/ServiceCollectionExtensions.cs`** — registers
  `AddSingleton<IContentDialogService, ContentDialogService>()`. The Carbon `Services` namespace is
  pulled in by **type alias** (it also ships an `IFileDialogService`, which would clash with the app's).

## Verification

WSL has no display; build here, verify visually on Windows. `dotnet build Draw.slnx` passes
(0 warnings / 0 errors; nullable warnings are build errors).

1. New empty doc → close window → **no** prompt, exits.
2. Edit a saved doc → window **X** → dialog. Cancel keeps it open; Don't Save exits; Save writes then exits.
3. Edit an *untitled* doc → Save → Save As picker; cancel the picker ⇒ window stays open.
4. `Ctrl+W` / tab **X** on a dirty tab → same 3-button dialog, same outcomes.
5. Two+ dirty tabs → File ▸ Exit → prompts each in turn; Cancel on the 2nd aborts the quit.
6. The dialog is the Carbon-themed in-window overlay, not a separate OS window.

## Out of scope / follow-ups

- `IClassicDesktopStyleApplicationLifetime.ShutdownRequested` (e.g. macOS ⌘Q) is not separately
  hooked — the main-window `Closing` drives shutdown for the X / Exit cases. Add later if needed.
- `IDialogService.ConfirmAsync` (Yes/No) now has no callers; left in place intentionally.
