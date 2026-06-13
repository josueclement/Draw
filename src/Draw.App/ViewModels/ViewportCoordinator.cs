using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.App.ViewModels;

/// <summary>
/// Owns the zoom/pan/fit operations over an <see cref="IViewportHost"/>; the pure arithmetic lives in
/// <see cref="ViewportMath"/>. The document view model keeps the bound zoom/pan properties and the zoom
/// commands (which forward here), mirroring how <see cref="SelectionCoordinator"/> owns the selection
/// mutations. Extracted from the god view model so the viewport behaviour lives in one place.
/// </summary>
public sealed class ViewportCoordinator
{
    // Ribbon View-tab zoom step. Zoom bounds live in EditorOptions (surfaced via the host's Min/MaxZoom).
    private const double ZoomStep = 1.2d;

    // Padding (world units) kept around content when fitting it to the viewport.
    private const double FitMargin = 40d;

    private readonly IViewportHost _host;

    public ViewportCoordinator(IViewportHost host) => _host = host;

    public void ZoomIn() => _host.Zoom = System.Math.Clamp(_host.Zoom * ZoomStep, _host.MinZoom, _host.MaxZoom);

    public void ZoomOut() => _host.Zoom = System.Math.Clamp(_host.Zoom / ZoomStep, _host.MinZoom, _host.MaxZoom);

    public void ZoomReset()
    {
        _host.Zoom = 1d;
        _host.PanX = 0d;
        _host.PanY = 0d;
    }

    /// <summary>Zooms/pans so all content fits centred in the viewport, never enlarging past 100%.</summary>
    public void FitToContent()
    {
        if (_host.GetContentBounds() is not { } content || _host.ViewportWidth <= 0 || _host.ViewportHeight <= 0)
        {
            return;
        }

        ViewportFit fit = ViewportMath.FitToContent(
            content, _host.ViewportWidth, _host.ViewportHeight, _host.MinZoom, _host.MaxZoom, FitMargin);
        _host.Zoom = fit.Zoom;
        _host.PanX = fit.PanX;
        _host.PanY = fit.PanY;
    }

    public Point2D ViewportCenterWorld()
        => ViewportMath.CenterToWorld(_host.ViewportWidth, _host.ViewportHeight, _host.PanX, _host.PanY, _host.Zoom);
}
