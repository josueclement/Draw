using System;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Rendering;
using Draw.App.Services;
using Draw.Diagramming.Styling;

namespace Draw.App.ViewModels;

/// <summary>One swatch in the quick style palette. Its preview colours follow the active theme; clicking
/// it applies the swatch to the active document's selection.</summary>
public sealed class StyleSwatchViewModel : ViewModelBase
{
    private readonly StyleSwatch _swatch;
    private readonly IThemeService _theme;

    public StyleSwatchViewModel(StyleSwatch swatch, IThemeService theme, Action<StyleSwatch> apply)
    {
        _swatch = swatch ?? throw new ArgumentNullException(nameof(swatch));
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));
        ApplyCommand = new RelayCommand(() => apply(_swatch));
    }

    public string Name => _swatch.Name;

    /// <summary>Fill of the swatch face, in the active theme's variant.</summary>
    public IBrush PreviewBackground => _swatch.Variant(_theme.IsDark).Fill.ToBrush();

    /// <summary>Border of the swatch face, in the active theme's variant.</summary>
    public IBrush PreviewBorder => _swatch.Variant(_theme.IsDark).Stroke.ToBrush();

    public RelayCommand ApplyCommand { get; }

    /// <summary>Re-raises the preview brushes after a theme change so the swatch face re-tints.</summary>
    public void RaisePreviewChanged()
    {
        OnPropertyChanged(nameof(PreviewBackground));
        OnPropertyChanged(nameof(PreviewBorder));
    }
}
