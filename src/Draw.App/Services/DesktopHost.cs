using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;

namespace Draw.App.Services;

/// <summary>
/// Single point for the desktop main window's platform services (storage provider, clipboard), so the
/// services that need them don't each repeat the <c>ApplicationLifetime</c> cast. Returns <c>null</c>
/// when there is no classic-desktop main window (e.g. during startup or in headless contexts).
/// </summary>
internal static class DesktopHost
{
    private static Avalonia.Controls.Window? MainWindow
        => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;

    public static IStorageProvider? StorageProvider => MainWindow?.StorageProvider;

    public static IClipboard? Clipboard => MainWindow?.Clipboard;
}
