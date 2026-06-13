using System;
using System.Collections.Generic;
using System.Linq;
using Draw.App.Services;
using Draw.Diagramming.Styling;
using Draw.Model.Primitives;
using Draw.Model.Styling;

namespace Draw.App.ViewModels;

/// <summary>
/// Owns styling the selection (quick-palette swatches, reset-to-default, no-fill) and re-resolving
/// theme-aware brushes when the active theme changes, over the shared <see cref="IDocumentEditContext"/>.
/// The document view model keeps the public style methods the inspector/palette bind to and forwards
/// here, mirroring <see cref="SelectionCoordinator"/>. Subscribes to the theme service for its lifetime;
/// the view model disposes it when the tab closes.
/// </summary>
public sealed class StyleCoordinator : IDisposable
{
    private readonly IDocumentEditContext _context;
    private readonly IThemeService _theme;

    public StyleCoordinator(IDocumentEditContext context, IThemeService theme)
    {
        _context = context;
        _theme = theme;
        _theme.ThemeChanged += OnThemeChanged;
    }

    /// <summary>Applies a quick-palette swatch to the whole selection: a coordinated fill + stroke +
    /// text on every selected node, and stroke + label colour on a selected connector. Stores the
    /// swatch id so the colours follow the active theme, and bakes the current theme's variant into the
    /// raw colour fields as a fallback. One undo step; a no-op (and no undo) when nothing is selected.</summary>
    public void ApplyStyleSwatch(StyleSwatch swatch)
    {
        SwatchVariant v = swatch.Variant(_theme.IsDark);
        ApplyStyleToSelection(
            style =>
            {
                style.PaletteId = swatch.Id;
                style.Fill = v.Fill;
                style.Stroke.Color = v.Stroke;
                style.Font.Color = v.Text;
            },
            style =>
            {
                style.PaletteId = swatch.Id;
                style.Stroke.Color = v.Stroke;
                style.Font.Color = v.Text;
            });
    }

    /// <summary>Resets the selection to the default style: the first ("Blue") quick-palette swatch,
    /// which is also what newly-created shapes get. Behaves exactly like clicking that swatch — a
    /// theme-aware fill/stroke/text linked to the palette. Stroke thickness, dash, and font family/size
    /// are left untouched.</summary>
    public void ResetStyleToDefault() => ApplyStyleSwatch(StylePalette.Default);

    /// <summary>Makes the selected nodes outline-only (transparent fill) and unlinks them from any
    /// palette swatch. Connectors have no fill, so they're unaffected.</summary>
    public void ApplyNoFill()
        => ApplyStyleToSelection(
            style =>
            {
                style.PaletteId = null;
                style.Fill = ArgbColor.Transparent;
            },
            mutateConnector: null);

    // Shared body for the palette actions: one undo snapshot, mutate every selected node + a selected
    // connector, refresh bindings, mark dirty. No-op (no undo) when the selection is empty.
    private void ApplyStyleToSelection(Action<ShapeStyle> mutateNode, Action<ConnectorStyle>? mutateConnector)
    {
        List<NodeViewModelBase> nodes = _context.SelectedNodes.ToList();
        List<ConnectorViewModel> connectors = _context.SelectedConnectors.ToList();
        if (nodes.Count == 0 && connectors.Count == 0)
        {
            return;
        }

        _context.CaptureUndo();
        foreach (NodeViewModelBase node in nodes)
        {
            mutateNode(node.Model.Style);
            node.RaiseStyleChanged();
        }

        if (mutateConnector is not null)
        {
            foreach (ConnectorViewModel connector in connectors)
            {
                mutateConnector(connector.Model.Style);
                connector.RaiseStyleChanged();
            }
        }

        _context.MarkModified();
    }

    // On theme change, re-raise style-derived brushes so default-styled nodes adopt the new theme's
    // fill/text colours (user-customised colours are unaffected — see NodeViewModelBase.UsesDefault*).
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (NodeViewModelBase node in _context.Nodes)
        {
            node.RaiseStyleChanged();
        }

        // Palette-linked connectors resolve their stroke/label colour from the active theme too.
        foreach (ConnectorViewModel connector in _context.Connectors)
        {
            connector.RaiseStyleChanged();
        }
    }

    public void Dispose() => _theme.ThemeChanged -= OnThemeChanged;
}
