namespace Draw.App.Configuration;

/// <summary>Tunables for the keyboard-shortcut (keymap) system.</summary>
public sealed class KeymapOptions
{
    public const string SectionName = "Keymap";

    /// <summary>Idle time, in milliseconds, after a keystroke before a pending chord buffer is discarded.</summary>
    public int ChordTimeoutMs { get; set; } = 1000;

    /// <summary>How long, in milliseconds, a transient status message (e.g. "no binding") stays visible.</summary>
    public int TransientMessageMs { get; set; } = 1200;
}
