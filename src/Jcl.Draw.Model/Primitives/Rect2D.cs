namespace Jcl.Draw.Model.Primitives;

/// <summary>
/// An axis-aligned rectangle in diagram (document) coordinates. Width/Height are kept
/// non-negative by callers; use <see cref="Normalized"/> when they may be negative
/// (e.g. while dragging a marquee).
/// </summary>
public readonly record struct Rect2D(double X, double Y, double Width, double Height)
{
    public static Rect2D Empty => new(0, 0, 0, 0);

    public double Left => X;
    public double Top => Y;
    public double Right => X + Width;
    public double Bottom => Y + Height;

    public Point2D Position => new(X, Y);
    public Size2D Size => new(Width, Height);
    public Point2D Center => new(X + (Width / 2), Y + (Height / 2));

    public static Rect2D FromLtrb(double left, double top, double right, double bottom)
        => new(left, top, right - left, bottom - top);

    public static Rect2D FromPoints(Point2D a, Point2D b)
        => FromLtrb(
            System.Math.Min(a.X, b.X),
            System.Math.Min(a.Y, b.Y),
            System.Math.Max(a.X, b.X),
            System.Math.Max(a.Y, b.Y));

    /// <summary>Returns an equivalent rectangle with non-negative width and height.</summary>
    public Rect2D Normalized()
    {
        double x = Width < 0 ? X + Width : X;
        double y = Height < 0 ? Y + Height : Y;
        return new Rect2D(x, y, System.Math.Abs(Width), System.Math.Abs(Height));
    }

    public bool Contains(Point2D p)
        => p.X >= Left && p.X <= Right && p.Y >= Top && p.Y <= Bottom;

    public bool Contains(Rect2D other)
        => other.Left >= Left && other.Right <= Right && other.Top >= Top && other.Bottom <= Bottom;

    public bool IntersectsWith(Rect2D other)
        => other.Left <= Right && other.Right >= Left && other.Top <= Bottom && other.Bottom >= Top;

    public Rect2D Inflate(double amount) => Inflate(amount, amount);

    public Rect2D Inflate(double dx, double dy)
        => new(X - dx, Y - dy, Width + (2 * dx), Height + (2 * dy));

    public Rect2D Translate(double dx, double dy) => new(X + dx, Y + dy, Width, Height);

    public Rect2D WithPosition(Point2D position) => new(position.X, position.Y, Width, Height);

    public Rect2D WithSize(Size2D size) => new(X, Y, size.Width, size.Height);

    public Rect2D Union(Rect2D other)
    {
        double left = System.Math.Min(Left, other.Left);
        double top = System.Math.Min(Top, other.Top);
        double right = System.Math.Max(Right, other.Right);
        double bottom = System.Math.Max(Bottom, other.Bottom);
        return FromLtrb(left, top, right, bottom);
    }
}
