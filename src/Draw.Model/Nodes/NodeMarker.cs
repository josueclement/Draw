namespace Draw.Model.Nodes;

/// <summary>
/// A small status marker that can be attached to any node and rendered as an icon badge. Multiple,
/// independent markers may be set on one node (a true stack); the enum order is the display order.
/// Purely visual metadata — markers carry no behaviour.
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
}
