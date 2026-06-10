using Avalonia;
using Avalonia.Controls.Shapes;
using Draw.App.ViewModels;

namespace Draw.App.Views.Interaction;

/// <summary>The kind of pointer drag currently in progress on the canvas. <c>None</c> = idle.</summary>
internal enum DragMode
{
    None,
    Move,
    Marquee,
    Pan,
    Resize,
    Connect,
    EndpointMove,
    WaypointMove,
    LabelMove,
}

/// <summary>
/// Mutable state for a single in-progress canvas pointer gesture. One instance lives on
/// <c>DiagramView</c> for its lifetime; <see cref="Reset"/> returns it to idle at the end of every
/// gesture. Connector-edit sub-state, arrow-nudge state and scrollbar state are tracked separately
/// because they have different lifecycles.
/// </summary>
internal sealed class CanvasGestureState
{
    public DragMode Mode { get; set; } = DragMode.None;

    // True once the current gesture has captured its undo snapshot (one snapshot per completed gesture).
    public bool UndoCaptured { get; set; }

    // Move: dead-zone tracking so a select-click doesn't nudge the shape, plus the last applied world point.
    public bool MoveThresholdPassed { get; set; }
    public Point MoveStartScreen { get; set; }
    public Point MoveLastWorld { get; set; }

    // Pan: last screen point (incremental delta) + the initial press (right-click-vs-drag arrange threshold).
    public Point LastScreen { get; set; }
    public Point PanStartScreen { get; set; }

    // Marquee: world anchor + screen anchor (screen drives the bare-click reference-dismiss threshold).
    public Point MarqueeStartWorld { get; set; }
    public Point MarqueeStartScreen { get; set; }
    public bool MarqueeAdditive { get; set; }
    public Rectangle? Marquee { get; set; }

    // Resize: which of the 8 handles is dragged, and the node being resized.
    public int ResizeHandle { get; set; } = -1;
    public NodeViewModelBase? ResizeTarget { get; set; }

    // Connect: the source node a new connector is dragged from, plus its rubber-band preview line.
    public NodeViewModelBase? ConnectSource { get; set; }
    public Line? ConnectPreview { get; set; }

    /// <summary>
    /// Returns to idle at the end of a gesture. The transient overlay visuals (<see cref="Marquee"/> /
    /// <see cref="ConnectPreview"/> controls) are removed from the canvas by the view's End* methods
    /// before this nulls the references.
    /// </summary>
    public void Reset()
    {
        Mode = DragMode.None;
        UndoCaptured = false;
        MoveThresholdPassed = false;
        ResizeHandle = -1;
        ResizeTarget = null;
        ConnectSource = null;
        Marquee = null;
        ConnectPreview = null;
    }
}
