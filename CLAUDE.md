# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Draw is a cross-platform Avalonia 12 / .NET 10 desktop app for drawing schema and UML/ER
diagrams. Read `documentation/architecture.md` first (canonical architecture) and
`documentation/roadmap.md` (phased plan; per-phase specs in `documentation/specs/`). The README
covers setup. This file captures only what those don't, or what is easy to get wrong.

## Commands

```bash
dotnet build Draw.slnx                          # build all (solution is .slnx, not .sln)
dotnet run --project src/Draw.App           # run the desktop app
dotnet test --solution Draw.slnx                # run unit tests (xUnit v3 on MTP; note: --solution, not a positional arg)
```

There is no `.editorconfig` or lint task. `dotnet format Draw.slnx` works but isn't a gate.
Analyzers are on (`AnalysisLevel=latest`) and **nullable warnings are build errors**
(`WarningsAsErrors=nullable`) — a `?`/null mistake fails the build, not just warns.

Tests cover the pure-logic layers only (`tests/Draw.Model.Tests`, `tests/Draw.Diagramming.Tests`):
routing, signature parsing, serialization. They have **no Avalonia dependency, so they run headless
under WSL2** — unlike the GUI. The test runner is the Microsoft Testing Platform (xUnit v3 via the
`xunit.v3.mtp-v2` meta-package), wired through `global.json`'s `test.runner` setting.

## Architecture (load-bearing invariants)

Three layers (`src/`), all `net10` on purpose (the libraries target net10, not netstandard —
they have no external consumers):

- **`Draw.Model`** — framework-agnostic document model + `System.Text.Json` serialization
  with polymorphic `$type` discriminators. One diagram per `.draw` file (JSON). Uses its own
  value primitives (`Point2D`/`Rect2D`/`ArgbColor`), **never** Avalonia types.
- **`Draw.Diagramming`** — UI-agnostic behavior: memento `IUndoService`, grid snapping,
  connector routing (`Routing`, with `ShapeBoundary`/`ShapeOutline`), UML signature parsing.
  No Avalonia dependency.
- **`Draw.App`** — Avalonia 12 MVVM editor, bootstrapped via `Microsoft.Extensions.Hosting`
  (`Program.Main` builds an `IHost`, runs the Avalonia desktop lifetime, then stops the host;
  `App` resolves `MainWindow` from DI). This is the only project that touches Avalonia.

Invariants that are easy to violate and must be preserved:

- **View models depend only on `Avalonia.Media` value types — never `Avalonia.Controls`.** This
  is what keeps editor logic decoupled from the rendering layer. Don't reach for a `Control`
  in a VM. Avalonia↔model mapping lives in `App/Rendering/` (e.g. `StyleMappingExtensions`,
  `ShapeGeometryBuilder`), not in the model.
- **`NodeViewModelBase`** factors out placement/selection/style shared by every node kind;
  shapes, class nodes, actors, use-cases and boundaries differ only in content. Add behavior
  there, not per-kind, when it's shared.
- **Undo = whole-document memento snapshots**, captured once per completed gesture. **Each
  document tab owns its own `IUndoService`**, created by `DiagramDocumentViewModelFactory` — get
  undo/services through the factory, don't share one across tabs.
- **Rendering** is hybrid retained-mode: nodes are templated controls on a `Canvas` under an
  in-house `MatrixTransform` (zoom/pan is hand-rolled — `PanAndZoom` has no Avalonia 12 build);
  selection handles and the marquee live on a separate overlay `Canvas`; connectors are `Path`
  controls in a layer behind nodes. Routing is computed UI-agnostically in `Diagramming.Routing`.

## Conventions (deliberate, enforced)

- C# 14, `Nullable=enable`, **no implicit usings** (declare usings explicitly), explicit types
  (the codebase does not use `var`).
- **No MVVM source generators.** Use the `field` keyword + CommunityToolkit's `SetProperty`/
  `OnPropertyChanged` and explicit `RelayCommand`s (see `ViewModelBase`). Don't add `[ObservableProperty]`/`[RelayCommand]`.
- **Central package management**: declare/upgrade versions only in `Directory.Packages.props`;
  `.csproj` files reference packages without versions.

## Environment & workflow gotchas

- **WSL2 / headless Linux has no usable display for the GUI.** Don't launch or screenshot the
  app there — `dotnet build` runs fine, but the window won't render. Verify anything **visual**
  by running on Windows/macOS (or ask the user to run and report).
- **Linux prerequisite:** `sudo apt-get install -y libfontconfig1 libice6 libsm6`, or Skia fails
  to load at startup. Windows/macOS need nothing extra.
- **Line endings:** a `.gitattributes` (`* text=auto eol=lf`) normalizes the tree to LF, so the
  old CRLF phantom-diffs are gone. If you still see spurious whole-file diffs (e.g. a stale local
  checkout), check real changes with `git diff --ignore-cr-at-eol`. Commit/push only when asked and
  only the intended files.
- **Avalonia 12 trap:** `ItemsControl` defaults `ClipToBounds=true`; with a `Canvas` ItemsPanel
  the control measures to 0×0 and clips its items away (invisible nodes). The world-space
  layers in `DiagramView.axaml` set `ClipToBounds="False"` for this reason — don't remove it.
  Compiled bindings are on (`AvaloniaUseCompiledBindingsByDefault=true`) and binding failures are
  silent unless logging is enabled.
