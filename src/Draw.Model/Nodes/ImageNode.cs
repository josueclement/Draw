using System;

namespace Draw.Model.Nodes;

/// <summary>
/// A raster image placed on the canvas. The encoded bytes are embedded in the document
/// (<see cref="byte"/>[] serialises to base64), so a <c>.draw</c> file stays self-contained.
/// </summary>
public sealed class ImageNode : NodeBase
{
    /// <summary>The original encoded image bytes (PNG/JPEG/…), stored verbatim.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    /// <summary>Informational source format (e.g. <c>png</c>, <c>jpeg</c>); not used for decoding.</summary>
    public string Format { get; set; } = "png";

    /// <summary>Native pixel width of the source image; drives initial sizing and aspect-lock.</summary>
    public int PixelWidth { get; set; }

    /// <summary>Native pixel height of the source image; drives initial sizing and aspect-lock.</summary>
    public int PixelHeight { get; set; }

    public override NodeBase Clone()
    {
        ImageNode copy = new()
        {
            Data = Data,
            Format = Format,
            PixelWidth = PixelWidth,
            PixelHeight = PixelHeight,
        };
        CopyBaseTo(copy);
        return copy;
    }
}
