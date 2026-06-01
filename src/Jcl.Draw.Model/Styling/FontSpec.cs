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

    public ArgbColor Color { get; set; } = ArgbColor.Black;

    public FontSpec Clone() => new()
    {
        Family = Family,
        Size = Size,
        Bold = Bold,
        Italic = Italic,
        Color = Color,
    };
}
