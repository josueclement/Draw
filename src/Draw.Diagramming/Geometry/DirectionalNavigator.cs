using System.Collections.Generic;
using Draw.Model.Primitives;

namespace Draw.Diagramming.Geometry;

/// <summary>The four vim-style move directions (h/j/k/l) for keyboard selection navigation.</summary>
public enum MoveDirection
{
    Left,
    Right,
    Up,
    Down,
}

/// <summary>
/// Framework-agnostic "nearest shape in a direction" used by the vim h/j/k/l selection moves. Given a
/// reference point and a set of candidate bounds, it picks the candidate whose centre lies in the pressed
/// direction and is closest, favouring shapes that are roughly aligned on the cross axis: the score is the
/// distance along the move axis plus a weighted penalty for the perpendicular offset, so a neighbour in the
/// same row/column beats one that is nearer in a straight line but far off-axis.
/// </summary>
public static class DirectionalNavigator
{
    // Cross-axis offset costs this many times its distance. >1 keeps the search feeling directional
    // (an aligned neighbour wins) without a hard band that would make off-axis shapes unreachable.
    private const double PerpendicularWeight = 2d;

    /// <summary>
    /// Index into <paramref name="candidates"/> of the best shape to move to from <paramref name="reference"/>
    /// in <paramref name="direction"/>, or <c>null</c> when no candidate lies in that direction. A candidate
    /// qualifies only when its centre is strictly past the reference along the move axis (so the current shape
    /// and anything behind it are excluded).
    /// </summary>
    public static int? FindNearest(Point2D reference, IReadOnlyList<Rect2D> candidates, MoveDirection direction)
    {
        int? best = null;
        double bestScore = double.MaxValue;

        for (int i = 0; i < candidates.Count; i++)
        {
            Point2D centre = candidates[i].Center;
            (double along, double perpendicular) = direction switch
            {
                MoveDirection.Left => (reference.X - centre.X, System.Math.Abs(centre.Y - reference.Y)),
                MoveDirection.Right => (centre.X - reference.X, System.Math.Abs(centre.Y - reference.Y)),
                MoveDirection.Up => (reference.Y - centre.Y, System.Math.Abs(centre.X - reference.X)),
                MoveDirection.Down => (centre.Y - reference.Y, System.Math.Abs(centre.X - reference.X)),
                _ => (double.NegativeInfinity, 0d),
            };

            if (along <= 0)
            {
                continue; // Not strictly in the pressed direction.
            }

            double score = along + (PerpendicularWeight * perpendicular);
            if (score < bestScore)
            {
                bestScore = score;
                best = i;
            }
        }

        return best;
    }
}
