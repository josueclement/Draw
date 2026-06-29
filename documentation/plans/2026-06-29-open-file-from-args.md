# Open `.draw` files from launch arguments (+ no blank startup tab)

Status: 🚧 implemented, pending visual verification on Windows (WSL2 can't render the GUI).

## Context

Double-clicking a `.draw` file (or otherwise launching the app with a file path) did not open it.
`Program.Main(string[] args)` forwarded `args` to `Host.CreateApplicationBuilder` and
`StartWithClassicDesktopLifetime(args)`, but nothing in the app ever read them — Avalonia stored them
on `IClassicDesktopStyleApplicationLifetime.Args` and `App.OnFrameworkInitializationCompleted()`
ignored that property, so a launch file path was always dropped.

Two things are needed for an OS double-click; this work does **only the first**:

1. **In-app handling** (this change): read the path(s) the app was launched with and open them.
2. **OS-level file association** (out of scope, by request): registering `.draw` → Draw so the OS
   *passes* the path. Done manually by the user on Windows/Linux ("Open with → Always"); no registry /
   `.desktop` / `Info.plist` / packaging changes here.

The user also wanted the app to never auto-open a blank "New" tab on startup — even when launched with
no file.

## Decisions

- **Scope:** code only — no OS file-association, registry, or packaging changes.
- **Platforms:** Windows, macOS, Linux. Windows/Linux receive the path via `desktop.Args`; macOS via
  the `IActivatableLifetime` file-activation event (never argv).
- **Startup tab:** removed entirely. Bare launch → empty window (zero tabs); launch-with-file → only
  that file.
- **Already-running instance:** a new instance opens per double-click. No single-instance / IPC.
- **Testing:** manual on Windows (repo has no App/UI test project by design); build + the pure-logic
  test suite gate the rest.

## Implementation

### `src/Draw.App/ViewModels/ShellViewModel.cs`

- Removed the `OnNew();` call at the end of the constructor (the `OnNew` method, `NewCommand`, and
  `NewMindMapCommand` stay — they back the New buttons).
- Added a public `OpenFilesAsync(IReadOnlyList<string> paths)` that loops over the existing private
  `OpenPathAsync`, reusing its load → deserialize → dedupe → add-tab → recents → error-dialog path.
  Multiple paths → multiple tabs, last one active; an already-open file re-activates; a corrupt file
  shows the standard "Could not open file" dialog.
- Added `using System.Collections.Generic;` (no implicit usings in this repo).

### `src/Draw.App/App.axaml.cs`

In `OnFrameworkInitializationCompleted()`, after assigning `desktop.MainWindow`:

- **Windows/Linux:** `OpenStartupFiles(desktop.Args)` — keeps only entries where `File.Exists`, so
  stray host-configuration switches never raise a spurious error dialog (a real-but-corrupt `.draw`
  still surfaces its dialog).
- **macOS:** subscribes to `IActivatableLifetime.Activated` (resolved via
  `this.TryGetFeature<IActivatableLifetime>()`); on a `FileActivatedEventArgs`, maps each
  `IStorageItem` through `TryGetLocalPath()`.
- Both paths funnel into `OpenFiles(...)`, which resolves the singleton `ShellViewModel` from DI (the
  same instance the window binds to) and posts `OpenFilesAsync` onto `Dispatcher.UIThread` so the
  document-collection mutation runs once the UI loop is live.
- On Windows/Linux `TryGetFeature<IActivatableLifetime>()` returns null (macOS-only feature), so the
  event is simply not subscribed — no duplicate opens.

Avalonia 12.0.4 API used: `IActivatableLifetime` / `ActivatedEventArgs` / `FileActivatedEventArgs`
(`Files` is `IReadOnlyList<IStorageItem>`) in `Avalonia.Controls.ApplicationLifetimes`;
`StorageProviderExtensions.TryGetLocalPath` in `Avalonia.Platform.Storage`; the generic
`TryGetFeature<T>()` extension in namespace `Avalonia`.

## Risk

Starting with zero documents is low risk: the `ActiveDocument == null` state was already a first-class,
reachable state (`OnCloseDocumentAsync` sets it null when the last tab closes), `HasActiveDocument`
exists and every command's `CanExecute` already guards on it, and `Title` / `ShowGrid` / the
`ActiveDocument` setter all handle null. Removing the constructor's `OnNew()` just makes the app *start*
in a state reachable by closing all tabs. Ribbon bindings to `ActiveDocument.X` resolve a null root to
`UnsetValue` (silent, disabled control) under Avalonia compiled bindings — not an NRE.

## Scope caveats

- **macOS handler is wired but dormant** under "code only": Finder won't route a `.draw` to the app
  until a `.app` bundle declares the document type (`CFBundleDocumentTypes`), which is the deferred OS
  association. The code works once that exists; it cannot be exercised or verified in this task.

## Verification

- `dotnet build Draw.slnx` — clean (0 warnings, 0 errors; nullable warnings are build errors here).
- `dotnet test --solution Draw.slnx` — 390/390 pass (unchanged; pure-logic layers untouched).
- Manual, on **Windows** (built exe or `dotnet run --project src/Draw.App -- <path>`):
  1. Bare launch → empty window, no tab, no binding errors; New button still works; closing all tabs
     still works.
  2. Launch with one `.draw` path → opens in a tab, becomes active, added to Recent files.
  3. Launch with several `.draw` paths → each opens as a tab.
  4. Corrupt `.draw` → "Could not open file" dialog; non-existent / junk arg → silently ignored.
  5. Associate `.draw` → the exe via Windows "Open with → Always", then double-click → app opens it.
  6. App already open, double-click another → a new instance opens it (expected).
- Linux argv path mirrors Windows; macOS event path is unverifiable here (see caveat).
