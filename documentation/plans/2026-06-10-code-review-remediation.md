# Code-review remediation: prioritized plan

**Date:** 2026-06-10
**Status:** In progress — Priority 1 (test safety net), items 6a/6b, 2a, 3a, 3b (all four VM
coordinators extracted), and 4 (4a + 4b) done; remainder pending (2b/2c/2d, 3c, 5, 6c/6d, 7).
**Branch:** one feature branch per item (see *Execution sequence*).

## Problem

A full code-review pass over the solution (~13.6k LOC across `Draw.Model` / `Draw.Diagramming` /
`Draw.App`) looked for oversized classes/methods, logic crammed into `.axaml.cs` code-behind, and
broader correctness/duplication/testability issues. The architecture is fundamentally sound — the
debt concentrates in **two oversized files** plus a tail of duplication and unguarded edge cases.
This document captures what *could* be fixed, ordered by impact, so the work can be picked up
incrementally. Nothing here has been implemented.

## Scope decisions (from the review interview)

- **Scope:** broad — structural smells *plus* correctness/robustness and testability.
- **Tests:** recommend reintroducing **one focused test project** for the pure-logic layers (unit
  tests + framework were deliberately removed 2026-06-03; routing/parsing/serialization are the
  highest-ROI, lowest-cost targets).
- **Depth (the two giants):** **phased** — safe incremental extraction first, deeper decomposition
  later.
- **Ordering:** **impact-first**, with Effort (S/M/L) and Risk (Low/Med/High) per item so quick wins
  can still be cherry-picked.

## Honesty note: corrections to the raw findings

Three findings a first-pass scan flags as severe were checked against the code and **downgraded** —
recorded so the priorities below are trustworthy:

1. **`MoveSelectedBy` / `SnapSelectionToGrid` "miss undo capture"** → **not a bug.** The view calls
   `EnsureUndoCaptured()` once per gesture (`DiagramView.axaml.cs:591`) before the per-pixel move;
   capturing *inside* those methods would flood the undo stack. The real (mild) issue is that
   undo-capture responsibility is *split* between view (gesture-level) and VM (command-level) — a
   coupling smell, not a correctness defect → folded into item **2**.
2. **`DialogService` "violates the VM→no-Controls invariant"** → **false.** It's a *service* in the
   App layer, which may touch Avalonia. The real issue is **inconsistency** → item **5**.
3. **`KeymapService` "hardcodes `%APPDATA%`, Windows-only"** → **false.** It uses
   `Environment.GetFolderPath(SpecialFolder.ApplicationData)` — correctly cross-platform. Dropped.

## What is already good — do not "fix"

- **Rendering decoupling:** `ConnectorDecorationBuilder.Describe` → geometry data consumed by both
  the Avalonia `Build` path and the SVG `Emit` path; `ShapeGeometryBuilder` / `ShapeSvgPathBuilder`
  both reuse `ShapeOutline.GetPolygon`. (The actor figure is the lone exception — item 4b.)
- **Value objects:** `Point2D` / `Rect2D` / `Size2D` / `ArgbColor` are immutable record structs with
  pure operators.
- **DI & undo ownership:** lifetimes in `ServiceCollectionExtensions` are correct; each tab gets its
  own `IUndoService` via the factory — don't centralize undo.
- **Convention compliance:** no `var`, explicit usings, no MVVM source generators, no
  `Avalonia.Controls` in view models, nullable-as-errors — all upheld across the codebase.

## Prioritized remediation plan (impact-first)

### Priority 1 — Test safety net (enabler; do before the big refactors) · Effort M · Risk Low
The two giants below are far safer to dismantle behind regression tests, and the pure-logic layers
are untested today.
- Add **one** test project (e.g. `tests/Draw.Diagramming.Tests`) targeting `net10`; reintroduce a
  test framework into `Directory.Packages.props` (xUnit recommended; pick what you prefer).
