# Code review — Draw solution (2026-06-11)

**Reviewer:** fresh full-solution pass, judged on today's code on its own merits (not anchored to
the 2026-06-10 review). **Scope:** whole solution — `Draw.Model`, `Draw.Diagramming`, `Draw.App`,
both test projects, build/config, docs. **Depth:** exhaustive (Critical → Nit). **Deliverable:**
this report only — no code was changed.

**Baseline at review time:** `dotnet build Draw.slnx` → **0 warnings** (analyzers on,
`WarningsAsErrors=nullable`); `dotnet test --solution Draw.slnx` → **220 passed, 0 failed, 0 skipped**.

---

## 1. Executive summary

**This is a high-quality, well-disciplined codebase.** The three-layer architecture is real and
respected, the framework-agnostic layers are genuinely UI-free and well-tested, naming and comments
are consistently excellent (every non-obvious decision carries a *why*), and the build is clean
under strict analyzers. There are **no correctness bugs of note and nothing Critical/High in the
"this is broken" sense.**

The findings are about **evolvability and concentration of responsibility**, which is exactly what
you asked about. Three themes dominate:

1. **Two files carry most of the editor's weight** — `DiagramView.axaml.cs` (2032 lines) and
   `DiagramDocumentViewModel.cs` (1121 lines). Both are well-*organized* internally, but both still
   own many responsibilities and are the hardest things to change safely.
2. **Adding a new node kind is shotgun surgery** across ~6–7 sites with **no compile-time safety
   net** — the single biggest limiter on evolvability.
3. **The class-member / entity-column inline editor is duplicated** (~400 lines of parallel
   code-behind + parallel XAML), even though the model/VM layer already unified it.

Everything else is Low/Nit polish. The prior remediation clearly did real work; the debt that
remains is concentrated and addressable.

### Severity tally

| Severity | Count | Notes |
|---|---|---|
| Critical | 0 | — |
| High | 3 | Evolvability / SoC — *relevance* to your goal, not bugs |
| Medium | 7 | |
| Low | 12 | |
| Nit | 8 | |

> **Severity = impact on maintainability / evolvability / correctness, not bug urgency.** A "High"
> here means "most relevant to the best-practices/evolvability question you asked," not "the app is
> broken." For a product with a *closed, stable* shape/node vocabulary, several of these are
> defensible as-is — each recommendation notes whether it's worth the churn.

---

## 2. What's strong (keep doing this)

- **Layering is real, not aspirational.** `Draw.Model` uses only its own primitives; `Draw.Diagramming`
  has no Avalonia dependency; VMs touch only `Avalonia.Media` value types (verified: zero
  `Avalonia.Controls` in `ViewModels/`). The one glue type that needs `Controls`
  (`ViewportScrollController`) correctly lives in `Views/Interaction/`, not in a VM.
- **Pure logic is extracted and tested.** Routing, shape-boundary geometry, layout (align / distribute /
  z-order / clone / connection-spacing), and signature parsing all live in `Draw.Diagramming` with
  focused unit tests. `CloneArranger`, `ConnectionDistributor.PlanPinning`, `ZOrderArranger.ReorderInBands`
  are model examples of "lift the pure core out of the VM."
- **Render-path parity is designed in.** `DecorationGeometryData` and `ActorDimensions` are shared by
  the Avalonia builder *and* the SVG exporter, so the two render paths cannot drift.
- **Conventions are followed mechanically:** 0 `var`, 0 `TODO/HACK/FIXME`, 0 commented-out code,
  explicit usings, one `null!` in the whole tree. Comments explain *why*.
- **Robust I/O:** corrupt keymap / recent-files / images all degrade gracefully with documented
  narrow catches; schema-version guard + migration seam; clipboard/file access abstracted behind
  services so VMs stay Avalonia-free.
- **Coordinators (`Clipboard`/`Alignment`/`ConnectorSpacing`/`ZOrder`) and the input/keymap
  subsystem are exemplary** — thin, single-purpose, JSON-configurable, auto-registered from enums.

---

## 3. Findings

### 3.1 Separation of concerns

