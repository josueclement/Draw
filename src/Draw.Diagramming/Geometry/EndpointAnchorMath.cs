using System;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>Framework-agnostic math for pinning a connector endpoint to a relative point on a node.</summary>
public static class EndpointAnchorMath
{
    /// <summary>
    /// The relative (u, v) anchor in [0, 1]² for a world point dropped on a node's <paramref name="bounds"/>.
    /// A zero-width or zero-height bound collapses that axis to the centre (0.5).
    /// </summary>
    public static Point2D RelativeAnchor(Rect2D bounds, Point2D world)
    {
        double u = bounds.Width <= 0 ? 0.5 : Math.Clamp((world.X - bounds.X) / bounds.Width, 0d, 1d);
        double v = bounds.Height <= 0 ? 0.5 : Math.Clamp((world.Y - bounds.Y) / bounds.Height, 0d, 1d);
        return new Point2D(u, v);
    }
}