- Cover the highest-ROI, lowest-cost units (no Avalonia needed):
  - `Routing/` — `OrthogonalRouter`, `RoundedRouter`, `StraightRouter`, `ShapeBoundary` intersections
    (parallel / tangent / degenerate rays).
  - `Uml/MemberSignature.Parse` and `Er/ColumnSignature.Parse` — **Format→Parse→Format round-trips**
    + malformed input (these double as the spec for item 6a).
  - `Serialization/JsonDocumentSerializer` — round-trip identity across all node `$type`s, clone
    independence, schema-version rejection.
- **Why first:** locks current behavior before refactoring; converts item 6's parsing fixes from
  "hope" to "verified."

### Priority 2 — `DiagramView.axaml.cs` (2032 lines): break up the code-behind · phased
The headline finding. It conflates pointer dispatch, hit-testing, zoom/pan, scrollbars, marquee,
node move/resize, connector editing, the handles overlay, context menus, and arrow-key nudge, driven
by ~13 loose gesture-state fields acting as an implicit state machine.
- **2a · Effort M · Risk Low · ✅ done** — Split the mega-handlers into intention-named private methods.
  **Done:** `OnPointerPressed` → `BeginPan`/`BeginResize`/`BeginConnectorDrag`/`TryPlaceTool`/
  `TryBeginConnectorEdit`/`BeginConnectorPick`/`BeginNodeMove`/`BeginMarquee`; `OnPointerMoved` →
  `Handle*` per `DragMode`; `OnPointerReleased` → `Finalize*` per `DragMode` + a `ResetGestureState`
  for the universal cleanup. Pure extraction, zero behavior change (build clean, tests green, diff
  self-reviewed). Verify gestures manually on Windows/macOS. The ~13 gesture fields + `DragMode` are
  untouched — that is 2b.
- **2b · Effort M · Risk Med** — Collapse the ~13 gesture fields (`_mode`, `_moveLastWorld`,
  `_marquee*`, `_edit*`, `_resize*`, nudge state) into a small **gesture-state object** (or a tagged
  union per `DragMode`) with a `Finalize()`/reset, eliminating the scattered manual flag resets on
  release.
- **2c · Effort L · Risk Med (later phase)** — Extract cohesive interaction logic into testable
  helper classes the code-behind *drives* but doesn't *own*: e.g. a `ConnectorEditController`
  (`BeginConnectorEdit` ~`1886-1941` + endpoint/waypoint/label drag) and a scrollbar/viewport sync
  helper (`UpdateScrollBars` ~`269-336`). Keep raw pointer/visual-tree events in the view.
- **2d · Effort S · Risk Low** — `UpdateHandles` (~`1690-1746`) rebuilds all overlay handles on every
  pointer move; gate rebuilds on selection/zoom change rather than per-pixel motion.

### Priority 3 — `DiagramDocumentViewModel.cs` (1540 lines): decompose the God VM · phased
~9 responsibilities, ~40 methods, 11 commands in one class: selection, node CRUD, connector CRUD,
clipboard/duplicate, align/distribute (+ reference-align), z-order, connector spacing/merge/pin, undo
orchestration, style application, view (zoom/pan) coordination.
- **3a · Effort M · Risk Low · ✅ done** — Extract the 3 longest/most-complex methods into focused units
  (with tests from item 1): `PlaceClones`, `ReorderSelected`, `PinSelectedConnectionEnds`. **Done:** the
  pure cores moved to the testable `Draw.Diagramming.Layout` layer — new `CloneArranger.Clone`,
  `ConnectionDistributor.PlanPinning`, `ZOrderArranger.ReorderInBands` — leaving only VM orchestration
  (factory, collection sync, undo, selection). Backed by new headless tests (`CloneArrangerTests`,
  `ConnectionDistributorTests`, `ZOrderArrangerTests`, which also cover the previously-untested
  `ClassifySide`/`FractionAlong`/`EvenAnchor`/`Reorder`). VM behavior unchanged (build clean, tests green,
  diff self-reviewed); spot-check duplicate / z-order / space-merge connections on Windows/macOS.
