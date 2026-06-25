using System;
using System.Collections.Generic;
using Avalonia.Media;
using Draw.Model.Nodes;
using PhosphorIconsAvalonia;

namespace Draw.App.Rendering;

/// <summary>The icon, colour and label a <see cref="NodeMarker"/> renders with, in a badge or a toggle.</summary>
public sealed record NodeMarkerVisual(NodeMarker Marker, Geometry Icon, IBrush Brush, string Label);

/// <summary>
/// Maps each <see cref="NodeMarker"/> to a filled Phosphor glyph + colour + label. Geometries and
/// brushes are built once and shared (geometries are immutable-by-use, safe to bind to many controls).
/// <see cref="Order"/> is the stable badge/toggle display order.
/// </summary>
public static class NodeMarkerVisuals
{
    /// <summary>Display order for badges and the inspector toggle row (task states first, then flags).</summary>
    public static IReadOnlyList<NodeMarker> Order { get; } = new[]
    {
        NodeMarker.Todo,
        NodeMarker.InProgress,
        NodeMarker.Done,
        NodeMarker.Stuck,
        NodeMarker.Important,
        NodeMarker.Idea,
        NodeMarker.Question,
    };

    private static readonly IReadOnlyDictionary<NodeMarker, NodeMarkerVisual> ByMarker = Build();

    public static NodeMarkerVisual For(NodeMarker marker) => ByMarker[marker];

    private static IReadOnlyDictionary<NodeMarker, NodeMarkerVisual> Build() => new Dictionary<NodeMarker, NodeMarkerVisual>
    {
        [NodeMarker.Todo] = Make(NodeMarker.Todo, Icon.clock, 0xFFE0A100, "To-do"),
        [NodeMarker.InProgress] = Make(NodeMarker.InProgress, Icon.circle_half, 0xFF2D7DD2, "In progress"),
        [NodeMarker.Done] = Make(NodeMarker.Done, Icon.check_circle, 0xFF3FA34D, "Done"),
        [NodeMarker.Stuck] = Make(NodeMarker.Stuck, Icon.warning_circle, 0xFFD64545, "Stuck"),
        [NodeMarker.Important] = Make(NodeMarker.Important, Icon.star_four, 0xFFE8533F, "Important"),
        [NodeMarker.Idea] = Make(NodeMarker.Idea, Icon.lightbulb, 0xFFF2B807, "Idea"),
        [NodeMarker.Question] = Make(NodeMarker.Question, Icon.seal_question, 0xFF7B5EA7, "Question"),
    };

    private static NodeMarkerVisual Make(NodeMarker marker, Icon icon, uint argb, string label)
    {
        Geometry geometry = IconService.CreateGeometry(icon, IconType.fill);
        SolidColorBrush brush = new(Color.FromUInt32(argb));
        return new NodeMarkerVisual(marker, geometry, brush, label);
    }
}
