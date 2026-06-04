using System;
using System.IO;
using Avalonia.Media.Imaging;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Displays an embedded raster image. The bytes live in <see cref="ImageNode.Data"/> (base64
/// in the document); they are decoded once into a <see cref="Bitmap"/> for rendering.</summary>
public sealed class ImageNodeViewModel : NodeViewModelBase, IDisposable
{
    public ImageNodeViewModel(ImageNode model, IThemeService theme)
        : base(model, theme)
    {
        Image = Decode(model.Data);
    }

    /// <summary>The decoded bitmap, or <c>null</c> when the bytes are empty/undecodable.</summary>
    public Bitmap? Image { get; }

    /// <summary>Connectors attach to images on a rectangular boundary.</summary>
    public override ShapeKind BoundaryKind => ShapeKind.Rectangle;

    /// <summary>Images keep their proportions when resized.</summary>
    public override bool LocksAspectRatio => true;

    public void Dispose() => Image?.Dispose();

    private static Bitmap? Decode(byte[] data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        // The bytes are untrusted (external clipboard / picked file / possibly hand-edited document):
        // a decode failure must degrade to a blank box, never crash the editor on open. This is a
        // deliberate broad catch around third-party image decoding, which has no single public failure type.
        try
        {
            using MemoryStream stream = new(data);
            return new Bitmap(stream);
        }
        catch (Exception)
        {
            return null;
        }
    }
}
