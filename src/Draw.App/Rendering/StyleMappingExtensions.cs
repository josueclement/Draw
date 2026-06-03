using Avalonia.Collections;
using Avalonia.Media;
using ModelStyle = Draw.Model.Styling;

namespace Draw.App.Rendering;

/// <summary>Maps between the framework-agnostic model styling types and Avalonia media types.</summary>
public static class StyleMappingExtensions
{
    extension(ModelStyle.ArgbColor color)
    {
        public Color ToAvaloniaColor() => Color.FromArgb(color.A, color.R, color.G, color.B);

        public IBrush ToBrush() => new SolidColorBrush(Color.FromArgb(color.A, color.R, color.G, color.B));
    }

    extension(Color color)
    {
        public ModelStyle.ArgbColor ToModelColor() => new(color.A, color.R, color.G, color.B);
    }

    extension(ModelStyle.DashStyle dash)
    {
        public AvaloniaList<double>? ToDashArray() => dash switch
        {
            ModelStyle.DashStyle.Dash => new AvaloniaList<double> { 4, 2 },
            ModelStyle.DashStyle.Dot => new AvaloniaList<double> { 1, 2 },
            ModelStyle.DashStyle.DashDot => new AvaloniaList<double> { 4, 2, 1, 2 },
            _ => null,
        };
    }

    extension(ModelStyle.TextAlignment alignment)
    {
        public TextAlignment ToAvalonia() => alignment switch
        {
            ModelStyle.TextAlignment.Left => TextAlignment.Left,
            ModelStyle.TextAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Center,
        };
    }
}
