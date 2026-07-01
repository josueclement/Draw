# Changelog

All notable changes to Draw are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-07-01

### Added
- **Recent documents on the welcome screen** — the empty-state screen (no tab open) now lists recent
  `.draw` files. Click a row to open it, hover the **×** to drop it from the list; missing files are
  greyed out and the section hides when there are none. The File ▸ Open Recent menu is unchanged.
- **Align & distribute palette** — a new **`Shift+A`** overlay grouping the align, distribute, and
  align-to-reference commands. Controls that need more than one shape disable themselves when the
  selection is too small.
- **Automatic connector spacing** — connectors no longer stack on a single point. A newly drawn
  connector takes a free slot on the side it attaches to (existing connectors stay put), while
  **Duplicate**, **Paste**, and the mind-map **"+"** button re-space the affected shapes' connectors
  evenly. Controlled by a new `AutoSpaceConnectors` option (default on; set it to `false` in
  `appsettings.json` to restore the previous merge-on-midpoint behaviour).
- **Empty-state guidance** — with no document open, the canvas shows a *"No document open"*
  call-to-action with **New document** / **Open…** buttons and a pointer to the help overlay. The
  **`Shift+H`** shortcut-help overlay now also opens when no document is open (previously it required
  an active document).

### Changed
- **The `:` command line is now a centered command palette** — the vim commands `:w`, `:q` / `:q!`,
  `:wq`, and `:qa` / `:qa!` open in an overlay card matching the other `Shift`+key surfaces, with a
  focused input prefilled `:` and a live-filtered list of the commands and their descriptions (run by
  **Enter** or by clicking a row). This replaces the thin bar that was docked at the bottom of the
  window; the commands themselves are unchanged.
- **Style picker shortcut moved from `Shift+Y` to `Shift+T`** — `Shift+Y` no longer opens the style
  picker.
- **Duplicate is split across two shortcuts** — **`Ctrl+D`** now duplicates the selection *without*
  its connectors, and the new **`Ctrl+Shift+D`** duplicates *with* connectors (carrying any connector
  with either end touching the selection). In 1.2.0 a plain `Ctrl+D` carried those connectors. Both
  variants appear in the Edit menu, the ribbon, and the `Shift+H` help.
- **Selection palettes open even with no selection** — the icon (**`Shift+I`**), style
  (**`Shift+T`**), and align/distribute (**`Shift+A`**) palettes now open whenever a document is open,
  rather than requiring a selection first; controls that act on a selection are shown disabled until
  something is selected. They still do nothing when no document tab is open.

### Fixed
- **Mind-map branch dash styles** — mind-map branch ribbons now render the **Dash**, **Dot**, and
  **DashDot** connector styles instead of always drawing solid. The dashed ribbon is honoured in SVG
  and PNG export as well.

## [1.2.0] - 2026-06-30

### Changed
- **Duplicating a shape now duplicates its connectors** — **Duplicate** (`Ctrl+D`) now also clones any
  connector with *either* end touching the selection (previously only connectors with **both** ends in
  the selection were carried). Endpoints are resolved per-connector: ends on duplicated nodes map to the
  clones, ends on shapes left in place stay attached to the originals, and a connector with no resolvable
  end is dropped. Copy / paste is unchanged (still both-ends-only).
- **Space connections now avoids crossings** — the **Space connections** action orders the spaced
  connectors on each side by the position of the shape at their far end (rather than their current
  position order), so fanned-out connectors follow the shapes they point to and stop crossing. Ties keep
  their current order; connectors keep the side they land on, and **Merge connections** is unchanged.

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
