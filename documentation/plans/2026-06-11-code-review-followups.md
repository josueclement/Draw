# Code review — next steps (follow-ups to the 2026-06-11 remediation)

**Date:** 2026-06-11
**Context:** The Tier-C remediation of `documentation/reviews/2026-06-11-code-review.md` is
**code-complete** (see `2026-06-11-code-review-remediation.md` for the per-phase log). Build is
**0-warning**; tests **220 → 299 green**. All work sits **unstaged** on
`review/2026-06-11-fresh-code-review` — nothing has been committed. This file is the forward backlog:
what to do to close the cycle, the findings that were intentionally left, and where the *next* review
pass should look.

---

## 1. Gate before "done" (blocking — these are not optional)

1. **Manual GUI verification on Windows/macOS.** Every Phase 4–6 change to the view, view models and
   rendering is **build-verified only** — WSL2/headless can't render the GUI, so none of it has been
   run. Work the per-phase checklists:
   - **Nodes (P3):** place every kind — single undo step, snap-to-grid, system boundary draws behind
     its use-cases, new node lands selected; paste/duplicate/insert-image z-order.
   - **Overlay (P2/P4):** resize handles track zoom/pan/selection; marquee; connector endpoint
     (filled/hollow) + waypoint handles; amber reference outlines; selection-halo + reference-dot
     colours (S2).
   - **Inline editing (P4):** double-tap a class field / operation / enum literal **and** an entity
     column → Enter-adds-next, Tab/Shift+Tab, Alt+↑/↓ reorder, blank-row discard, the two context menus.
   - **Toolbox/Inspector (P5):** each dropdown arms + updates its header and resets the others; status
     hint; select-tool cursor; every inspector panel shows for its selection; single-click /
     shift-toggle / marquee / mixed node+connector / click-empty-clears / undo-redo selection.
   - **Colours (P6):** pick *exactly* the default fill → it **stays put on a theme toggle** (the bug);
     a normal default node still follows the theme; open an old `.draw` (still themed); SVG/PNG export
     colours correct; zoom limits (B3).
2. **Commit.** Suggested split: (a) `.gitattributes` + `.editorconfig`; (b) the C1 nullable-colour +
   **schema-v2** change on its own (it changes the on-disk format — older app builds won't read v2
   files); (c) the remaining refactors/tests. Review real diffs with `git diff --ignore-cr-at-eol`.

---

## 2. Remaining findings backlog (carried over; all Low/Nit unless noted)

**Status (2026-06-13): all of §2 is done** — D6, D7, P3, S1, B4, B5 **and S3** (unstaged on the same
branch alongside the Tier-C work; build is 0-warning in Debug + Release, tests 299 → **321** green).
Resolution notes below.

| # | Finding | Sev | Status | Resolution |
|---|---|---|---|---|
| 1 | **D6** `MainWindow` repetition — export trio share a get-view→guard→produce→error skeleton; `WireAlign*Dropdown` near-identical | Low | ✅ done | Extracted `RunWithErrorDialogAsync(title, action)` (catches the union of the prior filters) for the export/copy tail and a single `WireAlignmentDropdown(dropDown, apply)` helper replacing both wire methods. |
| 2 | **D7** "MainWindow's clipboard/storage provider" lookup repeated in `FileDialogService`, `ImageExportService`, `ClipboardService` | Nit | ✅ done | New `Services/DesktopHost` exposes `StorageProvider`/`Clipboard`; all three sites route through it. |
| 3 | **P3** `SvgFormat.Escape` chains five `string.Replace` (export-only) | Nit | ✅ done | `Escape`/`Num` moved to a **pure** `Draw.Diagramming/Formatting/SvgText` (single-pass, allocation-free when nothing escapes); `SvgFormat` now forwards. New headless `SvgTextTests` (the backlog assumed Diagramming; `SvgFormat` was actually in App — hence the split). |
| 4 | **S1** some service files hold multiple top-level types vs one-type-per-file in Model/Diagramming | Nit | ✅ done | Split every `App/Services/I*.cs` to one-type-per-file (interface, impl, and enums in their own files), matching the `Undo/IUndoService.cs`+`MementoUndoService.cs` precedent. |
| 5 | **B4** `WarningsAsErrors=nullable` only — analyzer (CAxxxx) warnings don't fail the build | Low | ✅ done | `Directory.Build.props` now `TreatWarningsAsErrors=true`. Verified clean in Debug + Release; **no `WarningsNotAsErrors` exemptions needed**. |
| 6 | **B5** `IConfiguration`/`IOptions` wired but no `appsettings.json` ships | Nit | ✅ done | Shipped `src/Draw.App/appsettings.json` mirroring the in-code defaults (`Editor`/`RecentFiles`/`Keymap`/`Undo`), copied to output via `Content … PreserveNewest`. Defaults unchanged, so behaviour is identical. |
| 7 | **S3** `MainWindow.axaml` is large (~860 lines, declarative ribbon/menu) | Nit | ✅ done | Moved the two static tool menus (~95 lines) into `Resources/ToolMenus.axaml`, merged into `Window.Resources`; resource keys + code-behind `FindResource` wiring unchanged. **Build-only verified — confirm the Shift+S/Shift+C menus still open with correct icons in the GUI pass (§1).** |

