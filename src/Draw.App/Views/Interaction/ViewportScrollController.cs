using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Draw.App.ViewModels;
using Draw.Diagramming.Geometry;
using Draw.Model.Primitives;

namespace Draw.App.Views.Interaction;

/// <summary>
/// Keeps the two canvas scrollbars in sync with the document's pan/zoom and content bounds, and maps
/// scrollbar drags back into pan. All geometry lives in <see cref="ViewportScrollMath"/>; this class
/// owns the bar controls, the feedback guard and the world-space origin used to invert a bar value.
/// </summary>
internal sealed class ViewportScrollController
{
    // World-space padding around the content, so you can scroll a little past the outermost shapes.
    private const double ScrollContentMargin = 50d;

    private readonly ScrollBar _horizontal;
    private readonly ScrollBar _vertical;
    private readonly Control _fitCorner;

    // Guards against feedback while we push values into the bars, plus the world-space origin
    // (top-left of the scrollable region) used to map a bar value back to a pan offset.
    private bool _updating;
    private double _originX;
    private double _originY;

    public ViewportScrollController(ScrollBar horizontal, ScrollBar vertical, Control fitCorner)
    {
        _horizontal = horizontal;
        _vertical = vertical;
        _fitCorner = fitCorner;
    }

    /// <summary>
    /// Recomputes both bars from the current pan/zoom and content. Each bar is hidden (its gutter
    /// collapsing) when content fits that axis; the fit-corner button shows only when both bars do.
    /// </summary>
    public void Sync(DiagramDocumentViewModel vm, double viewWidth, double viewHeight)
    {
        if (vm.GetContentBounds() is not { } content)
        {
            _horizontal.IsVisible = false;
            _vertical.IsVisible = false;
            _fitCorner.IsVisible = false;
            return;
        }

        if (viewWidth <= 0 || viewHeight <= 0)
        {
            return;
        }

        double zoom = vm.Zoom <= 0 ? 1d : vm.Zoom;
        Rect2D visible = new(-vm.PanX / zoom, -vm.PanY / zoom, viewWidth / zoom, viewHeight / zoom);
        ScrollBarLayout layout = ViewportScrollMath.Compute(content, visible, ScrollContentMargin);

        _originX = layout.Region.Left;
        _originY = layout.Region.Top;

        _updating = true;
        try
        {
            if (layout.Horizontal.Needed)
            {
                Apply(_horizontal, layout.Horizontal);
            }

            if (layout.Vertical.Needed)
            {
                Apply(_vertical, layout.Vertical);
            }
        }
        finally
        {
            _updating = false;
        }

        _horizontal.IsVisible = layout.Horizontal.Needed;
        _vertical.IsVisible = layout.Vertical.Needed;
        _fitCorner.IsVisible = layout.Horizontal.Needed && layout.Vertical.Needed;
    }

    public void OnHorizontalScroll(DiagramDocumentViewModel vm, double value)
    {
        if (_updating)
        {
            return;
        }

        double zoom = vm.Zoom <= 0 ? 1d : vm.Zoom;
        vm.PanX = ViewportScrollMath.PanForScrollValue(_originX, value, zoom);
    }

    public void OnVerticalScroll(DiagramDocumentViewModel vm, double value)
    {
        if (_updating)
        {
            return;
        }

        double zoom = vm.Zoom <= 0 ? 1d : vm.Zoom;
        vm.PanY = ViewportScrollMath.PanForScrollValue(_originY, value, zoom);
    }

    private static void Apply(ScrollBar bar, ScrollAxis axis)
    {
        bar.Minimum = axis.Minimum;
        bar.Maximum = axis.Maximum;
        bar.ViewportSize = axis.ViewportSize;
        bar.Value = axis.Value;
    }
}
