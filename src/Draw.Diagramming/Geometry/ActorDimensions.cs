using System;

namespace Draw.Diagramming.Geometry;

/// <summary>
/// Framework-agnostic proportions of an actor stick-figure scaled to its bounds, reserving a
/// bottom strip for the name label. The single source of truth for actor geometry, shared by the
/// Avalonia geometry builder and the SVG exporter so the two render paths cannot drift apart.
/// </summary>
public readonly struct ActorDimensions
{
    public ActorDimensions(double width, double height)
    {
        width = Math.Max(1d, width);
        height = Math.Max(1d, height);

        double labelStrip = Math.Min(18d, height * 0.25d);
        FigureHeight = Math.Max(1d, height - labelStrip);
        CenterX = width / 2d;
        HeadRadius = Math.Min(width, FigureHeight) * 0.18d;
        NeckY = HeadRadius * 2d;
        HipY = FigureHeight * 0.62d;
        ShoulderY = NeckY + ((HipY - NeckY) * 0.25d);
        ArmHalf = width * 0.30d;
        LegSpread = width * 0.28d;
    }

    /// <summary>Drawable figure height, i.e. the bounds height minus the reserved label strip.</summary>
    public double FigureHeight { get; }

    /// <summary>Horizontal centerline of the figure.</summary>
    public double CenterX { get; }

    /// <summary>Radius of the head circle, whose top touches y = 0.</summary>
    public double HeadRadius { get; }

    /// <summary>Top of the torso line (just below the head).</summary>
    public double NeckY { get; }

    /// <summary>Bottom of the torso line, where the legs fork.</summary>
    public double HipY { get; }

    /// <summary>Vertical position of the horizontal arm line.</summary>
    public double ShoulderY { get; }

    /// <summary>Half-width of the arm line, measured from the centerline.</summary>
    public double ArmHalf { get; }

    /// <summary>Horizontal spread of each foot from the centerline at the figure base.</summary>
    public double LegSpread { get; }
}
