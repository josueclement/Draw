namespace Draw.Model.Connectors;

/// <summary>
/// ER crow's-foot cardinality at one end of a connector. <see cref="Unspecified"/> draws no end
/// symbol — that end falls back to the decoration implied by the connector's <see cref="RelationshipKind"/>.
/// </summary>
public enum Cardinality
{
    Unspecified = 0,
    One = 1,
    Many = 2,
    ZeroOrOne = 3,
    OneOrMany = 4,
    ZeroOrMany = 5,
}
