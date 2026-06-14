# Shape library expansion — roadmap

Adds 20 new `ShapeKind` shapes and 3 UML structural node types so Draw can produce real
flowchart, geometric, and UML/architecture diagrams. See `documentation/architecture.md` for the
canonical layering; this roadmap tracks the expansion only.

Design decisions: UML nodes are labeled shapes (single editable title + decoration, no
nesting/compartments, routed as a rectangle); new shapes are grouped into separate Insert-ribbon
dropdowns (Shapes / Flowchart / Arrows) with matching Shift+S tool-menu submenus.

| Phase | Scope | Branch | Status |
|-------|-------|--------|--------|
| 1 | Geometric: Hexagon, Pentagon, Octagon, Star, Cross, Cloud, Callout | `phase01-geometric-shapes` | Code complete — awaiting visual verify + commit |
| 2 | Flowchart: Terminator, Cylinder, Document, Predefined process, Manual input, Off-page connector, Display, Delay | `phase02-flowchart-shapes` | Not started |
| 3 | Block arrows: right, left, up, down, bidirectional | `phase03-block-arrows` | Not started |
| 4 | UML structural nodes: Package, Component, Deployment | `phase04-uml-structural-nodes` | Not started |

## Per-shape technique

- **Polygon** (outline only — both render builders fall through to `Polygon(ShapeOutline.GetPolygon)`):
  Hexagon, Pentagon, Octagon, Star, Cross, Manual input, Off-page connector, all 5 arrows.
- **Curved/multi-figure** (explicit geometry in `ShapeGeometryBuilder` + `ShapeSvgPathBuilder`,
  approximate polygon in `ShapeOutline` for routing): Terminator, Cylinder, Document,
  Predefined process, Display, Delay, Cloud, Callout.
</content>
