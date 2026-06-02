namespace Jcl.Draw.Model.Styling;

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

    // Default text: Carbon "foreground" near-black (#1E1F22) — readable on the default grey fill.
    public ArgbColor Color { get; set; } = new(0xFF, 0x1E, 0x1F, 0x22);

    public FontSpec Clone() => new()
    {
        Family = Family,
        Size = Size,
        Bold = Bold,
        Italic = Italic,
        Color = Color,
    };
}