- **3b · Effort L · Risk Med · ✅ done** — Move clusters behind collaborators the VM composes
  (the VM stays the façade the view binds to): a `ClipboardCoordinator` (copy/cut/paste/duplicate +
  image decode/encode), a `ConnectorSpacingCoordinator` (space/merge/pin), a z-order helper, and an
  `AlignmentCoordinator` (align/distribute/reference). One feature branch per coordinator, clean-seam
  first. The pure transforms were already lifted in 3a, so 3b is the orchestration glue — these
  coordinators live in `Draw.App` (they drive `NodeViewModelBase`/`ConnectorViewModel`), reaching the
  VM through a shared `IDocumentEditContext` seam; structural-only, verified by manual smoke +
  green Diagramming tests. **Done (all four):** `ClipboardCoordinator` (copy/cut/paste/duplicate +
  image insertion + `PlaceClones`); `ConnectorSpacingCoordinator` (space/merge/pin); `ZOrderCoordinator`
  (`ReorderSelected` restack + `RaiseZIndexChanged` fan-out); `AlignmentCoordinator` (align/distribute +
  the `_referenceIds` reference subsystem — the VM keeps command/notification ownership and calls
  `PruneStaleReferences`). The VM dropped from ~1480 to ~1050 lines and now delegates to the four
  coordinators.
- **3c · Effort S · Risk Low** — Document in code the deliberate split-undo contract (gesture-level
  capture in the view vs. command-level capture in the VM) so future callers of `MoveSelectedBy` don't
  assume it self-captures. (Resolves the downgraded "undo" finding without changing behavior.)
- Secondary large VMs for a lighter same-pattern pass once 3a/3b land: `ConnectorViewModel` (528),
  `ShellViewModel` (437), `InspectorViewModel` (407).

### Priority 4 — Eliminate duplication · Effort M · Risk Low
- **4a · ✅ done** — `ClassMemberViewModel` and `EntityColumnViewModel` shared ~70% of an
  edit/commit/cancel + `IsNewlyAdded` + `RaiseAll` pattern. Extracted a generic
  `EditableItemViewModelBase<TModel>` owning the lifecycle and the undo-capture contract; each VM now
  supplies only its format/parse/apply/raise hooks. Collapsed the two **mirrored** class-member
  `DataTemplate`s (primary members + operations) in `DiagramView.axaml` into one shared keyed
  resource (`ClassMemberRowTemplate`). The entity-column template stays separate — it shares only the
  outer shape; its context menu and edit handlers are genuinely different, so merging it would add
  conditionals rather than remove duplication.
- **4b · ✅ done** — Actor stick-figure geometry was duplicated verbatim between `ActorGeometry.cs`
  and `DiagramSvgRenderer.EmitActor` (identical 0.18/0.62/0.25/0.30/0.28 constants and clamps).
  Extracted a framework-agnostic `ActorDimensions` (`Draw.Diagramming/Geometry/`, no Avalonia types)
  consumed by both render paths, with `ActorDimensionsTests` covering the proportions and the
  degenerate-size clamp.

### Priority 5 — Service & DI tidy-ups · Effort S–M · Risk Low
- **5a** — `DialogService` is **inconsistent**: `ConfirmUnsavedAsync` uses Carbon's
  `IContentDialogService`, while `ShowErrorAsync`/`ConfirmAsync` hand-build a raw `Window` +
  `StackPanel` + `Button` (`IDialogService.cs:66-118`). Route all three through the Carbon content
  dialog (or one private helper) for consistent look and a single dialog mechanism.
- **5b** — `FileDialogService` repeats the `GetStorageProvider()` lifetime-walk at ~5 call sites; fold
  into one accessor. (Low value — cosmetic.)
