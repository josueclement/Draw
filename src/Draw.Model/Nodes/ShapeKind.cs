namespace Draw.Model.Nodes;

/// <summary>
/// The (non-UML) shape primitives. Members are serialized by their integer value, so new kinds must
/// be appended and existing values never renumbered.
/// </summary>
public enum ShapeKind
{
    Rectangle = 0,
    RoundedRectangle = 1,
    Ellipse = 2,
    Circle = 3,
    Diamond = 4,
    Parallelogram = 5,
    Trapezoid = 6,
    Triangle = 7,

    /// <summary>A UML note: a rectangle with a folded top-right corner.</summary>
    Note = 8,

    // Geometric shapes (Phase 1 of the shape-library expansion).

    /// <summary>A regular hexagon (flat top and bottom).</summary>
    Hexagon = 9,

    /// <summary>A regular upward-pointing pentagon.</summary>
    Pentagon = 10,

    /// <summary>A regular octagon.</summary>
    Octagon = 11,

    /// <summary>A five-pointed star.</summary>
    Star = 12,

    /// <summary>A plus/cross with equal arms.</summary>
    Cross = 13,

    /// <summary>A cloud (lobed outline); routing uses an approximate polygon.</summary>
    Cloud = 14,

    /// <summary>A rounded speech callout with a tail; routing uses an approximate polygon.</summary>
    Callout = 15,
}
