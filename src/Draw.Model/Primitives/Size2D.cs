namespace Draw.Model.Primitives;

/// <summary>
/// A framework-agnostic width/height pair in diagram (document) coordinates.
/// </summary>
public readonly record struct Size2D(double Width, double Height)
{
    public static Size2D Empty => new(0, 0);

    public bool IsEmpty => Width <= 0 || Height <= 0;
}
