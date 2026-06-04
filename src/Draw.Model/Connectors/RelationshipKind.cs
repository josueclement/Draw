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
}
