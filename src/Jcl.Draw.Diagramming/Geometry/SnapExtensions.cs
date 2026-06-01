using System;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.Diagramming.Geometry;

/// <summary>Grid-snapping helpers for canvas coordinates (C# 14 extension blocks).</summary>
public static class SnapExtensions
{
    /// <summary>Rounds a single coordinate to the nearest multiple of <paramref name="gridSize"/>.</summary>
    public static double SnapValue(double value, double gridSize)
        => gridSize <= 0 ? value : Math.Round(value / gridSize, MidpointRounding.AwayFromZero) * gridSize;

    extension(Point2D point)
    {
        public Point2D SnappedToGrid(double gridSize)
            => new(SnapValue(point.X, gridSize), SnapValue(point.Y, gridSize));
    }

    extension(Rect2D rect)
    {
        /// <summary>Snaps the rectangle's top-left position to the grid, preserving its size.</summary>
        public Rect2D PositionSnappedToGrid(double gridSize)
            => rect.WithPosition(rect.Position.SnappedToGrid(gridSize));

        /// <summary>Snaps the rectangle's edges to the grid (used while resizing).</summary>
        public Rect2D EdgesSnappedToGrid(double gridSize)
            => Rect2D.FromLtrb(
                SnapValue(rect.Left, gridSize),
                SnapValue(rect.Top, gridSize),
                SnapValue(rect.Right, gridSize),
                SnapValue(rect.Bottom, gridSize));
    }
}
