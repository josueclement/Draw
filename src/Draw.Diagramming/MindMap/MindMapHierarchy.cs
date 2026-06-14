using System;
using System.Collections.Generic;
using Draw.Model.Connectors;

namespace Draw.Diagramming.MindMap;

/// <summary>
/// Pure graph helper for mind-map branch connectors. Computes each node's depth from its root (the
/// central topic) by walking branch connectors source→target, so branch widths can scale with depth.
/// UI-agnostic and side-effect free.
/// </summary>
public static class MindMapHierarchy
{
    /// <summary>
    /// Maps every node touched by a mind-map branch to its depth from the root (0 = a root / central
    /// topic). Roots are branch sources that are never a branch target; depth is the shortest branch
    /// distance from any root (BFS). Robust to cycles and missing roots: if no node is un-parented
    /// every touched node is seeded at depth 0, so the result is always defined and the walk
    /// terminates. Connectors that are not mind-map branches are ignored.
    /// </summary>
    public static IReadOnlyDictionary<Guid, int> ComputeDepths(IReadOnlyList<Connector> connectors)
    {
        Dictionary<Guid, int> depths = new();
        if (connectors is null || connectors.Count == 0)
        {
            return depths;
        }

        Dictionary<Guid, List<Guid>> children = new();
        HashSet<Guid> hasParent = new();
        HashSet<Guid> touched = new();
        foreach (Connector connector in connectors)
        {
            if (!connector.IsMindMapBranch)
            {
                continue;
            }

            touched.Add(connector.SourceNodeId);
            touched.Add(connector.TargetNodeId);
            hasParent.Add(connector.TargetNodeId);
            if (!children.TryGetValue(connector.SourceNodeId, out List<Guid>? kids))
            {
                kids = new List<Guid>();
                children[connector.SourceNodeId] = kids;
            }

            kids.Add(connector.TargetNodeId);
        }

        if (touched.Count == 0)
        {
            return depths;
        }

        Queue<Guid> queue = new();
        foreach (Guid node in touched)
        {
            if (!hasParent.Contains(node))
            {
                depths[node] = 0;
                queue.Enqueue(node);
            }
        }

        // A pure cycle leaves nothing un-parented — seed every node at depth 0 so the BFS still runs.
        if (queue.Count == 0)
        {
            foreach (Guid node in touched)
            {
                depths[node] = 0;
                queue.Enqueue(node);
            }
        }

        while (queue.Count > 0)
        {
            Guid node = queue.Dequeue();
            int depth = depths[node];
            if (!children.TryGetValue(node, out List<Guid>? kids))
            {
                continue;
            }

            foreach (Guid kid in kids)
            {
                // Keep the shallowest depth; a node is re-enqueued only when its depth improves, and
                // depth is bounded below by 0, so this terminates even with cycles.
                if (!depths.TryGetValue(kid, out int existing) || depth + 1 < existing)
                {
                    depths[kid] = depth + 1;
                    queue.Enqueue(kid);
                }
            }
        }

        return depths;
    }

    /// <summary>The depth recorded for <paramref name="nodeId"/>, or 0 when it is not part of any branch.</summary>
    public static int DepthOf(IReadOnlyDictionary<Guid, int> depths, Guid nodeId)
        => depths.TryGetValue(nodeId, out int depth) ? depth : 0;
}
