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
    /// <summary>Display order for badges and the inspector toggle row (status markers first, then general icons).</summary>
    public static IReadOnlyList<NodeMarker> Order { get; } = new[]
    {
        NodeMarker.Todo,
        NodeMarker.InProgress,
        NodeMarker.Done,
        NodeMarker.Stuck,
        NodeMarker.Important,
        NodeMarker.Idea,
        NodeMarker.Question,
        NodeMarker.Warning,
        NodeMarker.Chat,
        NodeMarker.Happy,
        NodeMarker.Angry,
        NodeMarker.Phone,
        NodeMarker.Mail,
        NodeMarker.Car,
        NodeMarker.Island,
        NodeMarker.PalmTree,
        NodeMarker.Flag,
        NodeMarker.Heart,
        NodeMarker.Coffee,
        NodeMarker.Calendar,
        NodeMarker.MapPin,
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

        [NodeMarker.Warning] = Make(NodeMarker.Warning, Icon.warning_octagon, 0xFFE8A317, "Warning"),
        [NodeMarker.Chat] = Make(NodeMarker.Chat, Icon.chat_circle, 0xFF2D7DD2, "Chat"),
        [NodeMarker.Happy] = Make(NodeMarker.Happy, Icon.smiley_wink, 0xFF3FA34D, "Happy"),
        [NodeMarker.Angry] = Make(NodeMarker.Angry, Icon.smiley_angry, 0xFFD64545, "Angry"),
        [NodeMarker.Phone] = Make(NodeMarker.Phone, Icon.phone_call, 0xFF3FA34D, "Phone"),
        [NodeMarker.Mail] = Make(NodeMarker.Mail, Icon.envelope_simple, 0xFF2D7DD2, "Mail"),
        [NodeMarker.Car] = Make(NodeMarker.Car, Icon.car_simple, 0xFF5B6470, "Car"),
        [NodeMarker.Island] = Make(NodeMarker.Island, Icon.island, 0xFF14A0A0, "Island"),
        [NodeMarker.PalmTree] = Make(NodeMarker.PalmTree, Icon.tree_palm, 0xFF2E9E5B, "Palm tree"),
        [NodeMarker.Flag] = Make(NodeMarker.Flag, Icon.flag, 0xFFD64545, "Flag"),
        [NodeMarker.Heart] = Make(NodeMarker.Heart, Icon.heart_straight, 0xFFE5447A, "Heart"),
        [NodeMarker.Coffee] = Make(NodeMarker.Coffee, Icon.coffee, 0xFF8B5E3C, "Coffee"),
        [NodeMarker.Calendar] = Make(NodeMarker.Calendar, Icon.calendar, 0xFF2D7DD2, "Calendar"),
        [NodeMarker.MapPin] = Make(NodeMarker.MapPin, Icon.map_pin, 0xFFD64545, "Location"),
    };

    private static NodeMarkerVisual Make(NodeMarker marker, Icon icon, uint argb, string label)
    {
        Geometry geometry = IconService.CreateGeometry(icon, IconType.fill);
        SolidColorBrush brush = new(Color.FromUInt32(argb));
        return new NodeMarkerVisual(marker, geometry, brush, label);
    }
}
