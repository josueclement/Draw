using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.VisualTree;
using Draw.App.ViewModels;
using Draw.Diagramming.Geometry;
using Draw.Model.Nodes;
using static Draw.App.Rendering.SvgFormat;
using ModelPoint = Draw.Model.Primitives.Point2D;
using ModelRect = Draw.Model.Primitives.Rect2D;

namespace Draw.App.Rendering;

/// <summary>
/// Renders the diagram to a full-parity SVG document. Geometry (shape outlines, connector routes and
/// decorations, images) is emitted from the view models / model data; text and compartment separators
/// are harvested from the already-laid-out node controls (so class/entity layout, fonts and resolved
/// theme colours match the on-screen rendering exactly). Everything is emitted in world coordinates
/// under a single translate that maps the padded content bounds to the SVG origin.
/// </summary>
public static class DiagramSvgRenderer
{
    // Connector labels render at a fixed 11px in the template; baseline ≈ 0.8·em below the top-left
    // anchor. Node label baselines use the measured line box (see EmitText).
    private const double ConnectorLabelFontSize = 11d;
    private const double BaselineRatio = 0.8d;

    public static string Render(
        Visual nodesLayer,
        IReadOnlyList<NodeViewModelBase> nodes,
        IReadOnlyList<ConnectorViewModel> connectors,
        ModelRect content,
        double padding)
    {
        double width = content.Width + (padding * 2d);
        double height = content.Height + (padding * 2d);

        // Map each node view model to its realized container so we can emit a node's fill and then walk
        // that same node's text in document/z order.
        Dictionary<NodeViewModelBase, Visual> containers = new();
        foreach (Visual descendant in nodesLayer.GetVisualDescendants())
        {
            if (descendant is ContentPresenter { DataContext: NodeViewModelBase vm } presenter
                && !containers.ContainsKey(vm))
            {
                containers[vm] = presenter;
            }
        }

        StringBuilder sb = new();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"no\"?>\n");
        sb.Append("<svg xmlns=\"http://www.w3.org/2000/svg\" ")
          .Append("width=\"").Append(Num(width)).Append("\" height=\"").Append(Num(height)).Append("\" ")
          .Append("viewBox=\"0 0 ").Append(Num(width)).Append(' ').Append(Num(height)).Append("\">\n");
        sb.Append("<g transform=\"translate(")
          .Append(Num(padding - content.X)).Append(',').Append(Num(padding - content.Y)).Append(")\">\n");

        // Nodes (fills + their text) in z order; connectors render above all nodes (matching the app).
        foreach (NodeViewModelBase node in nodes.OrderBy(n => n.ZIndex))
        {
            EmitNode(sb, node);
            if (containers.TryGetValue(node, out Visual? container))
            {
                WalkText(sb, container, nodesLayer);
            }
        }

        foreach (ConnectorViewModel connector in connectors)
        {
            EmitConnector(sb, connector);
        }

