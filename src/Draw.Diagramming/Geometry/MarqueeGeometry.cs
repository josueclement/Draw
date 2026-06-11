using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>
/// Framework-agnostic test for whether a marquee (selection rectangle) grabs a connector. A connector
/// is given to this code as the flattened polyline of its route (curves sampled into segments). The
/// rule matches how shapes select on overlap: the connector is hit if any part of its line lies inside
/// or crosses the rectangle — including a long straight segment whose endpoints sit outside the box but
/// whose body passes through it.
/// </summary>
public static class MarqueeGeometry
{
    /// <summary>
    /// True when the axis-aligned <paramref name="rect"/> overlaps the polyline through
    /// <paramref name="points"/>. The rectangle is normalized first, so a marquee dragged in any
    /// direction works. An empty point list is never hit; a single point is hit when it is inside.
    /// </summary>
    public static bool IntersectsPolyline(Rect2D rect, IReadOnlyList<Point2D> points)
    {
        if (points.Count == 0)
        {
            return false;
        }

        Rect2D r = rect.Normalized();
        if (points.Count == 1)
        {
            return r.Contains(points[0]);
        }

        for (int i = 1; i < points.Count; i++)
        {
            if (SegmentIntersectsRect(points[i - 1], points[i], r))
            {
                return true;
            }
        }

        return false;
    }

    // Liang–Barsky segment/AABB test: clips the parametric segment a→b against the four slabs of the
    // rectangle and reports whether any portion (endpoints included) survives. Returns true when the
    // segment touches or crosses the box.
    private static bool SegmentIntersectsRect(Point2D a, Point2D b, Rect2D r)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        double u1 = 0d;
        double u2 = 1d;

        return Clip(-dx, a.X - r.Left, ref u1, ref u2)
            && Clip(dx, r.Right - a.X, ref u1, ref u2)
            && Clip(-dy, a.Y - r.Top, ref u1, ref u2)
            && Clip(dy, r.Bottom - a.Y, ref u1, ref u2);
    }

    // One Liang–Barsky slab clip. p is the (signed) edge direction, q the distance to the edge. Narrows
    // the surviving parameter window [u1,u2]; returns false once it becomes empty (segment is outside).
    private static bool Clip(double p, double q, ref double u1, ref double u2)
    {
        if (p == 0d)
        {
            // Segment is parallel to this edge: inside the slab only when already on the correct side.
            return q >= 0d;
        }

        double t = q / p;
        if (p < 0d)
        {
            if (t > u2)
            {
                return false;
            }

            if (t > u1)
            {
                u1 = t;
            }
        }
        else
        {
            if (t < u1)
            {
                return false;
            }

            if (t < u2)
            {
                u2 = t;
            }
        }

        return true;
    }
}
