using System;

namespace Draw.Diagramming.MindMap;

/// <summary>
/// The depth-scaled stroke widths of mind-map branches: thick near the central topic, tapering
/// thinner toward the leaves. The single source of branch widths, shared by the on-canvas renderer
/// and the SVG export so they agree.
/// </summary>
public static class MindMapBranchStyle
{
    /// <summary>Full stroke width (world units) of a branch leaving a depth-0 (central) topic.</summary>
    public const double BaseWidth = 9d;

    /// <summary>Each level deeper multiplies the width by this factor (a geometric taper by depth).</summary>
    public const double Falloff = 0.68d;

    /// <summary>Branches never render thinner than this, so deep leaves stay visible.</summary>
    public const double MinWidth = 2d;

    /// <summary>
    /// The full stroke width of a branch end attached to a topic at <paramref name="depth"/> (the
    /// central topic is depth 0). A branch interpolates from <c>WidthAt(parentDepth)</c> at its source
    /// to <c>WidthAt(parentDepth + 1)</c> at its child, giving the continuous trunk→twig taper.
    /// </summary>
    public static double WidthAt(int depth)
    {
        if (depth < 0)
        {
            depth = 0;
        }

        double width = BaseWidth * Math.Pow(Falloff, depth);
        return Math.Max(MinWidth, width);
    }
}
