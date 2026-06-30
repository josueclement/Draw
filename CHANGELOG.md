# Changelog

All notable changes to Draw are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-06-30

### Added
- **Open from launch arguments** — `.draw` files passed on the command line (and, on macOS, via the
  file-activation event) now open in tabs. A bare launch shows an empty window instead of a blank
  document.
- **Quick text edit** — press **Enter** or **F2** to edit the selected node's text.
- **Vim-style shortcuts** — a bottom **`:` command line** with `:w` (save), `:q` / `:q!` (close the
  active tab, prompting if modified / discarding), `:wq` (save then close), and `:qa` / `:qa!` (quit,
  prompting per tab / discarding all). **`h` / `j` / `k` / `l`** move the selection to the nearest
  shape in that direction (hold **Ctrl** to extend the selection); **`u` / `U`** undo / redo.

### Fixed
- Mind-map branch base now sits **flush against the parent node's edge** — previously it was cut at
  the angle the curve happened to leave, leaving a visible slant once a child was moved off-axis or
  branches were fanned out via *Space connections*.

## [1.0.0] - 2026-06-29

First public release. Draw is a cross-platform (Windows / Linux / macOS) desktop editor for
schema, UML and ER diagrams, built with Avalonia 12 on .NET 10.

### Diagram types
- **Basic shapes** — 9 core shapes with editable text and per-shape styling.
- **UML class diagrams** — class / interface / enum nodes with a compartment member editor
  (inline + inspector), free-text member types with autocomplete.
- **Use-case diagrams** — actors, use cases, system boundaries; association, include/extend,
  generalization.
- **ER diagrams** — entity nodes with typed columns (PK / FK / nullable / unique) and per-end
  crow's-foot cardinality relationships.
- **Mind maps** — central topic with hover-to-spawn linked children and organic tapered branch
  connectors; per-node status markers.

### Editing
- Multi-document tabs, new / open / save / save-as of `.draw` files, recent files.
- Zoom / pan, visible grid with snap-to-grid (per-document grid visibility, app-wide snap toggle).
- Marquee multi-select (shapes and connectors), move / resize handles, arrow-key nudge.
- Copy / cut / paste / duplicate, including embedded images (drag-drop, paste, or picker).
- Memento-based undo / redo, one step per gesture.
- Align, distribute, align-to-reference, connector spacing, and shape stacking order (Z-order).

### Connectors
- Straight / orthogonal / rounded routing with shape-boundary attachment.
- Full UML relationship decoration set; editable source / center / target labels.
- Forced endpoint anchors, waypoints, and movable labels; bulk styling across a selection.

### Styling & shell
- Ribbon-based UI (`Carbon.Avalonia.Desktop`) with Phosphor icons; light / dark themes.
- Theme-aware quick-style palette with curated pastel swatches.
- Keyboard shortcuts, including vim/blender-style multi-key chords, configurable via
  `%APPDATA%/Draw/keymap.json`; a Shift+H overlay lists the active shortcuts and the app version.
- Unsaved-changes warning on close / quit.

### Export
- Export the whole diagram at 1:1 as **PNG**, **JPEG**, or **SVG**, plus copy-as-image.

### Reliability
- Unhandled exceptions are written to a timestamped log under `%APPDATA%/Draw/logs/`, and
  UI-thread crashes show an error dialog before exiting.

### Known limitations
- **No SQL DDL generation yet** — the `Draw.Sql` ER→DDL feature is planned for a later release.
- **No PDF export yet** — only PNG / JPEG / SVG are available.