**[A1 · High] `DiagramDocumentViewModel` is still a god object** — `ViewModels/DiagramDocumentViewModel.cs`
(1121 lines, ~90 public members). Even after the coordinator extraction it owns node CRUD
(`AddShape`/`AddClassNode`/`AddEntityNode`/`AddUseCaseNode`/`AddConnector`), selection (8 methods,
`DiagramDocumentViewModel.cs:709-814`), hit-testing (`HitTestConnector`/`ConnectorDistance`/`DistanceToSegment`,
`:816-1083`), viewport/zoom/pan/fit, undo/redo, style application, full-collection rebuild, theme
handling, and disposal. *Why it matters:* it's the single highest-churn class and the hardest to
test (its useful logic is reachable only through the whole VM). *Recommendation:* continue the
direction already set — extract a node-creation factory, a selection manager, and move the
hit-test/segment geometry to `Draw.Diagramming.Geometry` (testable). *Effort:* L. *Note:* it is
well-organized and the façade pattern is deliberate; this is "keep peeling," not "rewrite."

**[A2 · Medium] `DiagramView.axaml.cs` (2032 lines) holds 6+ responsibilities** — gesture state
machine, overlay rendering (`RebuildOverlay`/`RepositionOverlay` + handles + reference outlines +
marquee + connect-preview, `:1687-2028`), node-handle hit-test/resize geometry
(`HitTestHandle`/`HandlePositions`/`ResizeBounds`/`LockAspect`, `:1569-1676`), inline class-member
editing, inline entity-column editing, image export, drag-drop, and context-menu building. *Why:*
it's the second-highest-churn file and almost none of it is unit-testable. *Recommendation:* extract
(a) the duplicated inline row-editing into a generic controller (see D1), (b) the node-handle
geometry into `Diagramming.Geometry` beside `ConnectorHandleHitTester` (testable), (c) overlay
management into an `OverlayController` (mirrors the existing `ConnectorEditController` /
`ViewportScrollController`). *Effort:* L.

**[A3 · Low] `InspectorViewModel` models selection as a fan of per-type booleans** —
`IsShapeSelected`/`IsClassNodeSelected`/`IsLabelNodeSelected`/`IsEntityNodeSelected`/
`IsConnectorSelected`/`IsSingleConnectorSelected` with per-type casts in `LoadFromSelection`
(`InspectorViewModel.cs:284-343`). Each new node kind with inspector fields adds another flag + cast
+ branch + XAML panel. *Recommendation:* a small per-kind "inspector descriptor" or a discriminated
selection model. *Effort:* M. (Acceptable today at 7 kinds.)

### 3.2 Evolvability / extensibility

**[E1 · High] Adding a node kind is shotgun surgery with no compile-time enforcement.** A new node
type must be wired into, at minimum:
- `Model/Nodes/NodeBase.cs:13-20` — `[JsonDerivedType]` discriminator
- `DiagramDocumentViewModel.CreateNodeViewModel` switch (`:996-1006`) — **runtime** `NotSupportedException`, not a compile error
- a new `AddXxx` method on the VM + a `ToolboxViewModel` entry (collection + `Selected*` + mode bool + header + command)
- `InspectorViewModel` (flag + cast + load branch)
- `Rendering/DiagramSvgRenderer.EmitNode` switch (`:82-113`) and, for shapes, `ShapeGeometryBuilder` + `ShapeSvgPathBuilder` + `ShapeOutline`
- the `DiagramView.axaml` `DataTemplate`

Miss one and nothing fails to build; you find out at runtime (or in an export). *Why:* this is the
central evolvability cost and directly answers your "evolutivity" question. *Recommendation:* a
**node-type registry/descriptor** that pairs (model type ↔ VM factory ↔ tool metadata ↔ discriminator
↔ render builder) in one place, so adding a kind is one registration and the compiler/one test
enforces completeness. *Effort:* L. *Honest caveat:* the vocabulary (UML + ER + basic shapes) is
fairly closed; if you don't expect many new kinds, the current spread is livable — but it's the
first thing to fix if extensibility is a goal.

**[E2 · Medium] `ToolboxViewModel` mutual-exclusion boilerplate** — `ViewModels/ToolboxViewModel.cs:95-193`:
five `Selected*` setters each manually null the other four, plus five `IsXMode` bools and five header
properties. Adding a sixth category means editing all five existing setters. *Recommendation:* model
"armed tool" as a single nullable value (object / discriminated union); mutual exclusion and the mode
flags fall out for free. *Effort:* M.

*Positive counter-examples:* connector routing is genuinely open/closed (strategy + DI registration,
`Routing/ConnectorRouter.cs`), and `KeymapActionRegistry` auto-registers every tool action from
`Enum.GetValues` (`Input/KeymapActionRegistry.cs:43-61`) — that's the pattern E1 wants everywhere.

