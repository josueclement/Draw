namespace Draw.Model.Styling;

/// <summary>
/// Font settings for shape/connector text. Named <c>FontSpec</c> rather than
/// <c>FontStyle</c> to avoid confusion with the UI framework's italic/oblique enum.
/// </summary>
public sealed class FontSpec
{
    public string Family { get; set; } = "Inter";

    public double Size { get; set; } = 12d;

    public bool Bold { get; set; }

    public bool Italic { get; set; }

    // Default text: Carbon light-theme "foreground" near-black (#1E1F22). The concrete fallback used when
    // Color is null and no theme brush is available (e.g. export). Since v2, "follow the theme default"
    // is represented explicitly by a null Color — not by value-equality on this constant.
    public static readonly ArgbColor DefaultColor = new(0xFF, 0x1E, 0x1F, 0x22);

    /// <summary>The text colour, or <c>null</c> to follow the active theme's default node text.</summary>
    public ArgbColor? Color { get; set; }

    public FontSpec Clone() => new()
    {
        Family = Family,
        Size = Size,
        Bold = Bold,
        Italic = Italic,
        Color = Color,
    };
}
