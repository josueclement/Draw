using System;
using Avalonia;
using Avalonia.Media;
using static Draw.App.Rendering.SvgFormat;

namespace Draw.App.Rendering;

/// <summary>
/// Builds the outline <see cref="Geometry"/> for UML structural nodes that need a custom shape
/// (currently the deployment node's 3D box). Package and component compose plain borders in XAML.
/// </summary>
public static class UmlNodeGeometry
{
    /// <summary>The 3D depth (offset of the back faces) of a deployment box of the given size.</summary>
    public static double DeploymentDepth(double width, double height)
        => Math.Min(Math.Min(width, height) * 0.22d, Math.Min(width, height) / 2d);

    /// <summary>
    /// A cuboid: a filled silhouette plus stroke-only inner edges (front-face top/right edges and the
    /// diagonal to the back-top-right corner), so the box reads as 3D. The front face is the rectangle
    /// from (0, depth) to (width − depth, height).
    /// </summary>
    public static Geometry Deployment(double width, double height)
    {
        width = Math.Max(0d, width);
        height = Math.Max(0d, height);
        double d = DeploymentDepth(width, height);

        StreamGeometry geometry = new();
        using (StreamGeometryContext ctx = geometry.Open())
        {
            // Outer silhouette (filled).
            ctx.BeginFigure(new Point(0, d), isFilled: true);
            ctx.LineTo(new Point(d, 0));
            ctx.LineTo(new Point(width, 0));
            ctx.LineTo(new Point(width, height - d));
            ctx.LineTo(new Point(width - d, height));
            ctx.LineTo(new Point(0, height));
            ctx.EndFigure(true);

            // Front face edges (stroke only).
            ctx.BeginFigure(new Point(0, d), isFilled: false);
            ctx.LineTo(new Point(width - d, d));
            ctx.LineTo(new Point(width - d, height));
            ctx.EndFigure(false);

            // Diagonal from the front top-right corner to the back top-right corner.
            ctx.BeginFigure(new Point(width - d, d), isFilled: false);
            ctx.LineTo(new Point(width, 0));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    /// <summary>The deployment cuboid as an SVG path pair (mirrors <see cref="Deployment"/>) for export.</summary>
    public static SvgShape DeploymentSvg(double width, double height)
    {
        width = Math.Max(0d, width);
        height = Math.Max(0d, height);
        double d = DeploymentDepth(width, height);

        string body =
            $"M0,{Num(d)} L{Num(d)},0 L{Num(width)},0 L{Num(width)},{Num(height - d)} " +
            $"L{Num(width - d)},{Num(height)} L0,{Num(height)} Z";
        string edges =
            $"M0,{Num(d)} L{Num(width - d)},{Num(d)} L{Num(width - d)},{Num(height)} " +
            $"M{Num(width - d)},{Num(d)} L{Num(width)},0";
        return new SvgShape(body, edges);
    }
}