### 3.3 Duplication (DRY)

**[D1 · Medium] Inline member-editing and column-editing are duplicated ~400 lines.** In
`DiagramView.axaml.cs`, `OnMember*` (`:1091-1340`) and `OnColumn*` (`:1342-1523`) are structurally
identical (`CommitAndAddNext`/`NavigateMember`/`AddMemberAndEdit`/`OwningNode`/`IsEditingMemberOf`
↔ the column equivalents). The XAML mirrors it: class members use a shared `ClassMemberRowTemplate`
resource (`DiagramView.axaml:15`) but the entity-column row template is inline and parallel
(`:195-223`). The model/VM layer already unified this via `EditableItemViewModelBase<TModel>`; the
*view* layer didn't follow. *Recommendation:* a generic `InlineRowEditController<TRow>` driving both,
and a shared row template. *Effort:* M. *This is the highest-value dedup.*

**[D2 · Low] `DistanceToSegment` duplicated verbatim** in `DiagramDocumentViewModel.cs:1070` and
`ConnectorViewModel.cs:310`. Extract to `Diagramming.Geometry` (point-to-segment math already lives
near `ConnectorHandleHitTester`). *Effort:* S.

**[D3 · Low] "max ZIndex + 1" repeated** — `DiagramDocumentViewModel.NextZIndex` (`:977`),
`ClipboardCoordinator.NextZIndex` (`:273`), and reproduced (deliberately, with a comment) in
`CloneArranger`. Consider exposing it once via `IDocumentEditContext`. *Effort:* S.

**[D4 · Low] Node-creation methods repeat a fixed sequence** — `AddShape`/`AddClassNode`/
`AddEntityNode`/`AddUseCaseNode` (`DiagramDocumentViewModel.cs:309-464`) each do
center→bounds→snap→clone-style→`NextZIndex`→add→`SelectOnly`→`MarkModified`. Extract a
`PlaceNode(NodeBase, center, w, h)` helper. *Effort:* S.

**[D5 · Low] Hand-written `Clone()` methods invite clone-drift.** Every model type lists its fields
by hand (`Connector.Clone`, `ShapeStyle.Clone`, the `NodeBase` subclasses, …). Add a field, forget
the clone, and **copy/paste silently drops it** (paste uses structural `Clone()` via `CloneArranger`;
only undo uses the JSON round-trip). Only `ImageNode.Clone` is directly tested. *Recommendation:* a
reflection-based "clone preserves every property" test across all model types, or a documented
checklist. *Effort:* S–M.

**[D6 · Low] `MainWindow` repetition** — the export trio (`OnExportImageRequested`/`OnExportSvgRequested`/
`OnCopyImageRequested`, `:309-417`) share a get-view → guard → produce → error-dialog skeleton, and
`WireAlignDropdown`/`WireAlignToReferenceDropdown` (`:217-253`) are near-identical. *Effort:* S.

**[D7 · Nit] "MainWindow's clipboard/storage provider" lookup** repeated in `FileDialogService`,
`ImageExportService`, `ClipboardService`. One shared helper. *Effort:* S.

### 3.4 Correctness & robustness (mostly clean)

**[C1 · Low] Default style colors double as "uncustomized" sentinels.** `ShapeStyle.DefaultFill` /
`FontSpec.DefaultColor` are compared by value to decide theme-following
(`NodeViewModelBase.UsesDefaultFill`, `:137-140`). A user who picks *exactly* that color is then
treated as "default" and silently re-tinted on theme toggle. Documented, but fragile.
*Recommendation:* represent "uses theme default" explicitly (null color / a flag), not by
value-equality on a magic constant. *Effort:* M (touches serialization).

**[C2 · Nit] `double.Epsilon` used as an "is zero" test** in `Point2D.Normalized` (`:34`) and the two
`DistanceToSegment` copies. `double.Epsilon` is the smallest denormal (~5e-324), so it only catches
*exact* zero — a small tolerance (e.g. `1e-9`) is usually intended. *Note:* `ShapeBoundary`'s
`double.Epsilon` uses are **deliberate and documented** (`:22-25`) — exclude those.

