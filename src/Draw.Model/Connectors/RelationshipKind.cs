namespace Draw.Model.Connectors;

/// <summary>
/// Semantic relationship a connector represents. The full UML set is modeled now;
/// rendering of the decorations arrives in Phase 2.
/// </summary>
public enum RelationshipKind
{
    Association = 0,
    DirectedAssociation = 1,
    Aggregation = 2,
    Composition = 3,
    Generalization = 4,
    Realization = 5,
    Dependency = 6,
    Include = 7,
    Extend = 8,

    /// <summary>A plain ER relationship line. Its crow's-foot end symbols come from the connector's
    /// per-end <see cref="Cardinality"/>, not from this kind.</summary>
    Relationship = 9,

    /// <summary>A mind-map branch: rendered as a filled, depth-scaled tapered ribbon (thick near the
    /// central topic, thinner toward the leaves) with no end decorations. Its width is derived from the
    /// source node's depth from the root (see <c>MindMapHierarchy</c>/<c>MindMapBranchStyle</c>), so it
    /// must be attached source = parent, target = child.</summary>
    MindMapBranch = 10,
}
