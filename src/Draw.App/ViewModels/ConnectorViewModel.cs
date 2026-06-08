using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Draw.App.Rendering;
using Draw.App.Services;
using Draw.Diagramming.Geometry;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Styling;
using Draw.Model.Connectors;
using ModelPoint = Draw.Model.Primitives.Point2D;
using ModelStyle = Draw.Model.Styling;

namespace Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="Connector"/>: routed geometry, UML decorations and labels.</summary>
public sealed class ConnectorViewModel : ViewModelBase
{
    private const double LabelOffset = 12d;

    private readonly Connector _model;
    private readonly IConnectorRouter _router;
    private readonly IThemeService _theme;
    private ConnectorRoute _route;

    public ConnectorViewModel(Connector model, NodeViewModelBase source, NodeViewModelBase target, IConnectorRouter router, IThemeService theme)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _router = router ?? throw new ArgumentNullException(nameof(router));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));

        Source.PropertyChanged += OnEndpointChanged;
        Target.PropertyChanged += OnEndpointChanged;
        _route = Compute();
    }

    public Connector Model => _model;

    public Guid Id => _model.Id;

    public NodeViewModelBase Source { get; }

    public NodeViewModelBase Target { get; }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public Geometry Geometry => BuildLineGeometry();

    /// <summary>The active-theme variant of the quick-palette swatch this connector is linked to, or
    /// null when it carries no <c>PaletteId</c>.</summary>
    private SwatchVariant? Swatch
        => StylePalette.TryGet(_model.Style.PaletteId, out StyleSwatch swatch) ? swatch.Variant(_theme.IsDark) : null;

    public IBrush Stroke => Swatch is { } s ? s.Stroke.ToBrush() : _model.Style.Stroke.Color.ToBrush();

    /// <summary>Brush for the connector's labels: the swatch's text colour, else the theme-aware
    /// default (matching node text), else the customised font colour.</summary>
    public IBrush LabelForeground => Swatch is { } s ? s.Text.ToBrush()
        : _model.Style.Font.Color == ModelStyle.FontSpec.DefaultColor && _theme.DefaultNodeText is { } text ? text
        : _model.Style.Font.Color.ToBrush();

    public double StrokeThickness => _model.Style.Stroke.Thickness;

    public AvaloniaList<double>? StrokeDashArray
        => ConnectorDecorationBuilder.Describe(_model.Kind).Dashed
            ? new AvaloniaList<double> { 6, 3 }
            : _model.Style.Stroke.Dash.ToDashArray();

    // Per-end decoration: a set crow's-foot cardinality (ER) wins; otherwise the relationship kind's cap.
    private ConnectorEndDecoration SourceEnd => _model.SourceCardinality != Cardinality.Unspecified
        ? ConnectorDecorationBuilder.FromCardinality(_model.SourceCardinality)
        : ConnectorDecorationBuilder.Describe(_model.Kind).Source;

    private ConnectorEndDecoration TargetEnd => _model.TargetCardinality != Cardinality.Unspecified
        ? ConnectorDecorationBuilder.FromCardinality(_model.TargetCardinality)
        : ConnectorDecorationBuilder.Describe(_model.Kind).Target;

    public Geometry? SourceDecoration => ConnectorDecorationBuilder.Build(SourceEnd, _route.Start, _route.StartDirection * -1d);

    public IBrush? SourceDecorationFill => DecorationFill(SourceEnd);

    public Geometry? TargetDecoration => ConnectorDecorationBuilder.Build(TargetEnd, _route.End, _route.EndDirection);

    public IBrush? TargetDecorationFill => DecorationFill(TargetEnd);

    public string CenterLabelText => _model.CenterLabel ?? DefaultStereotype();

    public bool HasCenterLabel => !string.IsNullOrEmpty(CenterLabelText);

    public double CenterLabelX => LabelDisplay(ConnectorLabelKind.Center).X;

    public double CenterLabelY => LabelDisplay(ConnectorLabelKind.Center).Y;

    public string? SourceLabelText => _model.SourceLabel;

    public bool HasSourceLabel => !string.IsNullOrEmpty(_model.SourceLabel);

    public double SourceLabelX => LabelDisplay(ConnectorLabelKind.Source).X;

    public double SourceLabelY => LabelDisplay(ConnectorLabelKind.Source).Y;

    public string? TargetLabelText => _model.TargetLabel;

    public bool HasTargetLabel => !string.IsNullOrEmpty(_model.TargetLabel);

    public double TargetLabelX => LabelDisplay(ConnectorLabelKind.Target).X;

    public double TargetLabelY => LabelDisplay(ConnectorLabelKind.Target).Y;

    /// <summary>The resolved source endpoint (where the connector touches the source shape).</summary>
    public ModelPoint RouteStart => _route.Start;

    /// <summary>The resolved target endpoint (where the connector touches the target shape).</summary>
    public ModelPoint RouteEnd => _route.End;

    /// <summary>Unit vector pointing along the line away from the source endpoint (export decorations).</summary>
    public ModelPoint RouteStartDirection => _route.StartDirection;

    /// <summary>Unit vector pointing along the line into the target endpoint (export decorations).</summary>
    public ModelPoint RouteEndDirection => _route.EndDirection;

    /// <summary>The resolved end decoration at the source endpoint (export).</summary>
    public ConnectorEndDecoration SourceEndDecoration => SourceEnd;

    /// <summary>The resolved end decoration at the target endpoint (export).</summary>
    public ConnectorEndDecoration TargetEndDecoration => TargetEnd;

    /// <summary>The user-controlled bend points, in world coordinates (drawn as draggable handles).</summary>
    public IReadOnlyList<ModelPoint> Waypoints => _model.BendPoints;

    /// <summary>True when the source end is pinned to a forced anchor (vs. automatic attachment).</summary>
    public bool SourceAnchored => _model.SourceAnchor is not null;

    /// <summary>True when the target end is pinned to a forced anchor (vs. automatic attachment).</summary>
    public bool TargetAnchored => _model.TargetAnchor is not null;

    /// <summary>The forced source anchor as a relative (u,v) in [0,1]², or null when the source end is automatic.</summary>
    public ModelPoint? SourceAnchor => _model.SourceAnchor;

    /// <summary>The forced target anchor as a relative (u,v) in [0,1]², or null when the target end is automatic.</summary>
    public ModelPoint? TargetAnchor => _model.TargetAnchor;

    /// <summary>True for route styles whose bend points are honoured (straight, orthogonal, rounded).</summary>
    public bool SupportsWaypoints => _model.Route is RouteStyle.Straight or RouteStyle.Orthogonal or RouteStyle.Rounded;

    /// <summary>Recomputes the route and re-raises all derived properties.</summary>
    public void Recompute()
    {
        _route = Compute();
        RaiseAll();
    }

    /// <summary>Unsubscribes from endpoint changes; call before discarding.</summary>
    public void Detach()
    {
        Source.PropertyChanged -= OnEndpointChanged;
        Target.PropertyChanged -= OnEndpointChanged;
    }

    /// <summary>Pins (or, with null, releases) the source attachment as a relative (u,v) anchor.</summary>
    public void SetSourceAnchor(ModelPoint? relative)
    {
        _model.SourceAnchor = relative;
        Recompute();
    }

    /// <summary>Pins (or, with null, releases) the target attachment as a relative (u,v) anchor.</summary>
    public void SetTargetAnchor(ModelPoint? relative)
    {
        _model.TargetAnchor = relative;
        Recompute();
    }

    /// <summary>Inserts a bend point at <paramref name="world"/> on the nearest segment; returns its index.</summary>
    public int InsertBendPointAt(ModelPoint world)
    {
        int index = NearestSegmentIndex(world);
        _model.BendPoints.Insert(index, world);
        Recompute();
        return index;
    }

    /// <summary>Moves the bend point at <paramref name="index"/> to <paramref name="world"/>.</summary>
    public void MoveBendPoint(int index, ModelPoint world)
    {
        if (index < 0 || index >= _model.BendPoints.Count)
        {
            return;
        }

        _model.BendPoints[index] = world;
        Recompute();
    }

    /// <summary>Removes the bend point at <paramref name="index"/>.</summary>
    public void RemoveBendPoint(int index)
    {
        if (index < 0 || index >= _model.BendPoints.Count)
        {
            return;
        }

        _model.BendPoints.RemoveAt(index);
        Recompute();
    }

    /// <summary>Snaps the bend point at <paramref name="index"/> to the grid.</summary>
    public void SnapBendPointToGrid(int index, double gridSize)
    {
        if (index < 0 || index >= _model.BendPoints.Count)
        {
            return;
        }

        _model.BendPoints[index] = _model.BendPoints[index].SnappedToGrid(gridSize);
        Recompute();
    }

    /// <summary>Sets (or, with null, clears) the world-unit offset of a label from its natural anchor.</summary>
    public void SetLabelOffset(ConnectorLabelKind kind, ModelPoint? offset)
    {
        switch (kind)
        {
            case ConnectorLabelKind.Source:
                _model.SourceLabelOffset = offset;
                break;
            case ConnectorLabelKind.Target:
                _model.TargetLabelOffset = offset;
                break;
            default:
                _model.CenterLabelOffset = offset;
                break;
        }

        RaiseLabelPositions();
    }

    /// <summary>Snaps a label's resolved (displayed) position to the grid, re-deriving its offset.</summary>
    public void SnapLabelToGrid(ConnectorLabelKind kind, double gridSize)
    {
        ModelPoint snapped = LabelDisplay(kind).SnappedToGrid(gridSize);
        SetLabelOffset(kind, snapped - NaturalLabelAnchor(kind));
    }

    /// <summary>The computed label anchor before any user offset (used to derive a drag offset).</summary>
    public ModelPoint NaturalLabelAnchor(ConnectorLabelKind kind) => kind switch
    {
        ConnectorLabelKind.Source => EndLabelPosition(_route.Start, _route.StartDirection),
        ConnectorLabelKind.Target => EndLabelPosition(_route.End, _route.EndDirection * -1d),
        _ => Midpoint(),
    };

    /// <summary>The displayed label position (natural anchor + stored offset).</summary>
    public ModelPoint LabelDisplay(ConnectorLabelKind kind)
        => NaturalLabelAnchor(kind) + (StoredLabelOffset(kind) ?? ModelPoint.Origin);

    private ModelPoint? StoredLabelOffset(ConnectorLabelKind kind) => kind switch
    {
        ConnectorLabelKind.Source => _model.SourceLabelOffset,
        ConnectorLabelKind.Target => _model.TargetLabelOffset,
        _ => _model.CenterLabelOffset,
    };

    private int NearestSegmentIndex(ModelPoint world)
    {
        List<ModelPoint> logical = new() { _route.Start };
        logical.AddRange(_model.BendPoints);
        logical.Add(_route.End);

        int best = 0;
        double bestDistance = double.PositiveInfinity;
        for (int i = 1; i < logical.Count; i++)
        {
            double distance = DistanceToSegment(world, logical[i - 1], logical[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i - 1;
            }
        }

        return best;
    }

    private static double DistanceToSegment(ModelPoint p, ModelPoint a, ModelPoint b)
    {
        ModelPoint ab = b - a;
        double lengthSquared = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (lengthSquared <= double.Epsilon)
        {
            return p.DistanceTo(a);
        }

        double t = (((p.X - a.X) * ab.X) + ((p.Y - a.Y) * ab.Y)) / lengthSquared;
        t = Math.Clamp(t, 0d, 1d);
        ModelPoint projection = new(a.X + (t * ab.X), a.Y + (t * ab.Y));
        return p.DistanceTo(projection);
    }

    private void RaiseLabelPositions()
    {
        OnPropertyChanged(nameof(CenterLabelX));
        OnPropertyChanged(nameof(CenterLabelY));
        OnPropertyChanged(nameof(SourceLabelX));
        OnPropertyChanged(nameof(SourceLabelY));
        OnPropertyChanged(nameof(TargetLabelX));
        OnPropertyChanged(nameof(TargetLabelY));
    }

    private ConnectorRoute Compute()
    {
        ConnectorRouteRequest request = new(
            Source.BoundaryKind,
            Source.Bounds,
            Target.BoundaryKind,
            Target.Bounds,
            _model.Route,
            _model.BendPoints,
            _model.SourceAnchor,
            _model.TargetAnchor);
        return _router.Route(request);
    }

    private void OnEndpointChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(NodeViewModelBase.X)
            or nameof(NodeViewModelBase.Y)
            or nameof(NodeViewModelBase.Width)
            or nameof(NodeViewModelBase.Height))
        {
            Recompute();
        }
    }

    private Geometry BuildLineGeometry()
    {
        if (_route.Cubics is { } cubics)
        {
            StreamGeometry geometry = new();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(ToPoint(_route.Start), isFilled: false);
                foreach (CubicSegment segment in cubics)
                {
                    ctx.CubicBezierTo(ToPoint(segment.Control1), ToPoint(segment.Control2), ToPoint(segment.End));
                }

                ctx.EndFigure(false);
            }

            return geometry;
        }

        return new PolylineGeometry(_route.Points.Select(ToPoint).ToList(), isFilled: false);
    }

    private IBrush? DecorationFill(ConnectorEndDecoration decoration)
    {
        if (ConnectorDecorationBuilder.IsStrokeOnly(decoration))
        {
            return null;
        }

        return ConnectorDecorationBuilder.IsFilled(decoration) ? Stroke : HollowFill();
    }

    // Hollow decorations occlude the line, so they fill with the diagram background (theme-aware).
    private static IBrush HollowFill()
    {
        if (Application.Current is { } app
            && app.TryGetResource("ThemeBackgroundBrush", app.ActualThemeVariant, out object? resource)
            && resource is IBrush brush)
        {
            return brush;
        }

        return Brushes.White;
    }

    /// <summary>The route as a flattened world-coordinate polyline (curve segments are sampled).</summary>
    public IReadOnlyList<ModelPoint> GetFlattenedPoints()
    {
        if (_route.Cubics is { } cubics)
        {
            const int perSegment = 16;
            List<ModelPoint> samples = new((cubics.Count * perSegment) + 1) { _route.Start };
            ModelPoint segmentStart = _route.Start;
            foreach (CubicSegment segment in cubics)
            {
                for (int i = 1; i <= perSegment; i++)
                {
                    samples.Add(CubicAt(segmentStart, segment.Control1, segment.Control2, segment.End, i / (double)perSegment));
                }

                segmentStart = segment.End;
            }

            return samples;
        }

        return _route.Points;
    }

    private static ModelPoint CubicAt(ModelPoint a, ModelPoint b, ModelPoint c, ModelPoint d, double t)
    {
        double u = 1d - t;
        double w0 = u * u * u;
        double w1 = 3 * u * u * t;
        double w2 = 3 * u * t * t;
        double w3 = t * t * t;
        return new ModelPoint(
            (w0 * a.X) + (w1 * b.X) + (w2 * c.X) + (w3 * d.X),
            (w0 * a.Y) + (w1 * b.Y) + (w2 * c.Y) + (w3 * d.Y));
    }

    private string DefaultStereotype() => _model.Kind switch
    {
        RelationshipKind.Include => "«include»",
        RelationshipKind.Extend => "«extend»",
        _ => string.Empty,
    };

    private ModelPoint Midpoint()
    {
        ModelPoint mid = PointAtHalfLength(GetFlattenedPoints(), out ModelPoint along);
        ModelPoint perpendicular = new(-along.Y, along.X);
        return mid + (perpendicular * LabelOffset);
    }

    private static ModelPoint PointAtHalfLength(IReadOnlyList<ModelPoint> points, out ModelPoint direction)
    {
        double total = 0d;
        for (int i = 1; i < points.Count; i++)
        {
            total += points[i].DistanceTo(points[i - 1]);
        }

        double half = total / 2d;
        double accumulated = 0d;
        for (int i = 1; i < points.Count; i++)
        {
            double segment = points[i].DistanceTo(points[i - 1]);
            if (segment > 1e-9 && accumulated + segment >= half)
            {
                double t = (half - accumulated) / segment;
                direction = (points[i] - points[i - 1]).Normalized();
                return points[i - 1] + ((points[i] - points[i - 1]) * t);
            }

            accumulated += segment;
        }

        direction = (points[points.Count - 1] - points[0]).Normalized();
        return (points[0] + points[points.Count - 1]) * 0.5d;
    }

    private static ModelPoint EndLabelPosition(ModelPoint anchor, ModelPoint outward)
    {
        ModelPoint perpendicular = new(-outward.Y, outward.X);
        return anchor + (outward * (LabelOffset + 6d)) + (perpendicular * LabelOffset);
    }

    private void RaiseAll()
    {
        OnPropertyChanged(nameof(Geometry));
        OnPropertyChanged(nameof(SourceDecoration));
        OnPropertyChanged(nameof(SourceDecorationFill));
        OnPropertyChanged(nameof(TargetDecoration));
        OnPropertyChanged(nameof(TargetDecorationFill));
        OnPropertyChanged(nameof(StrokeDashArray));
        OnPropertyChanged(nameof(CenterLabelText));
        OnPropertyChanged(nameof(HasCenterLabel));
        OnPropertyChanged(nameof(CenterLabelX));
        OnPropertyChanged(nameof(CenterLabelY));
        OnPropertyChanged(nameof(SourceLabelText));
        OnPropertyChanged(nameof(HasSourceLabel));
        OnPropertyChanged(nameof(SourceLabelX));
        OnPropertyChanged(nameof(SourceLabelY));
        OnPropertyChanged(nameof(TargetLabelText));
        OnPropertyChanged(nameof(HasTargetLabel));
        OnPropertyChanged(nameof(TargetLabelX));
        OnPropertyChanged(nameof(TargetLabelY));
    }

    /// <summary>Re-raises style/decoration/label properties after the inspector edits the model.</summary>
    public void RaiseStyleChanged()
    {
        OnPropertyChanged(nameof(Stroke));
        OnPropertyChanged(nameof(StrokeThickness));
        OnPropertyChanged(nameof(LabelForeground));
        Recompute();
    }

    private static Point ToPoint(ModelPoint p) => new(p.X, p.Y);
}

/// <summary>Identifies one of a connector's three movable labels.</summary>
public enum ConnectorLabelKind
{
    Source,
    Center,
    Target,
}
