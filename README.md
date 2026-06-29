# Draw

A cross-platform (Windows / Linux / macOS) desktop application for drawing schemas and
UML/ER diagrams, built with **Avalonia 12** on **.NET 10**. Diagrams with a semantic
model (UML class/ER) can generate SQL DDL.

> Status: **Phase 2 (connectors)** complete — see [`documentation/roadmap.md`](documentation/roadmap.md).

## Solution layout

```
src/
  Draw.Model         document model, styling, serialization (net10 library)
  Draw.Diagramming   memento undo, grid snapping (net10 library)
  Draw.App           Avalonia 12 desktop app (IHost bootstrap, MVVM)
documentation/
```

`Draw.Sql` (DDL generation) and `Draw.Export` (SVG/PDF) arrive in Phase 5.

## Build

```bash
dotnet build Draw.slnx
```

## Run

```bash
dotnet run --project src/Draw.App
```

**Linux prerequisite:** Avalonia's Skia backend needs fontconfig. On a headless/minimal
distro (incl. some WSL installs) install it first, otherwise startup fails with
`Unable to load shared library 'libSkiaSharp' … libfontconfig.so.1`:

```bash
sudo apt-get install -y libfontconfig1 libice6 libsm6
```

Windows and macOS need no extra native packages.

## House conventions

C# 14, nullable-as-error, no implicit usings, no MVVM source generators
(`field` + `SetProperty`, explicit `RelayCommand`s), `.slnx` solution, central package
management. See [`documentation/architecture.md`](documentation/architecture.md).

## License

Released under the [MIT License](LICENSE) — Copyright © 2026 Josué Clément.
