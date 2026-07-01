using System;
using System.Collections.Generic;
using Draw.Model.Styling;

namespace Draw.Diagramming.MindMap;

/// <summary>
/// Maps a <see cref="DashStyle"/> to the on/off run lengths (world units) used to chop a mind-map
/// branch ribbon into dashes via <see cref="TaperedStroke.BuildDashedOutlines"/>. UI-agnostic and
/// shared by the on-canvas renderer and the SVG export so they agree.
/// <para>
/// The on/off ratios mirror those an ordinary stroked connector uses (App
/// <c>StyleMappingExtensions.ToDashArray</c>: Dash 4:2, Dot 1:2, DashDot 4:2:1:2). They are scaled by
/// a width unit — the ribbon's thick end — rather than a fixed length, so the pattern reads in
/// proportion to the ribbon's thickness (mirroring how Avalonia scales a stroked line's dashes by its
/// own thickness). The ratios are deliberately duplicated here rather than shared with the App-layer
/// converter, which returns Avalonia types and feeds only ordinary connectors.
/// </para>
/// </summary>
public static class BranchDashPattern
{
    /// <summary>Never scale dashes off a hair-thin twig; keep them readable at deep branch levels.</summary>
    public const double MinUnit = 3d;

    /// <summary>
    /// The on/off dash run lengths (world units) for <paramref name="dash"/>, scaled by
    /// <paramref name="widthUnit"/> (the ribbon's thick-end width, floored at <see cref="MinUnit"/>).
    /// Returns an empty list for <see cref="DashStyle.Solid"/> or a non-positive width, meaning
    /// "draw one continuous ribbon".
    /// </summary>
    public static IReadOnlyList<double> For(DashStyle dash, double widthUnit)
    {
        double[] ratios = Ratios(dash);
        if (ratios.Length == 0 || widthUnit <= 0d)
        {
            return Array.Empty<double>();
        }

        double unit = Math.Max(widthUnit, MinUnit);
        double[] lengths = new double[ratios.Length];
        for (int i = 0; i < ratios.Length; i++)
        {
            lengths[i] = ratios[i] * unit;
        }

        return lengths;
    }

    private static double[] Ratios(DashStyle dash) => dash switch
    {
        DashStyle.Dash => new double[] { 4d, 2d },
        DashStyle.Dot => new double[] { 1d, 2d },
        DashStyle.DashDot => new double[] { 4d, 2d, 1d, 2d },
        _ => Array.Empty<double>(),
    };
}
