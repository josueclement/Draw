using System;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>The zoom factor and pan offsets (screen px) that fit content into a viewport.</summary>
public readonly record struct ViewportFit(double Zoom, double PanX, double PanY);

/// <summary>
/// Framework-agnostic viewport arithmetic: fitting content to the visible area and mapping the viewport
/// centre back to world coordinates. Pairs with <see cref="ViewportScrollMath"/> (which maps pan/zoom to
/// the scrollbars); both keep the zoom/pan algebra out of the view model so it can be unit-tested.
/// </summary>
public static class ViewportMath
{
    /// <summary>
    /// Zoom/pan that centres <paramref name="content"/> in a <paramref name="viewportWidth"/> ×
    /// <paramref name="viewportHeight"/> viewport, padded by <paramref name="margin"/> and never enlarged
    /// past 100%, with the zoom clamped to [<paramref name="minZoom"/>, <paramref name="maxZoom"/>].
    /// </summary>
    public static ViewportFit FitToContent(
        Rect2D content,
        double viewportWidth,
        double viewportHeight,
        double minZoom,
        double maxZoom,
        double margin)
    {
        Rect2D b = content.Inflate(margin);
        double fit = Math.Min(viewportWidth / b.Width, viewportHeight / b.Height);
        double zoom = Math.Clamp(Math.Min(fit, 1d), minZoom, maxZoom);
        double panX = (viewportWidth / 2d) - (b.Center.X * zoom);
        double panY = (viewportHeight / 2d) - (b.Center.Y * zoom);
        return new ViewportFit(zoom, panX, panY);
    }

    /// <summary>The world-space point under the centre of the viewport (guards a non-positive zoom).</summary>
    public static Point2D CenterToWorld(
        double viewportWidth,
        double viewportHeight,
        double panX,
        double panY,
        double zoom)
    {
        double z = zoom <= 0 ? 1d : zoom;
        return new Point2D(((viewportWidth / 2d) - panX) / z, ((viewportHeight / 2d) - panY) / z);
    }
}
