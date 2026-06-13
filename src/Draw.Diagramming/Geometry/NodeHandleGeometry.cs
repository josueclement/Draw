using System;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>The eight resize handles of a node's bounding box, in the canonical clockwise order
/// (NW=0, N=1, NE=2, E=3, SE=4, S=5, SW=6, W=7) used by the resize gesture.</summary>
public enum NodeHandle
{
    NW = 0,
    N = 1,
    NE = 2,
    E = 3,
    SE = 4,
    S = 5,
    SW = 6,
    W = 7,
}

/// <summary>
/// Pure, Avalonia-free geometry for the eight node resize handles: their positions, hit-testing,
/// and the resized bounds (with min-size clamp and optional aspect lock). Lifted out of the view
/// code-behind so it can be unit-tested; the view maps a node view model to its <see cref="Rect2D"/>
/// and converts the returned points to framework points.
/// </summary>
public static class NodeHandleGeometry
{
    /// <summary>Number of resize handles on a node.</summary>
    public const int HandleCount = 8;

    /// <summary>The eight handle centres of <paramref name="bounds"/> in canonical clockwise order
    /// (NW, N, NE, E, SE, S, SW, W).</summary>
    public static Point2D[] HandlePositions(Rect2D bounds)
    {
        double cx = bounds.X + (bounds.Width / 2);
        double cy = bounds.Y + (bounds.Height / 2);
        return new[]
        {
            new Point2D(bounds.Left, bounds.Top),     // 0 NW
            new Point2D(cx, bounds.Top),              // 1 N
            new Point2D(bounds.Right, bounds.Top),    // 2 NE
            new Point2D(bounds.Right, cy),            // 3 E
            new Point2D(bounds.Right, bounds.Bottom), // 4 SE
            new Point2D(cx, bounds.Bottom),           // 5 S
            new Point2D(bounds.Left, bounds.Bottom),  // 6 SW
            new Point2D(bounds.Left, cy),             // 7 W
        };
    }

    /// <summary>Index of the handle within <paramref name="tolerance"/> of <paramref name="world"/> on
    /// both axes (square tolerance), or -1 if none.</summary>
    public static int HitTest(Rect2D bounds, Point2D world, double tolerance)
    {
        Point2D[] positions = HandlePositions(bounds);
        for (int i = 0; i < positions.Length; i++)
        {
            if (Math.Abs(world.X - positions[i].X) <= tolerance && Math.Abs(world.Y - positions[i].Y) <= tolerance)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>The new bounds for dragging <paramref name="handle"/> to <paramref name="world"/>,
    /// honouring the minimum size and (when <paramref name="locksAspect"/>) the original aspect ratio.</summary>
    public static Rect2D Resize(Rect2D bounds, int handle, Point2D world, double minWidth, double minHeight, bool locksAspect)
    {
        double l = bounds.Left;
        double t = bounds.Top;
        double r = bounds.Right;
        double bottom = bounds.Bottom;

        switch (handle)
        {
            case 0: l = world.X; t = world.Y; break;
            case 1: t = world.Y; break;
            case 2: r = world.X; t = world.Y; break;
            case 3: r = world.X; break;
            case 4: r = world.X; bottom = world.Y; break;
            case 5: bottom = world.Y; break;
            case 6: l = world.X; bottom = world.Y; break;
            case 7: l = world.X; break;
        }

        double left = Math.Min(l, r);
        double top = Math.Min(t, bottom);
        double width = Math.Max(minWidth, Math.Abs(r - l));
        double height = Math.Max(minHeight, Math.Abs(bottom - t));
        Rect2D result = new(left, top, width, height);

        if (locksAspect && bounds.Width > 0 && bounds.Height > 0)
        {
            result = LockAspect(handle, bounds, result, minWidth, minHeight);
        }

        return result;
    }

    // Re-proportions an unconstrained resize so the node keeps its original aspect ratio, anchored at the
    // fixed corner/edge of the dragged handle. Corner handles scale from the opposite corner; edge handles
    // scale (driven by the moved axis) and re-centre on the fixed edge.
    private static Rect2D LockAspect(int handle, Rect2D original, Rect2D raw, double minWidth, double minHeight)
    {
        double ratio = original.Width / original.Height;

        double scale = handle switch
        {
            1 or 5 => raw.Height / original.Height,                                   // N / S: height-driven
            3 or 7 => raw.Width / original.Width,                                     // E / W: width-driven
            _ => Math.Max(raw.Width / original.Width, raw.Height / original.Height),  // corners: dominant axis
        };

        // Don't let either dimension fall below its minimum.
        scale = Math.Max(scale, Math.Max(minWidth / original.Width, minHeight / original.Height));

        double newWidth = original.Width * scale;
        double newHeight = newWidth / ratio;

        double cx = original.Center.X;
        double cy = original.Center.Y;
        (double newLeft, double newTop) = handle switch
        {
            0 => (original.Right - newWidth, original.Bottom - newHeight),  // NW: fix SE corner
            2 => (original.Left, original.Bottom - newHeight),              // NE: fix SW corner
            4 => (original.Left, original.Top),                             // SE: fix NW corner
            6 => (original.Right - newWidth, original.Top),                 // SW: fix NE corner
            1 => (cx - (newWidth / 2), original.Bottom - newHeight),        // N: fix bottom edge
            5 => (cx - (newWidth / 2), original.Top),                       // S: fix top edge
            3 => (original.Left, cy - (newHeight / 2)),                     // E: fix left edge
            _ => (original.Right - newWidth, cy - (newHeight / 2)),         // W (7): fix right edge
        };

        return new Rect2D(newLeft, newTop, newWidth, newHeight);
    }
}
