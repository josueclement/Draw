using System.Collections.Generic;
using Jcl.Draw.Model.Primitives;

namespace Jcl.Draw.Diagramming.Routing;

internal static class RouteHelpers
{
    private const double Epsilon = 1e-6;

    /// <summary>Removes consecutive duplicate points; guarantees at least two points remain.</summary>
    public static List<Point2D> Dedupe(IReadOnlyList<Point2D> points)
    {
        List<Point2D> result = new();
        foreach (Point2D p in points)
        {
            if (result.Count == 0 || result[result.Count - 1].DistanceTo(p) > Epsilon)
            {
                result.Add(p);
            }
        }

        if (result.Count < 2)
        {
            result.Clear();
            result.Add(points[0]);
            result.Add(points[points.Count - 1]);
        }

        return result;
    }
}
