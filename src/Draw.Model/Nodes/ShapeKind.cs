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

    // Flowchart shapes (Phase 2 of the shape-library expansion).

    /// <summary>A terminator (stadium/pill): a rectangle with fully semicircular left/right ends.</summary>
    Terminator = 16,

    /// <summary>A database cylinder; routing uses the bounding rectangle.</summary>
    Cylinder = 17,

    /// <summary>A document: a rectangle with a wavy bottom edge; routing uses the bounding rectangle.</summary>
    Document = 18,

    /// <summary>A predefined process (subroutine): a rectangle with double vertical bars.</summary>
    PredefinedProcess = 19,

    /// <summary>A manual-input symbol: a quadrilateral with a slanted top edge.</summary>
    ManualInput = 20,

    /// <summary>An off-page connector: a rectangle tapering to a point at the bottom (home-plate).</summary>
    OffPageConnector = 21,

    /// <summary>A display symbol; routing uses the bounding rectangle.</summary>
    Display = 22,

    /// <summary>A delay symbol: a rectangle with a semicircular right end; routing uses the bounding rectangle.</summary>
    Delay = 23,
}
