using System.Globalization;
using Avalonia.Media;

namespace Draw.App.Rendering;

/// <summary>Low-level formatting helpers shared by the SVG export (numbers, colours, text escaping).</summary>
public static class SvgFormat
{
    /// <summary>Formats a coordinate/length with invariant culture and at most three decimals.</summary>
    public static string Num(double value)
        => System.Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>Escapes text for use as SVG element content or an attribute value.</summary>
    public static string Escape(string text) => text
        .Replace("&", "&amp;")
        .Replace("<", "&lt;")
        .Replace(">", "&gt;")
        .Replace("\"", "&quot;")
        .Replace("'", "&apos;");

    /// <summary>The solid colour of a brush, or null for a non-solid/absent brush.</summary>
    public static Color? ColorOf(IBrush? brush) => brush is ISolidColorBrush solid ? solid.Color : null;

    public static string Hex(Color color) => $"#{color.R:x2}{color.G:x2}{color.B:x2}";

    /// <summary>
    /// Builds an SVG paint attribute pair, e.g. <c>fill="#aabbcc"</c> plus <c>fill-opacity="0.5"</c> when
    /// translucent. A null/non-solid brush or a fully transparent colour yields <c>{attr}="none"</c>.
    /// </summary>
    public static string Paint(string attribute, IBrush? brush)
    {
        if (ColorOf(brush) is not { } color || color.A == 0)
        {
            return $"{attribute}=\"none\"";
        }

        string paint = $"{attribute}=\"{Hex(color)}\"";
        return color.A == 255 ? paint : $"{paint} {attribute}-opacity=\"{Num(color.A / 255d)}\"";
    }
}
