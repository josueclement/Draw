namespace Draw.Model.Nodes;

/// <summary>The basic (non-UML) shape primitives available in Phase 1.</summary>
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
}
