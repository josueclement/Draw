namespace Draw.App.ViewModels;

/// <summary>
/// A full-window overlay (dim backdrop + centred card) that owns the keyboard while open. The window
/// routes Esc to <see cref="Back"/> and an unmodified letter to <see cref="HandleLetter"/> instead of the
/// chord dispatcher (see <c>MainWindow.OnGlobalKeyDown</c>). The neovim-style tool palette and the
/// Shift+I / Shift+Y / Shift+H overlays all implement this, so <see cref="ShellViewModel"/> can route
/// keys to whichever one is open and keep at most one open at a time.
/// </summary>
public interface IOverlayPalette
{
    /// <summary>True while the overlay is shown.</summary>
    bool IsOpen { get; }

    /// <summary>Routes an unmodified letter key to the overlay. Returns true if it was consumed.</summary>
    bool HandleLetter(char letter);

    /// <summary>Esc semantics: step back a level or close. Returns true if it consumed the key.</summary>
    bool Back();

    /// <summary>Closes the overlay (used when another overlay opens, or on dismiss).</summary>
    void Close();
}
