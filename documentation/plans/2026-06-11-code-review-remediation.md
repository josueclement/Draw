# Code-review remediation (2026-06-11): prioritized plan

**Date:** 2026-06-11
**Status:** Code-complete — Phases 0–6 all ✅. Build 0-warning; **299 tests green** (was 220; +79).
`DiagramView.axaml.cs` 2032 → ~1518 lines; `DiagramDocumentViewModel.cs` further peeled. **The view/VM
and rendering changes are build-verified only — they need a manual GUI pass on Windows/macOS** (per-phase
checklists in `~/.claude/plans/can-you-start-the-lovely-stream.md` and below). Claude has not committed.
**Branch:** all work lands directly on `review/2026-06-11-fresh-code-review` (no per-item branches).
**Source review:** `documentation/reviews/2026-06-11-code-review.md` (0 Critical, 0 correctness
bugs; findings are about evolvability / concentration of responsibility).

## Scope decisions (from the remediation interview)

- **Scope:** Tier C — the full pass, including the High/L architectural items (E1 node registry,
  A1/A2 god-file peeling).
- **Branching:** one branch (`review/2026-06-11-fresh-code-review`); Claude never commits.
- **Line endings:** `.gitattributes` added; the tree was already 100% LF, so the renormalization
  was a no-op (the file is the forward guarantee).

## The constraint that shapes ordering: verification reality

`Draw.Model` + `Draw.Diagramming` are headless-unit-testable (xUnit v3 / MTP, no Avalonia).
**`Draw.App` (VMs / coordinators / views) is not** — it pulls in Avalonia and the GUI can't render
under WSL2. App changes are verified by `dotnet build Draw.slnx` (compiled bindings +
`WarningsAsErrors=nullable`) **plus a manual GUI run on Windows/macOS**. Strategy: extract testable
logic into `Draw.Diagramming` first, land the test safety net before refactoring, and keep the
un-testable view/VM moves as **pure relocations** (not rewrites).

Baseline before work: build **0 warnings**, **220/220** tests green.

## Phases (impact- and dependency-ordered; each ends build-clean + tests-green)

