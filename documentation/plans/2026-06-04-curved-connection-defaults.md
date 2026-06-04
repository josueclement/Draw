# Curved connections by default (auto-pin + rounded route) — Implementation plan

Branch: `feature/curved-connection-defaults`. Builds on
`documentation/specs/2026-06-03-connector-rounded-route-design.md` (the Rounded router) and
`documentation/plans/2026-06-04-force-pin-on-arrange.md` (the side-centre pinning math).

Make a freshly drawn connection look like a nice curve the instant it's created, by changing the
defaults at connection-creation time. Both pieces of machinery already exist — this only changes
what `AddConnector` applies to new connectors:

1. **Default route = `RouteStyle.Rounded`** (was `Straight`).
2. **Auto-pin each end to the centre of the side it naturally attaches to.** This is load-bearing:
   the rounded router's no-bend curve bows *only* when an endpoint's outward direction is off the
   straight line between the shapes. An unpinned end ray-casts toward the other shape's centre, so
   both ends aim dead-on at each other and the rounded route renders **straight**. A side-centre
   anchor gives the cardinal outward normal that makes it curve immediately.

Scope: all diagram types (Freeform, Class, UseCase, Er) and any future ones — one creation path,
applied unconditionally. New connections only; existing/saved connectors are untouched.

## Steps

1. **Document VM** — `Draw.App/ViewModels/DiagramDocumentViewModel.cs` `AddConnector`: set the new
   connector's `Route = RouteStyle.Rounded`. After building the `ConnectorViewModel`, read both
   `RouteStart`/`RouteEnd` from the initial auto-route, classify each to a `BoxSide`
   (`ConnectionDistributor.ClassifySide`), then pin each end to its side centre via
   `SetSourceAnchor`/`SetTargetAnchor` with `ConnectionDistributor.EvenAnchor(side, 0, 1)` (= 0.5,
   the same lone-end centring the **Space connections** action uses). Both sides are classified
   *before* pinning either, since pinning the source recomputes the route. The existing
   `CaptureUndo()` at the top of `AddConnector` covers create + pin as one undo step
   (`SetSourceAnchor`/`SetTargetAnchor` don't capture undo themselves).

2. **Build** `dotnet build Draw.slnx` clean; then manual verification on Windows (no GUI under WSL2).

No model change: `Connector.Route`'s default stays `Straight` (deserialization fallback, preserves
existing files). No UI/options change; copy-paste connector cloning is untouched.

## Status

- [x] 1 Document VM (`AddConnector` rounded + side-centre auto-pin) · [x] 2 Build (clean, 0 warnings)

Implemented on `feature/curved-connection-defaults`; build clean (nullable-as-error). Pending: manual
verification on Windows (no GUI under WSL2):
1. New Freeform diagram → two shapes apart → connector tool → connect them: a visibly **curved**
   connector appears immediately, each end pinned (filled handle) at the centre of its facing side.
2. Move a shape → the curve updates and the ends keep their relative (side-centre) positions.
3. Undo once → the connector and its pinning are removed in a single step.
4. Repeat in a Class (UML) diagram → rounded + pinned applies there too.
5. Open a previously-saved diagram → existing connectors keep their original route/look (no migration).
