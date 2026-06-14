using System;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Which stacking band a node kind is placed in when created.</summary>
public enum NodeZBand
{
    /// <summary>The normal band: the node is placed on top (max z-index + 1).</summary>
    Ordinary,

    /// <summary>The background band: the node is placed behind everything (min z-index − 1), e.g. a
    /// system boundary that frames the use cases it groups.</summary>
    Background,
}

/// <summary>
/// Everything the editor needs to place and rebuild one <see cref="NodeBase"/> subtype, in one place:
/// its default placement size, its stacking band, and the factory that builds its view model. Adding a
/// node kind is one entry in <see cref="NodeKindRegistry"/> (plus a <c>[JsonDerivedType]</c> and a
/// <c>DataTemplate</c>, both guarded by tests). The factory closes over a view-model constructor — an
/// <c>Avalonia.Media</c>-touching App type — so this descriptor lives in the App layer.
/// </summary>
public sealed record NodeKindDescriptor(
    Type ModelType,
    double DefaultWidth,
    double DefaultHeight,
    NodeZBand ZBand,
    Func<NodeBase, INodeEditContext, IThemeService, NodeViewModelBase> CreateViewModel);
