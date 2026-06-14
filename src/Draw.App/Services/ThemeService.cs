using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;

namespace Draw.App.Services;

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
            && app.TryFindResource(key, app.ActualThemeVariant, out object? value)
            && value is IBrush brush
                ? brush
                : null;
}
