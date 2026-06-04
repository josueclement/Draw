namespace Draw.Model.Connectors;

/// <summary>Geometry used to route a connector between two nodes.</summary>
public enum RouteStyle
{
    Straight = 0,
    Orthogonal = 1,

    /// <summary>
    /// A smooth curve averaged from the bend points: it passes through the two attachment
    /// endpoints and the midpoints between consecutive bend points, with the bend points acting
    /// as pull-handles. With no bend points it is a gentle S-curve.
    /// </summary>
    Rounded = 3,
}
