using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Draw.Model.Styling;

namespace Draw.Diagramming.Styling;

/// <summary>One theme variant of a <see cref="StyleSwatch"/>: the coordinated fill, stroke and text colours.</summary>
public sealed record SwatchVariant(ArgbColor Fill, ArgbColor Stroke, ArgbColor Text);

/// <summary>
/// A named entry in the quick style palette. Carries a Light and a Dark variant so a styled element
/// can recolour with the active theme. <see cref="Id"/> is the persisted token (stored on
/// <c>ShapeStyle.PaletteId</c> / <c>ConnectorStyle.PaletteId</c>) and must stay stable forever — a
/// rename only changes <see cref="Name"/>.
/// </summary>
public sealed record StyleSwatch(string Id, string Name, SwatchVariant Light, SwatchVariant Dark)
{
    /// <summary>The variant for the active theme.</summary>
    public SwatchVariant Variant(bool isDark) => isDark ? Dark : Light;

    /// <summary>Builds a shape style carrying this swatch: theme-aware via <see cref="ShapeStyle.PaletteId"/>,
    /// with the active theme's colours baked in as the fallback. Mirrors the per-node mutation in
    /// <c>DiagramDocumentViewModel.ApplyStyleSwatch</c>.</summary>
    public ShapeStyle ToShapeStyle(bool isDark)
    {
        SwatchVariant variant = Variant(isDark);
        ShapeStyle style = ShapeStyle.CreateDefault();
        style.PaletteId = Id;
        style.Fill = variant.Fill;
        style.Stroke.Color = variant.Stroke;
        style.Font.Color = variant.Text;
        return style;
    }
}

/// <summary>
/// The curated quick style palette: low-saturation ("pastel") swatches, each with a Light and a Dark
/// variant. UI-agnostic (model <see cref="ArgbColor"/> only) so it can be consumed by both the view
/// models that resolve a stored <c>PaletteId</c> and the ribbon palette UI.
/// </summary>
public static class StylePalette
{
    // Shared text colours: near-black on the light tints, near-white on the dark tones.
    private static readonly ArgbColor LightText = Rgb(0x1E1F22);
    private static readonly ArgbColor DarkText = Rgb(0xECECEE);

    /// <summary>The palette entries, in display order (paired as a 5×2 grid).</summary>
    public static IReadOnlyList<StyleSwatch> Swatches { get; } = new[]
    {
        Swatch("blue", "Blue", 0xDCE8FB, 0x5B8FE0, 0x2E3F57, 0x6E9BE6),
        Swatch("teal", "Teal", 0xD5EEEA, 0x4FA89B, 0x29423E, 0x5FB8AA),
        Swatch("green", "Green", 0xDDF0D8, 0x66A85A, 0x324A2C, 0x79BE6C),
        Swatch("sand", "Sand", 0xF6F0CE, 0xC9B24E, 0x4A4528, 0xD8C260),
        Swatch("orange", "Orange", 0xFBE6D2, 0xDE9A56, 0x543F2A, 0xE8A968),
        Swatch("coral", "Coral", 0xFBDDDC, 0xDD7A77, 0x553233, 0xE88E8A),
        Swatch("pink", "Pink", 0xF8DCEC, 0xD77AAE, 0x4F2E40, 0xE58FC0),
        Swatch("purple", "Purple", 0xE7DEF7, 0x9579D0, 0x3D3357, 0xA88FE0),
        Swatch("gray", "Gray", 0xE8E8EA, 0x9A9AA0, 0x3A3A3E, 0x9CA0A8),
        Swatch("slate", "Slate", 0xDEE4EA, 0x7E8C9C, 0x38414C, 0x93A2B3),
    };

    /// <summary>The swatch new shapes adopt by default: the first (top-left) palette entry.</summary>
    public static StyleSwatch Default => Swatches[0];

    private static readonly Dictionary<string, StyleSwatch> ById = BuildIndex(Swatches);

    /// <summary>Looks up a swatch by its stored id; <c>false</c> for null/unknown ids.</summary>
    public static bool TryGet(string? id, [MaybeNullWhen(false)] out StyleSwatch swatch)
    {
        if (id is not null && ById.TryGetValue(id, out StyleSwatch? found))
        {
            swatch = found;
            return true;
        }

        swatch = null;
        return false;
    }

    private static StyleSwatch Swatch(string id, string name, int lightFill, int lightStroke, int darkFill, int darkStroke)
        => new(
            id,
            name,
            new SwatchVariant(Rgb(lightFill), Rgb(lightStroke), LightText),
            new SwatchVariant(Rgb(darkFill), Rgb(darkStroke), DarkText));

    private static Dictionary<string, StyleSwatch> BuildIndex(IReadOnlyList<StyleSwatch> swatches)
    {
        Dictionary<string, StyleSwatch> index = new(swatches.Count);
        foreach (StyleSwatch swatch in swatches)
        {
            index[swatch.Id] = swatch;
        }

        return index;
    }

    // Opaque ARGB from a 0xRRGGBB literal.
    private static ArgbColor Rgb(int hex)
        => new(0xFF, (byte)(hex >> 16), (byte)(hex >> 8), (byte)hex);
}
