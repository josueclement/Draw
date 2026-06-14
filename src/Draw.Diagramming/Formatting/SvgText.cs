using System.Globalization;
using System.Text;

namespace Draw.Diagramming.Formatting;

/// <summary>
/// Pure (Avalonia-free) text/number formatting for SVG export: invariant-culture numbers and XML
/// entity escaping. The colour/brush helpers live in the App layer's <c>SvgFormat</c>.
/// </summary>
public static class SvgText
{
    /// <summary>Formats a coordinate/length with invariant culture and at most three decimals.</summary>
    public static string Num(double value)
        => System.Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Escapes text for use as SVG element content or an attribute value. Single pass: the input is
    /// returned unchanged (no allocation) when nothing needs escaping.
    /// </summary>
    public static string Escape(string text)
    {
        StringBuilder? builder = null;
        for (int i = 0; i < text.Length; i++)
        {
            string? entity = text[i] switch
            {
                '&' => "&amp;",
                '<' => "&lt;",
                '>' => "&gt;",
                '"' => "&quot;",
                '\'' => "&apos;",
                _ => null,
            };

            if (entity is null)
            {
                builder?.Append(text[i]);
            }
            else
            {
                builder ??= new StringBuilder(text.Length + 16).Append(text, 0, i);
                builder.Append(entity);
            }
        }

        return builder?.ToString() ?? text;
    }
}