- **5c (optional / your call)** — Interface + sealed impl are co-located one-per-file in `Services/`
  (e.g. `IDialogService` + `DialogService`). Consistent and fine for small services; flag only if a
  stricter one-type-per-file convention is wanted. **Recommendation: leave as-is.**

### Priority 6 — Correctness & robustness hardening · Effort S–M · Risk Low–Med
Verified by the tests added in item 1.
- **6a · Risk Med · ✅ done** — `MemberSignature.Parse` (~`29-84`) and `ColumnSignature.Parse` /
  `StripTrailingFlags` (~`46-140`) parse free-text with no structural validation: unbalanced parens,
  stray colons, type names colliding with flag tokens (`pk` / `unique` / `NOT NULL` / `NULL`), and
  `NOT NULL` vs lone `NULL` contradictions all parse silently-wrong. **Resolution:** added a strict
  `TryParse` (out result, out error) to each parser that enforces a defined grammar (documented in
  XML-doc; encoded in the test suites); `Parse` stays total/lenient for the Format↔Parse round-trip.
  The inline-edit VMs (`ClassMemberViewModel`/`EntityColumnViewModel`) now commit via `TryParse` and
  **revert** on invalid input (model keeps its last-good value) instead of committing a degraded model.
- **6b · Risk Low · ✅ done** — `ImageNode.Clone` (`ImageNode.cs`) copied the `byte[]` **by reference**.
  Fixed with `Data = (byte[])Data.Clone()`; covered by `ImageNodeTests`. (Sibling node `Clone`s already
  deep-copy their collections — ImageNode was the lone offender.)
- **6c · Risk Low** — Centralize the fragmented epsilons in `ShapeBoundary` (`double.Epsilon` vs
  `1e-9` vs `1e-12`, ~5 inline constants across `IntersectEllipse` / `TryIntersectSegment`) into named
  geometry-tolerance constants; document the rationale.
- **6d · Risk Low** — Serialization forward-proofing: `JsonDocumentSerializer` only rejects
  `SchemaVersion > Current`, and `Clone` is a serialize→deserialize round-trip. Fine while
  `Current == 1`, but add a tiny migration seam (a version→upgrade map) and a comment before the first
  schema bump, so old files aren't silently mishandled later.

### Priority 7 — Minor polish · Effort S · Risk Low
- `RoundedRouter` magic numbers (`20d`, `0.4`) — name / document them.
- Extract the repeated router anchor-resolution (`ShapeBoundary` calls duplicated across
  Straight / Orthogonal / Rounded routers) into a `RouteHelpers` method.
- `NodeViewModelBase`: optional helper for the `Text`/`Label` inline-label property echoed in
  `ShapeNodeViewModel` / `UseCaseNodeViewModel` / `ActorNodeViewModel` (low value — intentionally
  explicit).
- `KeymapService.WriteExampleIfMissing` swallows all write exceptions silently; at least log.
- `KeymapService` doc comment says `%APPDATA%/Draw` — reword to note it's per-OS app data (the code is
  already cross-platform).

## Execution sequence

One **feature branch per item** (phase-style names, e.g. `feature/code-review-test-safety-net`).
Recommended order: **1 → 6a/6b (quick correctness wins, now test-covered) → 2a → 3a → 4 → 2b/2c →
3b → 5 → 7.** Tests first, then small low-risk wins, then the phased decompositions.

## Verification (per item, since there is no CI gate)

- `dotnet build Draw.slnx` must stay clean — nullable warnings are build errors, so refactors can't
  silently regress null-handling.
- After item 1: `dotnet test` green; keep it green through every later item (regression guard for the
  pure-logic refactors in 2c / 3a / 3b / 6).
- Review real changes with `git diff --ignore-cr-at-eol` (CRLF noise in the working tree).
- **Visual items (2, 4b, 5a)** can't be verified under WSL2/headless — run on Windows/macOS:
  drag / resize / marquee / connector-edit still behave (2); the actor renders identically on canvas
  **and** in SVG export (4b); all dialogs look consistent (5a).
