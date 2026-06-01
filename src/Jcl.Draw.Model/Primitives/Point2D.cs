namespace Jcl.Draw.Model.Primitives;

/// <summary>
/// A framework-agnostic 2D point in diagram (document) coordinates.
/// </summary>
public readonly record struct Point2D(double X, double Y)
{
    public static Point2D Origin => new(0, 0);

    public Point2D Offset(double dx, double dy) => new(X + dx, Y + dy);

    public Point2D Offset(Point2D delta) => new(X + delta.X, Y + delta.Y);

    public static Point2D operator +(Point2D a, Point2D b) => new(a.X + b.X, a.Y + b.Y);

    public static Point2D operator -(Point2D a, Point2D b) => new(a.X - b.X, a.Y - b.Y);

    public double DistanceTo(Point2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return System.Math.Sqrt((dx * dx) + (dy * dy));
    }
}
