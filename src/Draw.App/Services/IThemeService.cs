using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

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

public sealed class ThemeService : IThemeService
{
    public ThemeService()
    {
        // The service is resolved after the Avalonia app is up, so Application.Current is set;
        // guard anyway. RequestedThemeVariant="Default" follows the OS, so this also fires on
        // OS-driven changes, not just our own Toggle().
        if (Application.Current is { } app)
        {
            app.ActualThemeVariantChanged += (_, _) => ThemeChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public IBrush? DefaultNodeFill => Resolve("NodeDefaultFillBrush");

    public IBrush? DefaultNodeText => Resolve("NodeDefaultTextBrush");

    public event EventHandler? ThemeChanged;

    public void Toggle()
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = IsDark ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }

    // Resolves a theme-scoped brush for the app's current ActualThemeVariant.
    private static IBrush? Resolve(string key)
        => Application.Current is { } app
            && app.TryFindResource(key, out object? value)
            && value is IBrush brush
                ? brush
                : null;
}
