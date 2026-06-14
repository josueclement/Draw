using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>
/// The viewport state <see cref="ViewportCoordinator"/> reads and writes: the bound zoom/pan that the
/// view binds to, the current viewport size, the zoom bounds, and the content extent. Implemented by the
/// document view model, which keeps these as observable properties so the coordinator can mutate them.
/// </summary>
public interface IViewportHost
{
    double Zoom { get; set; }

    double PanX { get; set; }

    double PanY { get; set; }

    double ViewportWidth { get; }

    double ViewportHeight { get; }

    double MinZoom { get; }

    double MaxZoom { get; }

    /// <summary>World-coordinate bounding box of all content, or <c>null</c> when the diagram is empty.</summary>
    Rect2D? GetContentBounds();
}
