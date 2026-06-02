# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

JCL Draw is a cross-platform Avalonia 12 / .NET 10 desktop app for drawing schema and UML/ER
diagrams. Read `documentation/architecture.md` first (canonical architecture) and
`documentation/roadmap.md` (phased plan; per-phase specs in `documentation/specs/`). The README
covers setup. This file captures only what those don't, or what is easy to get wrong.

## Commands

```bash
dotnet build Draw.slnx                          # build all (solution is .slnx, not .sln)
dotnet run --project src/Jcl.Draw.App           # run the desktop app
dotnet test --solution Draw.slnx                # all tests (MTP runner; opt-in is in global.json)
dotnet run --project tests/Jcl.Draw.Model.Tests # run one suite directly (test projects are Exe)
```

Run a **single test / class / namespace** with xUnit v3's native filters, passed after `--`
(single dash; `*` wildcards allowed). MTP intercepts `dotnet test`, so target one project via
`dotnet run`:

```bash
dotnet run --project tests/Jcl.Draw.Model.Tests -- -method "Jcl.Draw.Model.Tests.MyClass.MyTest"
dotnet run --project tests/Jcl.Draw.App.Tests   -- -class  "*CanvasPlacementHeadlessTests"
dotnet run --project tests/Jcl.Draw.Model.Tests -- -filter "/*/*/*/MyTest"   # query: /asm/ns/class/method[trait=value]
```

There is no `.editorconfig` or lint task. `dotnet format Draw.slnx` works but isn't a gate.
Analyzers are on (`AnalysisLevel=latest`) and **nullable warnings are build errors**
(`WarningsAsErrors=nullable`) — a `?`/null mistake fails the build, not just warns.

## Architecture (load-bearing invariants)

Three layers (`src/`), all `net10` on purpose (the libraries target net10, not netstandard —
they have no external consumers):

- **`Jcl.Draw.Model`** — framework-agnostic document model + `System.Text.Json` serialization
  with polymorphic `$type` discriminators. One diagram per `.jcld` file (JSON). Uses its own
  value primitives (`Point2D`/`Rect2D`/`ArgbColor`), **never** Avalonia types.
- **`Jcl.Draw.Diagramming`** — UI-agnostic behavior: memento `IUndoService`, grid snapping,
  connector routing (`Routing`, with `ShapeBoundary`/`ShapeOutline`), UML signature parsing.
  No Avalonia dependency.
- **`Jcl.Draw.App`** — Avalonia 12 MVVM editor, bootstrapped via `Microsoft.Extensions.Hosting`
  (`Program.Main` builds an `IHost`, runs the Avalonia desktop lifetime, then stops the host;
  `App` resolves `MainWindow` from DI). This is the only project that touches Avalonia.

Invariants that are easy to violate and must be preserved:

- **View models depend only on `Avalonia.Media` value types — never `Avalonia.Controls`.** This
  is what keeps editor logic unit-testable on the headless backend. Don't reach for a `Control`
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

- **WSL2 / headless Linux has no usable display for the GUI.** Don't launch, screenshot, or
  pixel-test the app there — `dotnet build` and the non-visual tests run fine, but the window
  won't render. Verify anything **visual** by running on Windows/macOS (or ask the user to run
  and report). Headless view tests exist (`HeadlessUnitTestSession`, `UseHeadlessDrawing=false`)
  but exercise interaction/layout, not pixels. There are no pixel tests by design.
- **Linux prerequisite:** `sudo apt-get install -y libfontconfig1 libice6 libsm6`, or Skia fails
  to load at startup. Windows/macOS need nothing extra.
- **Noisy working tree:** there is no `.gitattributes`, so ~90 files perennially show as modified
  (CRLF vs LF). Editing a file can flip its line endings and make the *whole* file show as
  changed. Check real changes with `git diff --ignore-cr-at-eol`, and commit/push only when asked
  and only the intended files.
- **Avalonia 12 trap:** `ItemsControl` defaults `ClipToBounds=true`; with a `Canvas` ItemsPanel
  the control measures to 0×0 and clips its items away (invisible nodes). The world-space
  layers in `DiagramView.axaml` set `ClipToBounds="False"` for this reason — don't remove it.
  Compiled bindings are on (`AvaloniaUseCompiledBindingsByDefault=true`) and binding failures are
  silent unless logging is enabled.