---

## 3. Partially done — the A1 "continue peeling" tail (High, optional)

A1 (god `DiagramDocumentViewModel`) was *advanced*, not finished: the node-creation factory
(`PlaceNode`), connector hit-test geometry (`SegmentGeometry`) and a `SelectionCoordinator` were
extracted.

**2026-06-13 — viewport/zoom/pan/fit peeled out.** The pure arithmetic moved to a headless-tested
`Draw.Diagramming/Geometry/ViewportMath` (`FitToContent` + `CenterToWorld`, neighbour to the existing
`ViewportScrollMath`; `ViewportMathTests` covers it), and the zoom/pan/fit *operations* moved to a
`ViewportCoordinator` over a new `IViewportHost` seam — mirroring `SelectionCoordinator`. The VM keeps
the bound `Zoom`/`PanX`/`PanY`/`ViewportWidth/Height` properties (so the `DiagramView` binding contract
is untouched) and its zoom commands now forward to the coordinator. **Build-only verified — fit-to-
content, zoom in/out/reset, Ctrl+wheel zoom and paste-centring need the §1 GUI pass.**

**2026-06-13 — style application + theme refresh peeled out.** A `StyleCoordinator` (over
`IDocumentEditContext` + `IThemeService`) now owns `ApplyStyleSwatch`/`ResetStyleToDefault`/`ApplyNoFill`
+ the shared apply-to-selection body, and the on-theme-change brush refresh. It subscribes to
`IThemeService.ThemeChanged` for its lifetime and is `IDisposable` (the VM disposes it on tab close).
The VM keeps thin public forwarders (so the inspector/`StylePaletteViewModel` bindings are unchanged)
and `NotifyStyleEditStarting` (an undo seam); `IDocumentEditContext` gained `SelectedConnectors`.
**Build-only verified — swatch apply, reset-to-default, no-fill, and the light/dark theme-toggle
recolour need the §1 GUI pass.**

**Still in the VM** and candidate for a future pass: undo orchestration (`Undo`/`Redo`/`RaiseUndoState`)
and the full-collection rebuild (`RebuildNodes`/`RebuildConnectors`). The review framed A1 as "keep
peeling, not rewrite" — pursue only if the VM's churn/size warrants it; each remaining extraction is
view/VM (no headless test net), so it needs the same manual-GUI discipline as Phases 4–5.

---

## 4. Deferred by design (recorded so the next review doesn't re-flag them)

- **T1d** — orchestration tests for `AlignmentCoordinator`/`ConnectorSpacingCoordinator`/`ZOrderCoordinator`.
  Not cleanly headless: they operate over `NodeViewModelBase`, an Avalonia type. Their pure cores are
  tested in `Draw.Diagramming`. A real fix is an `INodePlacement` seam — a separate, larger item.
- **P1** — undo snapshots serialize the whole document each gesture. Conscious trade-off (sidesteps the
  clone-drift risk now guarded by `CloneCompletenessTests`); revisit only for very large diagrams.
- **P2** — `RaiseSelectionChanged` fires ~26 notifications. Imperceptible at normal sizes; revisit only
  if selection becomes a hot path.

---

## 5. What the *next* full review pass should focus on

Once §1 is green and committed, a fresh pass should scrutinise the **new abstractions** this
remediation introduced (they're the highest-churn surface now):

- **`NodeKindRegistry`** (E1): confirm every node kind is registered (startup self-validation +
  `NodeKindContractTests` enforce the discriminator/round-trip side); check the XAML `DataTemplate`
  contract comment is honoured when a kind is added (still the one un-compiler-checked site).
- **`OverlayController` / `InlineRowEditController<TRow>`** (A2/D1): verify behaviour parity after the
  pure-move (this is what §1's GUI pass covers) and that the gesture/marquee/connect-preview split with
  the view still reads cleanly.
- **`ToolboxViewModel.ArmedTool` / `InspectorSelection`** (E2/A3): re-check the preserved binding
  surface against `MainWindow.axaml` after any further toolbox/inspector edits.
- **Schema v2 / C1**: as ER (Phase 5) and later kinds add styled elements, keep the `Migrate` seam and
  the null-colour round-trip tests current; decide whether the `DefaultShapeStyle` (palette-based) and
  `CreateDefault` (null fill) split is the intended long-term default-style story.
- **Re-baseline severities**: re-run `dotnet build Draw.slnx` (0 warnings) + `dotnet test --solution
  Draw.slnx` (299) before the pass so findings aren't confounded by pre-existing breakage.

---

## References
- Original review: `documentation/reviews/2026-06-11-code-review.md`
- Remediation log (per-phase status): `documentation/plans/2026-06-11-code-review-remediation.md`
- Verification reality (headless vs GUI) and the test layout: `documentation/architecture.md` (Verification)
