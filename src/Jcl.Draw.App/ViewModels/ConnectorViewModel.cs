using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using Jcl.Draw.App.Rendering;
using Jcl.Draw.Diagramming.Routing;
using Jcl.Draw.Model.Connectors;
using ModelPoint = Jcl.Draw.Model.Primitives.Point2D;

namespace Jcl.Draw.App.ViewModels;

/// <summary>Bindable wrapper over a <see cref="Connector"/>: routed geometry, UML decorations and labels.</summary>
public sealed class ConnectorViewModel : ViewModelBase
{
    private const double LabelOffset = 12d;

    private readonly Connector _model;
    private readonly IConnectorRouter _router;
    private ConnectorRoute _route;

    public ConnectorViewModel(Connector model, ShapeNodeViewModel source, ShapeNodeViewModel target, IConnectorRouter router)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        _router = router ?? throw new ArgumentNullException(nameof(router));

        Source.PropertyChanged += OnEndpointChanged;
        Target.PropertyChanged += OnEndpointChanged;
        _route = Compute();
    }

    public Connector Model => _model;

    public Guid Id => _model.Id;

    public ShapeNodeViewModel Source { get; }

    public ShapeNodeViewModel Target { get; }

    public bool IsSelected
    {
        get;
        set => SetProperty(ref field, value);
    }

    public Geometry Geometry => BuildLineGeometry();

    public IBrush Stroke => _model.Style.Stroke.Color.ToBrush();

    public double StrokeThickness => _model.Style.Stroke.Thickness;

    public AvaloniaList<double>? StrokeDashArray
        => ConnectorDecorationBuilder.Describe(_model.Kind).Dashed
            ? new AvaloniaList<double> { 6, 3 }
            : _model.Style.Stroke.Dash.ToDashArray();

    public Geometry? SourceDecoration
    {
        get
        {
            ConnectorEndDecoration deco = ConnectorDecorationBuilder.Describe(_model.Kind).Source;
            return ConnectorDecorationBuilder.Build(deco, _route.Start, _route.StartDirection * -1d);
        }
    }

    public IBrush? SourceDecorationFill => DecorationFill(ConnectorDecorationBuilder.Describe(_model.Kind).Source);

    public Geometry? TargetDecoration
    {
        get
        {
            ConnectorEndDecoration deco = ConnectorDecorationBuilder.Describe(_model.Kind).Target;
            return ConnectorDecorationBuilder.Build(deco, _route.End, _route.EndDirection);
        }
    }

    public IBrush? TargetDecorationFill => DecorationFill(ConnectorDecorationBuilder.Describe(_model.Kind).Target);

    public string CenterLabelText => _model.CenterLabel ?? DefaultStereotype();

    public bool HasCenterLabel => !string.IsNullOrEmpty(CenterLabelText);

    public double CenterLabelX => Midpoint().X;

    public double CenterLabelY => Midpoint().Y;

    public string? SourceLabelText => _model.SourceLabel;

    public bool HasSourceLabel => !string.IsNullOrEmpty(_model.SourceLabel);

    public double SourceLabelX => EndLabelPosition(_route.Start, _route.StartDirection).X;

    public double SourceLabelY => EndLabelPosition(_route.Start, _route.StartDirection).Y;

    public string? TargetLabelText => _model.TargetLabel;

    public bool HasTargetLabel => !string.IsNullOrEmpty(_model.TargetLabel);

    public double TargetLabelX => EndLabelPosition(_route.End, _route.EndDirection * -1d).X;

    public double TargetLabelY => EndLabelPosition(_route.End, _route.EndDirection * -1d).Y;

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

    private ConnectorRoute Compute()
    {
        ConnectorRouteRequest request = new(
            Source.Model.Kind,
            Source.Model.Bounds,
            Target.Model.Kind,
            Target.Model.Bounds,
            _model.Route,
            _model.BendPoints);
        return _router.Route(request);
    }

    private void OnEndpointChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ShapeNodeViewModel.X)
            or nameof(ShapeNodeViewModel.Y)
            or nameof(ShapeNodeViewModel.Width)
            or nameof(ShapeNodeViewModel.Height))
        {
            Recompute();
        }
    }

    private Geometry BuildLineGeometry()
    {
        if (_route.IsBezier)
        {
            StreamGeometry geometry = new();
            using (StreamGeometryContext ctx = geometry.Open())
            {
                ctx.BeginFigure(ToPoint(_route.Start), isFilled: false);
                ctx.CubicBezierTo(ToPoint(_route.Control1), ToPoint(_route.Control2), ToPoint(_route.End));
                ctx.EndFigure(false);
            }

            return geometry;
        }

        return new PolylineGeometry(_route.Points.Select(ToPoint).ToList(), isFilled: false);
    }

    private IBrush? DecorationFill(ConnectorEndDecoration decoration)
    {
        if (decoration is ConnectorEndDecoration.None or ConnectorEndDecoration.OpenArrow)
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

    /// <summary>The route as a flattened world-coordinate polyline (bezier curves are sampled).</summary>
    public IReadOnlyList<ModelPoint> GetFlattenedPoints()
    {
        if (!_route.IsBezier)
        {
            return _route.Points;
        }

        const int segments = 16;
        List<ModelPoint> points = new(segments + 1);
        for (int i = 0; i <= segments; i++)
        {
            points.Add(CubicAt(_route.Start, _route.Control1, _route.Control2, _route.End, i / (double)segments));
        }

        return points;
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
        Recompute();
    }

    private static Point ToPoint(ModelPoint p) => new(p.X, p.Y);
}
