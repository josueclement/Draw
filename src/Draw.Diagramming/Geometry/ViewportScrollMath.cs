using System;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>The computed state of one scrollbar: whether it is needed plus its range/thumb values.</summary>
public readonly record struct ScrollAxis(bool Needed, double Minimum, double Maximum, double ViewportSize, double Value);

/// <summary>The scrollable region (world coords) plus both axes' computed scrollbar state.</summary>
public readonly record struct ScrollBarLayout(Rect2D Region, ScrollAxis Horizontal, ScrollAxis Vertical);

/// <summary>
/// Framework-agnostic mapping between an infinite-canvas pan/zoom state and the two scrollbars. The
/// scrollable region is the padded content unioned with the current view (so panning past the content
/// is always possible); each bar is needed only when its axis overflows the view. Pan stays unbounded —
/// the bars merely reflect and adjust it.
/// </summary>
public static class ViewportScrollMath
{
    /// <summary>Half-pixel slack so an exact fit does not flicker a bar on/off.</summary>
    public const double DefaultEpsilon = 0.5d;

    /// <summary>
    /// Computes the scrollable region and both scrollbars from the content bounds and the currently
    /// visible region (both in world coordinates). <paramref name="contentMargin"/> pads the content so
    /// you can scroll a little past the outermost shapes.
    /// </summary>
    public static ScrollBarLayout Compute(Rect2D content, Rect2D visibleWorld, double contentMargin, double epsilon = DefaultEpsilon)
    {
        Rect2D region = content.Inflate(contentMargin).Union(visibleWorld);
        ScrollAxis horizontal = Axis(region.Left, region.Width, visibleWorld.Left, visibleWorld.Width, epsilon);
        ScrollAxis vertical = Axis(region.Top, region.Height, visibleWorld.Top, visibleWorld.Height, epsilon);
        return new ScrollBarLayout(region, horizontal, vertical);
    }

    /// <summary>Maps a scrollbar value back to the pan offset (screen px) along one axis.</summary>
    public static double PanForScrollValue(double regionStart, double scrollValue, double zoom)
        => -(regionStart + scrollValue) * zoom;

    private static ScrollAxis Axis(double regionStart, double regionLength, double viewStart, double viewLength, double epsilon)
    {
        bool needed = regionLength > viewLength + epsilon;
        double maximum = Math.Max(0d, regionLength - viewLength);
        double value = Math.Clamp(viewStart - regionStart, 0d, maximum);
        return new ScrollAxis(needed, 0d, maximum, viewLength, value);
    }
}
