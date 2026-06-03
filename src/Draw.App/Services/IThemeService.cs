using Avalonia;
using Avalonia.Styling;

namespace Draw.App.Services;

/// <summary>Toggles the application between light and dark Fluent themes.</summary>
public interface IThemeService
{
    bool IsDark { get; }

    void Toggle();
}

public sealed class ThemeService : IThemeService
{
    public bool IsDark => Application.Current?.ActualThemeVariant == ThemeVariant.Dark;

    public void Toggle()
    {
        if (Application.Current is { } app)
        {
            app.RequestedThemeVariant = IsDark ? ThemeVariant.Light : ThemeVariant.Dark;
        }
    }
}
