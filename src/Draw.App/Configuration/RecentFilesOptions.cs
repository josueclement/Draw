namespace Draw.App.Configuration;

/// <summary>Configuration for the recent-files list.</summary>
public sealed class RecentFilesOptions
{
    public const string SectionName = "RecentFiles";

    /// <summary>Maximum number of recently opened files to remember.</summary>
    public int MaxEntries { get; set; } = 10;
}