**[C3 · Nit] `StylePalette.TryGet` returns `swatch = null!`** with a non-nullable out
(`Styling/StylePalette.cs:75`), while the sibling `KeymapActionRegistry.TryGet` correctly uses
`[MaybeNullWhen(false)]`. Prefer the latter (it's the only `null!` in the codebase). *Effort:* S.

**[C4 · Nit] `ConnectionDistributor.PlanPinning` compares `Point2D` with `==`** to detect a no-op
anchor (`Layout/ConnectionDistributor.cs:124`). It works because the same formula yields identical
bits, but floating-point `==` is refactor-fragile; a tolerance compare is safer. *Effort:* S.

### 3.5 Performance (all minor — typical diagrams are small)

**[P1 · Low] Undo snapshots serialize the whole document to a JSON string** every gesture
(`MementoUndoService.Capture` → `IDocumentSerializer.Clone`), and `RecomputeModified` re-serializes
on every undo/redo (`DiagramDocumentViewModel.cs:861`). Fine at normal sizes; for very large diagrams
a structural deep-clone (`NodeBase.Clone`) is far cheaper. *Trade-off:* the JSON path sidesteps the
clone-drift risk of D5, so this is a conscious choice — just flag it if large diagrams become a use
case. *Effort:* M if ever needed.

**[P2 · Low] `RaiseSelectionChanged` fires ~26 notifications** per selection change
(`:1092-1120`), and `HasSelection`/`SelectedConnector`/… are O(n) LINQ re-evaluated by bindings.
Imperceptible for normal selections; note only if selection becomes a hot path. *Effort:* —

**[P3 · Nit] `SvgFormat.Escape` chains five `string.Replace`** (`Rendering/SvgFormat.cs:14`) — five
allocations per escaped string, export-only. *Effort:* S.

### 3.6 Tests & coverage

**[T1 · Medium] Coverage gaps in the pure-logic layers** (which are otherwise well-covered):
- **`MementoUndoService`** — no tests for undo/redo stacking, redo-clears-on-capture, or the depth cap.
- **Structural `Clone()` completeness** — only `ImageNode.Clone` is tested; copy/paste depends on the
  others being field-complete (see D5).
- **`SnapExtensions`** — untested (trivial, low risk).
- **Avalonia-free coordinators** (`AlignmentCoordinator`/`ConnectorSpacingCoordinator`/`ZOrderCoordinator`)
  — their pure cores are tested, but the orchestration (undo capture, no-op detection, collection
  mutation) isn't; they'd test cleanly against a fake `IDocumentEditContext`.

*Recommendation:* add undo-service tests and a reflection-based clone-completeness test first
(highest value / lowest effort). *Effort:* S–M. *App/VM layer remains untested by design (headless
WSL2 can't run Avalonia) — that's a reasonable boundary.*

### 3.7 Conventions & style (mostly positive)

**[S1 · Nit] Some service files hold multiple top-level types** (e.g. `IImageExportService.cs` =
enum + interface + impl; `IDialogService.cs` = enum + interface + impl), whereas `Model`/`Diagramming`
are one-type-per-file. Defensible for small cohesive service pairs, but inconsistent. *Effort:* S
(or document it as the intended convention).

**[S2 · Nit] Magic colors duplicated across XAML and code-behind** — selection accent `#3D7EFF`
appears as `DiagramView.axaml.cs:37` *and* as `#663D7EFF` in `DiagramView.axaml:329`; reference amber
`#F2A93B` in both. Promote to shared resources. *Effort:* S.

**[S3 · Nit] `MainWindow.axaml` is large (860 lines).** It's declarative ribbon/menu markup, so far
less concerning than large code-behind, but the two static tool menus are verbose. Optional. *Effort:* —

### 3.8 Build, config & tooling

**[B1 · Medium] No `.gitattributes`.** This is the cause of the perennial ~90-file CRLF phantom diffs
(called out in `CLAUDE.md`). Adding `* text=auto` (with `eol=lf`) normalizes line endings and ends
the daily "everything looks modified" friction. *Highest value-to-effort ratio in this report.*
*Effort:* S.

**[B2 · Medium] No `.editorconfig`.** The code is impressively consistent (0 `var`, explicit usings,
file-scoped namespaces), but that's enforced only by review. An `.editorconfig` codifies it
mechanically and makes the analyzer ruleset explicit. *Effort:* S–M.

**[B3 · Low] `EditorOptions.MinZoom`/`MaxZoom` are dead config.** The VM uses its own
`const MinZoom/MaxZoom` (`DiagramDocumentViewModel.cs:42-43`), and the wheel handler hardcodes
`0.1, 8` again (`DiagramView.axaml.cs:839`). Zoom bounds live in **three** places, one unused.
Consolidate to the (actually-bound) `EditorOptions`. *Effort:* S.

**[B4 · Low] `WarningsAsErrors=nullable` only** — other analyzer (CAxxxx) warnings don't fail the
build. It's clean today (0 warnings), but nothing prevents regression. Consider promoting to errors
in CI. *Deliberate choice today* (`Directory.Build.props:8`); judgment call. *Effort:* S.

**[B5 · Nit] `IConfiguration`/`IOptions` is wired but no `appsettings.json` ships**, so every option
resolves to its code default. Fine as a future seam — just noting it's currently inert. *Effort:* —

### 3.9 Documentation accuracy

**[DOC1 · Medium] `architecture.md` says "There is no automated test suite"** (`:78-80`). Stale and
contradicts reality — there are 220 passing tests across two projects. Update the Verification
section. *Effort:* S.

**[DOC2 · Low] "7 shape kinds"** in `architecture.md:20` and `roadmap.md:9` — `ShapeKind` now has 9
(adds `Circle`, `Note`). *Effort:* S.

**[DOC3 · Low] `roadmap.md` code-review-remediation section** closes with the *pre-remediation*
problem statement ("`DiagramDocumentViewModel.cs` … a 1540-line god VM", `:279`) right after stating
it was dropped to ~1050 — internally contradictory now. Trim the stale paragraph. *Effort:* S.

**[DOC4 · Low] `architecture.md` has no ER / Phase-5 section** though `EntityNode`/`ColumnSignature`
exist. Expected (Phase 5 in progress), but worth a stub. *Effort:* S.

---

## 4. Prioritized remediation backlog

Ordered by value-to-effort. None are blockers; the codebase is shippable as-is.

| # | Finding | Severity | Effort | Why first |
|---|---|---|---|---|
| 1 | **B1** `.gitattributes` | Medium | S | Ends the daily CRLF phantom-diff pain; 5 minutes |
| 2 | **DOC1** fix "no test suite" in architecture.md | Medium | S | Actively misleading to contributors |
| 3 | **T1** undo-service + clone-completeness tests | Medium | S–M | Safety net before any refactor; guards D5/P1 |
| 4 | **B3** consolidate zoom bounds (kill dead config) | Low | S | Removes a real inconsistency cheaply |
| 5 | **D2/D3/D4** small dedups (segment dist, z-index, place-node) | Low | S | Quick wins, shrink the god VM a little |
| 6 | **B2** `.editorconfig` | Medium | S–M | Locks in the (already-good) conventions |
| 7 | **D1** generic inline row-editor (member/column) | Medium | M | Biggest dedup; pre-req to thinning `DiagramView` |
| 8 | **E2** unify `ToolboxViewModel` armed-tool | Medium | M | Removes O(n²) boilerplate; helps E1 |
| 9 | **A2** peel `DiagramView` (overlay + handle geometry controllers) | Medium | L | Testability + size; build on D1 |
| 10 | **A1** continue peeling `DiagramDocumentViewModel` | High | L | The central maintainability item |
| 11 | **E1** node-type registry | High | L | The central evolvability item — do if new kinds are expected |
| 12 | misc Low/Nit (C1–C4, S1–S2, DOC2–DOC4, D5–D7, P3) | Low/Nit | S each | Opportunistic polish |

---

## 5. Verification notes

- Findings cite `file:line` against the working tree at review time (branch
  `review/2026-06-11-fresh-code-review`); each is openable to confirm.
- Baseline captured before review: build **0 warnings**, **220/220** tests green — so no finding is
  confounded by pre-existing breakage, and "tests pass" is stated from a real run, not assumed.
- One hypothesis was **checked and dropped** rather than reported: `DiagramDocumentViewModel.Dispose`
  does not `Detach()` connector VMs, but `ConnectorViewModel` only subscribes to its endpoint node
  VMs (not the singleton theme service), so on tab close the whole VM graph is collected together
  once the one external root (the theme handler) is removed — **not a leak.**
- No code was changed. If you choose remediation, each fix should be re-verified with
  `dotnet build Draw.slnx` + `dotnet test --solution Draw.slnx`, and anything visual confirmed on
  Windows/macOS (headless WSL2 can't render the GUI).
