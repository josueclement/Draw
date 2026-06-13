namespace Draw.Diagramming.Geometry;

/// <summary>
/// Shared numeric tolerances for diagram geometry. Use these instead of <see cref="double.Epsilon"/>
/// (≈5e-324, which only matches <em>exact</em> zero) for "is this effectively zero / equal?" tests.
/// </summary>
public static class GeometryTolerance
{
    /// <summary>Distance below which two points are treated as coincident (world units).</summary>
    public const double Distance = 1e-9;

    /// <summary>Squared-length guard for degenerate segments/vectors (compared against |v|²).
    /// Kept as <see cref="Distance"/>² so it stays dimensionally consistent with a linear tolerance.</summary>
    public const double LengthSquared = Distance * Distance;
}