        sb.Append("</g>\n</svg>\n");
        return sb.ToString();
    }

    private static void EmitNode(StringBuilder sb, NodeViewModelBase node)
    {
        double x = node.X;
        double y = node.Y;
        double w = node.Width;
        double h = node.Height;
        double thickness = node.Model.Style.Stroke.Thickness;

        switch (node)
        {
            case ShapeNodeViewModel shape:
                EmitShape(sb, ShapeSvgPathBuilder.Build(shape.Kind, w, h, shape.Model.CornerRadius), x, y, node, thickness);
                break;
            case UseCaseNodeViewModel:
                EmitShape(sb, ShapeSvgPathBuilder.Build(ShapeKind.Ellipse, w, h, 0d), x, y, node, thickness);
                break;
            case ActorNodeViewModel:
                EmitActor(sb, x, y, w, h, node.Stroke, thickness);
                break;
            case ImageNodeViewModel when node.Model is ImageNode image:
                EmitImage(sb, image, x, y, w, h);
                break;
            case ClassNodeViewModel:
            case EntityNodeViewModel:
            case SystemBoundaryNodeViewModel:
                EmitRect(sb, x, y, w, h, node, thickness);
                break;
            case PackageNodeViewModel package:
                EmitRect(sb, x, y, package.TabWidth, package.TabHeight, node, thickness);
                EmitRect(sb, x, y + package.TabHeight, w, h - package.TabHeight, node, thickness);
                break;
            case ComponentNodeViewModel:
                // Body plus the two port tabs straddling the left edge (matches the data template).
                EmitRect(sb, x, y, w, h, node, thickness);
                EmitRect(sb, x - 6d, y + 16d, 12d, 16d, node, thickness);
                EmitRect(sb, x - 6d, y + 44d, 12d, 16d, node, thickness);
                break;
            case DeploymentNodeViewModel:
                EmitShape(sb, UmlNodeGeometry.DeploymentSvg(w, h), x, y, node, thickness);
                break;
        }
    }

    private static void EmitRect(StringBuilder sb, double x, double y, double w, double h, NodeViewModelBase node, double thickness)
        => sb.Append("<rect x=\"").Append(Num(x)).Append("\" y=\"").Append(Num(y))
             .Append("\" width=\"").Append(Num(w)).Append("\" height=\"").Append(Num(h)).Append("\" ")
             .Append(Paint("fill", node.Fill)).Append(' ').Append(Paint("stroke", node.Stroke))
             .Append(" stroke-width=\"").Append(Num(thickness)).Append("\"/>\n");

    private static void EmitShape(StringBuilder sb, SvgShape shape, double x, double y, NodeViewModelBase node, double thickness)
    {
        string transform = $"transform=\"translate({Num(x)},{Num(y)})\"";
        sb.Append("<path d=\"").Append(shape.FillPath).Append("\" ").Append(transform).Append(' ')
          .Append(Paint("fill", node.Fill)).Append(' ').Append(Paint("stroke", node.Stroke))
          .Append(" stroke-width=\"").Append(Num(thickness)).Append('"')
          .Append(DashAttribute(node.StrokeDashArray)).Append("/>\n");

        if (shape.StrokeOnlyPath is { } strokeOnly)
        {
            sb.Append("<path d=\"").Append(strokeOnly).Append("\" ").Append(transform)
              .Append(" fill=\"none\" ").Append(Paint("stroke", node.Stroke))
              .Append(" stroke-width=\"").Append(Num(thickness)).Append("\"/>\n");
        }
    }

    // Stick figure from the shared ActorDimensions: head circle + body/arms/legs polylines, stroke only.
    private static void EmitActor(StringBuilder sb, double x, double y, double width, double height, IBrush? stroke, double thickness)
    {
        ActorDimensions d = new(width, height);

        string strokePaint = Paint("stroke", stroke);
        string g = $"transform=\"translate({Num(x)},{Num(y)})\"";
        sb.Append("<g ").Append(g).Append(" fill=\"none\" ").Append(strokePaint)
          .Append(" stroke-width=\"").Append(Num(thickness)).Append("\">\n");
        sb.Append("<circle cx=\"").Append(Num(d.CenterX)).Append("\" cy=\"").Append(Num(d.HeadRadius))
          .Append("\" r=\"").Append(Num(d.HeadRadius)).Append("\"/>\n");
        sb.Append("<path d=\"")
          .Append($"M{Num(d.CenterX)},{Num(d.NeckY)} L{Num(d.CenterX)},{Num(d.HipY)} ")
          .Append($"M{Num(d.CenterX - d.ArmHalf)},{Num(d.ShoulderY)} L{Num(d.CenterX + d.ArmHalf)},{Num(d.ShoulderY)} ")
          .Append($"M{Num(d.CenterX - d.LegSpread)},{Num(d.FigureHeight)} L{Num(d.CenterX)},{Num(d.HipY)} L{Num(d.CenterX + d.LegSpread)},{Num(d.FigureHeight)}")
          .Append("\"/>\n");
        sb.Append("</g>\n");
    }

    private static void EmitImage(StringBuilder sb, ImageNode image, double x, double y, double w, double h)
    {
        if (image.Data.Length == 0)
        {
            return;
        }

        string mime = image.Format.ToLowerInvariant() switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            _ => "image/png",
        };
        string data = System.Convert.ToBase64String(image.Data);
        sb.Append("<image x=\"").Append(Num(x)).Append("\" y=\"").Append(Num(y))
          .Append("\" width=\"").Append(Num(w)).Append("\" height=\"").Append(Num(h))
          .Append("\" preserveAspectRatio=\"none\" href=\"data:").Append(mime).Append(";base64,")
          .Append(data).Append("\"/>\n");
    }

    private static void EmitConnector(StringBuilder sb, ConnectorViewModel connector)
    {
        // Mind-map branch: filled tapered ribbon(s) (matching the on-canvas fill), no decorations.
        // One polygon when solid, one per dash run for a Dash/Dot/DashDot style.
        if (connector.IsMindMapBranch)
        {
            foreach (IReadOnlyList<ModelPoint> outline in connector.GetBranchOutlines())
            {
                if (outline.Count >= 3)
                {
                    sb.Append("<polygon points=\"")
                      .Append(string.Join(" ", outline.Select(p => $"{Num(p.X)},{Num(p.Y)}")))
                      .Append("\" ").Append(Paint("fill", connector.Stroke)).Append(" stroke=\"none\"/>\n");
                }
            }

            EmitConnectorLabel(sb, connector.CenterLabelText, connector.HasCenterLabel, connector.CenterLabelX, connector.CenterLabelY, connector.LabelForeground);
            EmitConnectorLabel(sb, connector.SourceLabelText, connector.HasSourceLabel, connector.SourceLabelX, connector.SourceLabelY, connector.LabelForeground);
            EmitConnectorLabel(sb, connector.TargetLabelText, connector.HasTargetLabel, connector.TargetLabelX, connector.TargetLabelY, connector.LabelForeground);
            return;
        }

        string points = string.Join(" ", connector.GetFlattenedPoints().Select(p => $"{Num(p.X)},{Num(p.Y)}"));
        double thickness = connector.StrokeThickness;
        sb.Append("<polyline points=\"").Append(points).Append("\" fill=\"none\" ")
          .Append(Paint("stroke", connector.Stroke)).Append(" stroke-width=\"").Append(Num(thickness)).Append('"')
          .Append(DashAttribute(connector.StrokeDashArray)).Append("/>\n");

        EmitDecoration(sb, connector.SourceEndDecoration, connector.RouteStart, connector.RouteStartDirection * -1d,
            connector.SourceDecorationFill, connector.Stroke, thickness);
        EmitDecoration(sb, connector.TargetEndDecoration, connector.RouteEnd, connector.RouteEndDirection,
            connector.TargetDecorationFill, connector.Stroke, thickness);

        EmitConnectorLabel(sb, connector.CenterLabelText, connector.HasCenterLabel, connector.CenterLabelX, connector.CenterLabelY, connector.LabelForeground);
        EmitConnectorLabel(sb, connector.SourceLabelText, connector.HasSourceLabel, connector.SourceLabelX, connector.SourceLabelY, connector.LabelForeground);
        EmitConnectorLabel(sb, connector.TargetLabelText, connector.HasTargetLabel, connector.TargetLabelX, connector.TargetLabelY, connector.LabelForeground);
    }

    private static void EmitDecoration(StringBuilder sb, ConnectorEndDecoration decoration, ModelPoint tip, ModelPoint direction, IBrush? fill, IBrush? stroke, double thickness)
    {
        if (ConnectorDecorationBuilder.Describe(decoration, tip, direction) is not { } data)
        {
            return;
        }

        string strokePaint = Paint("stroke", stroke);
        string fillPaint = Paint("fill", fill);
        string strokeWidth = Num(thickness);

        foreach (ModelPoint[] polygon in data.ClosedPaths)
        {
            sb.Append("<polygon points=\"").Append(Points(polygon)).Append("\" ")
              .Append(fillPaint).Append(' ').Append(strokePaint)
              .Append(" stroke-width=\"").Append(strokeWidth).Append("\"/>\n");
        }

        foreach (ModelPoint[] polyline in data.OpenPaths)
        {
            sb.Append("<polyline points=\"").Append(Points(polyline)).Append("\" fill=\"none\" ")
              .Append(strokePaint).Append(" stroke-width=\"").Append(strokeWidth).Append("\"/>\n");
        }

        foreach ((ModelPoint center, double radius) in data.Circles)
        {
            sb.Append("<circle cx=\"").Append(Num(center.X)).Append("\" cy=\"").Append(Num(center.Y))
              .Append("\" r=\"").Append(Num(radius)).Append("\" fill=\"none\" ")
              .Append(strokePaint).Append(" stroke-width=\"").Append(strokeWidth).Append("\"/>\n");
        }
    }

    private static void EmitConnectorLabel(StringBuilder sb, string? text, bool has, double x, double y, IBrush? foreground)
    {
        if (!has || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        Color color = ColorOf(foreground) ?? Colors.Black;
        sb.Append("<text x=\"").Append(Num(x)).Append("\" y=\"").Append(Num(y + (ConnectorLabelFontSize * BaselineRatio)))
          .Append("\" font-family=\"Inter, sans-serif\" font-size=\"").Append(Num(ConnectorLabelFontSize))
          .Append("\" fill=\"").Append(Hex(color)).Append("\">").Append(Escape(text!)).Append("</text>\n");
    }

    // Walks a node's laid-out container, emitting visible TextBlocks as <text> and thin compartment
    // separator Rectangles as <line>. Buttons and TextBoxes (the hover "+" and inline editors) are
    // skipped; all other containers are recursed through. Positions come from the live layout.
    private static void WalkText(StringBuilder sb, Visual node, Visual root)
    {
        foreach (Visual child in node.GetVisualChildren())
        {
            if (child is Button or TextBox)
            {
                continue;
            }

            if (child is Control { IsVisible: false })
            {
                continue;
            }

            switch (child)
            {
                case TextBlock textBlock:
                    EmitText(sb, textBlock, root);
                    break;
                case Rectangle rectangle when rectangle.Bounds.Height <= 2d:
                    EmitSeparator(sb, rectangle, root);
                    break;
                default:
                    WalkText(sb, child, root);
                    break;
            }
        }
    }

    private static void EmitText(StringBuilder sb, TextBlock textBlock, Visual root)
    {
        string? text = textBlock.Text;
        if (string.IsNullOrWhiteSpace(text) || textBlock.TransformToVisual(root) is not { } matrix)
        {
            return;
        }

        Point topLeft = matrix.Transform(new Point(0d, 0d));
        double w = textBlock.Bounds.Width;
        double h = textBlock.Bounds.Height;
        double baseline = topLeft.Y + (h * BaselineRatio);

        double anchorX;
        string anchor;
        switch (textBlock.TextAlignment)
        {
            case TextAlignment.Center:
                anchorX = topLeft.X + (w / 2d);
                anchor = "middle";
                break;
            case TextAlignment.Right:
                anchorX = topLeft.X + w;
                anchor = "end";
                break;
            default:
                anchorX = topLeft.X;
                anchor = "start";
                break;
        }

        Color color = ColorOf(textBlock.Foreground) ?? Colors.Black;
        string family = string.IsNullOrEmpty(textBlock.FontFamily?.Name) ? "sans-serif" : textBlock.FontFamily!.Name;

        sb.Append("<text x=\"").Append(Num(anchorX)).Append("\" y=\"").Append(Num(baseline))
          .Append("\" font-family=\"").Append(Escape(family)).Append(", sans-serif\" font-size=\"").Append(Num(textBlock.FontSize))
          .Append("\" font-weight=\"").Append((int)textBlock.FontWeight).Append('"');
        if (textBlock.FontStyle != FontStyle.Normal)
        {
            sb.Append(" font-style=\"italic\"");
        }

        string? decoration = DecorationValue(textBlock.TextDecorations);
        if (decoration is not null)
        {
            sb.Append(" text-decoration=\"").Append(decoration).Append('"');
        }

        sb.Append(" fill=\"").Append(Hex(color)).Append("\" text-anchor=\"").Append(anchor).Append("\">")
          .Append(Escape(text!)).Append("</text>\n");
    }

    private static void EmitSeparator(StringBuilder sb, Rectangle rectangle, Visual root)
    {
        if (rectangle.TransformToVisual(root) is not { } matrix)
        {
            return;
        }

        Point topLeft = matrix.Transform(new Point(0d, 0d));
        double w = rectangle.Bounds.Width;
        Color color = ColorOf(rectangle.Fill) ?? Colors.Gray;
        sb.Append("<line x1=\"").Append(Num(topLeft.X)).Append("\" y1=\"").Append(Num(topLeft.Y))
          .Append("\" x2=\"").Append(Num(topLeft.X + w)).Append("\" y2=\"").Append(Num(topLeft.Y))
          .Append("\" stroke=\"").Append(Hex(color)).Append("\" stroke-width=\"1\"/>\n");
    }

    private static string Points(ModelPoint[] points)
        => string.Join(" ", points.Select(p => $"{Num(p.X)},{Num(p.Y)}"));

    private static string DashAttribute(Avalonia.Collections.AvaloniaList<double>? dashes)
    {
        if (dashes is null || dashes.Count == 0)
        {
            return string.Empty;
        }

        return $" stroke-dasharray=\"{string.Join(",", dashes.Select(Num))}\"";
    }

    private static string? DecorationValue(TextDecorationCollection? decorations)
    {
        if (decorations is null || decorations.Count == 0)
        {
            return null;
        }

        List<string> parts = new();
        foreach (TextDecoration decoration in decorations)
        {
            switch (decoration.Location)
            {
                case TextDecorationLocation.Underline:
                    parts.Add("underline");
                    break;
                case TextDecorationLocation.Strikethrough:
                    parts.Add("line-through");
                    break;
                case TextDecorationLocation.Overline:
                    parts.Add("overline");
                    break;
            }
        }

        return parts.Count == 0 ? null : string.Join(' ', parts);
    }
}
