using System;
using Avalonia.Media;

namespace Draw.App.Services;

/// <summary>
/// Toggles the application between light and dark Fluent themes and exposes the theme-aware
/// default brushes used for nodes whose fill/text the user has not customised.
/// </summary>
public interface IThemeService
{
    bool IsDark { get; }

    /// <summary>Theme-aware fill for un-customised nodes; <c>null</c> if app resources aren't ready yet.</summary>
    IBrush? DefaultNodeFill { get; }

    /// <summary>Theme-aware text colour for un-customised nodes; <c>null</c> if app resources aren't ready yet.</summary>
    IBrush? DefaultNodeText { get; }

    /// <summary>Raised when the active theme variant changes, so node view models can re-resolve their brushes.</summary>
    event EventHandler? ThemeChanged;

    void Toggle();
}
