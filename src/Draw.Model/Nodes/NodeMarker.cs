namespace Draw.Model.Nodes;

/// <summary>
/// A small icon that can be attached to any node and rendered as a badge. The first group are status
/// markers (task states / flags); the rest are general-purpose icons. Multiple, independent icons may
/// be set on one node (a true stack); <see cref="Draw.App"/>'s visual table defines the display order.
/// Purely visual metadata — icons carry no behaviour. Values are stable (serialized by name); append
/// new icons, never renumber existing ones.
/// </summary>
public enum NodeMarker
{
    Todo = 0,
    InProgress = 1,
    Done = 2,
    Stuck = 3,
    Important = 4,
    Idea = 5,
    Question = 6,

    // General-purpose icons (appended; existing values above must keep their numbers).
    Warning = 7,
    Chat = 8,
    Happy = 9,
    Angry = 10,
    Phone = 11,
    Mail = 12,
    Car = 13,
    Island = 14,
    PalmTree = 15,
    Flag = 16,
    Heart = 17,
    Coffee = 18,
    Calendar = 19,
    MapPin = 20,
}