### Phase 0 — Config & docs · ✅ done
- **B2** `.editorconfig` codifying actual conventions (no-`var` = error; rest advisory).
- **B1** `.gitattributes` (`* text=auto eol=lf`); renormalization a no-op (tree already LF).
- **DOC1** `architecture.md` Verification section rewritten (real two-project suite + "App layer
  untested by design"). **DOC2** "7 shape kinds" → 9. **DOC3** stale "1540-line god VM" trimmed.
  **DOC4** ER / Phase-5 stub added. **CLAUDE.md** CRLF caveat updated.

### Phase 1 — Test safety net + C3 · pending
- **T1a** `MementoUndoServiceTests` (stacking/LIFO, redo-clears-on-capture, depth-cap, clamp,
  empty no-ops, StateChanged, clone-not-reference).
- **T1b** `CloneCompletenessTests` — reflection over auto-discovered `NodeBase` subclasses + the
  explicit non-node list; non-default population, deep-compare, reference-independence. Guards D5/C1.
- **T1c** `SnapExtensionsTests`. **C3** `StylePalette.TryGet` → `[MaybeNullWhen(false)]`.
- **T1d** coordinator orchestration tests: **deferred by design** (not cleanly headless; documented
  in DOC1).

### Phase 2 — Shared testable-geometry substrate + small dedups · pending
- **C2/C4 base:** `Point2D.ZeroLengthTolerance` + `ApproximatelyEquals`; new `GeometryTolerance`.
  C2: `Point2D.Normalized`. C4: `ConnectionDistributor.PlanPinning`.
- **D2:** new `SegmentGeometry` (`DistanceToSegment`/`DistanceToPolyline`/`NearestSegmentIndex`);
  delete the two verbatim copies; rewire call sites. (Reconciles two design proposals into one home.)
- **A2b:** new `NodeHandleGeometry` (Avalonia-free) + tests; view calls into it.
- **S2:** accent colours → `App.axaml` resources. **B3:** zoom bounds → injected `EditorOptions`.
  **D3:** `NextZIndex`/`NextBackgroundZIndex` on `IDocumentEditContext`.

### Phase 3 — Node-type registry & creation factory · pending
- **E1 net:** `NodeKindContractTests` (JsonDerivedType completeness/uniqueness/round-trip — the only
  headless-enforceable part of E1).
- **E1 registry:** `NodeKindDescriptor` + `NodeKindRegistry` (self-validates at construction);
  DI-register; thread through factory + VM ctor; `CreateNodeViewModel` delegates. Honest limits:
  `[JsonDerivedType]`/XAML templates/EmitNode stay distributed (test/build/startup-guarded, not
  registry-driven).
- **D4:** `PlaceNode<TVm>` collapses the AddXxx flow; per-kind sizes + boundary z-band move into
  descriptors.

### Phase 4 — View-layer decomposition · ✅ done
- **A2c** `OverlayController` (handles + connector handles + reference outlines moved out; marquee /
  connect-preview kept with the gesture state machine in the view). **D1** generic
  `InlineRowEditController<TRow>` + `IEditableRow` on the VM base + two owner adapters, driving both
  class-member and entity-column inline editing; the kind-specific fresh-add paths stay in the view.
  *The two XAML row templates are deliberately NOT merged* (different context menus + font binding —
  same call the 2026-06-10 remediation made). **A2a** `DiagramView.axaml.cs` is now ~1518 lines
  (was 2032). Build clean; needs a GUI pass (handles track zoom/pan/selection, marquee,
  connector endpoint/waypoint handles, reference outlines, inline-edit members + columns:
  double-tap, Enter-adds-next, Tab/Shift+Tab, Alt+↑/↓ reorder, blank-row discard, context menus).

### Phase 5 — VM-surface refactors · ✅ done
- **E2** `ToolboxViewModel`: single `ArmedTool` (closed `ToolItem` record union); `Selected*` are typed
  projections, mode flags/headers/`ActiveToolHint` computed off it — the mutual-exclusion boilerplate
  is gone, full binding surface preserved. **A3** `InspectorViewModel`: `InspectorSelection` enum;
  the bound booleans are computed getters (identical names), `LoadFromSelection` sets one value;
  dropped the unbound `IsShapeSelected`. **A1-selection** `SelectionCoordinator` (pure move of the
  IsSelected loops; VM methods delegate then `RaiseSelectionChanged`). Build clean; needs a GUI pass
  (arm/disarm every tool + headers + status hint + select-tool cursor; each inspector panel shows for
  its selection; single-click / shift-toggle / marquee / mixed node+connector / click-empty-clears /
  undo-redo selection).

### Phase 6 — C1 default-color sentinel · ✅ done
- `ShapeStyle.Fill`/`FontSpec.Color` are now `ArgbColor?` (null = follow theme); the `DefaultFill`/
  `DefaultColor` constants stay as render fallbacks, no longer sentinels. **Schema bumped to v2** with a
  `UpgradeV1ToV2` migration that nulls any colour equal to a legacy default (old files keep following the
  theme). Swept `NodeViewModelBase`, `ConnectorViewModel`, `InspectorViewModel`; the SVG export reads the
  VM's resolved brush, so null is handled there too. New tests cover the migration, null-omit round-trip,
  and the bug fix (a v2 explicit default-valued fill stays non-null). Needs a GUI pass: pick the exact
  default colour → it stays put on theme toggle; old `.draw` files still follow the theme; SVG/PNG export
  colours correct.

## Verification (per phase, no CI gate)

`dotnet build Draw.slnx` must stay 0-warning; `dotnet test --solution Draw.slnx` must stay green
(new tests added in Phases 1–3, 6). Visual phases (3–6) need a manual GUI pass on Windows/macOS
(per-phase checklists in `~/.claude/plans/can-you-start-the-lovely-stream.md`). Review real diffs
with `git diff --ignore-cr-at-eol`.
