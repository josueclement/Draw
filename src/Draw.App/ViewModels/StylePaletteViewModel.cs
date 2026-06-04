using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Services;
using Draw.Diagramming.Styling;

namespace Draw.App.ViewModels;

/// <summary>The quick style palette shown in the ribbon: a grid of theme-aware swatches plus Reset and
/// No-fill actions. App-global (one instance); it targets whichever document is active, kept in sync by
/// <see cref="ShellViewModel"/>.</summary>
public sealed class StylePaletteViewModel : ViewModelBase
{
    private readonly IThemeService _theme;
    private DiagramDocumentViewModel? _activeDocument;

    public StylePaletteViewModel(IThemeService theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));

        Swatches = StylePalette.Swatches
            .Select(swatch => new StyleSwatchViewModel(swatch, _theme, Apply))
            .ToList();

        ResetCommand = new RelayCommand(() => _activeDocument?.ResetStyleToDefault());
        NoFillCommand = new RelayCommand(() => _activeDocument?.ApplyNoFill());

        _theme.ThemeChanged += OnThemeChanged;
    }

    public IReadOnlyList<StyleSwatchViewModel> Swatches { get; }

    public RelayCommand ResetCommand { get; }

    public RelayCommand NoFillCommand { get; }

    /// <summary>Points the palette at the active document (call when the active tab changes).</summary>
    public void SetActiveDocument(DiagramDocumentViewModel? document) => _activeDocument = document;

    private void Apply(StyleSwatch swatch) => _activeDocument?.ApplyStyleSwatch(swatch);

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (StyleSwatchViewModel swatch in Swatches)
        {
            swatch.RaisePreviewChanged();
        }
    }
}
