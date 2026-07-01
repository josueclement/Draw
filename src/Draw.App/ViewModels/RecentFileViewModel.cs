namespace Draw.App.ViewModels;

/// <summary>One row in the empty-state "Recent" list: a recent file path split into its display
/// parts. Immutable — <see cref="ShellViewModel.RefreshRecentFiles"/> rebuilds the whole collection
/// (re-evaluating <see cref="Exists"/>) whenever the recent-files list changes.</summary>
public sealed class RecentFileViewModel
{
    public RecentFileViewModel(string path, bool exists)
    {
        Path = path;
        FileName = System.IO.Path.GetFileName(path);
        Directory = System.IO.Path.GetDirectoryName(path) ?? string.Empty;
        Exists = exists;
    }

    /// <summary>The full path — used as the open/remove command parameter and the row tooltip.</summary>
    public string Path { get; }

    /// <summary>The file name shown as the primary label.</summary>
    public string FileName { get; }

    /// <summary>The containing directory shown dimmed under the file name.</summary>
    public string Directory { get; }

    /// <summary>Whether the file still exists on disk; false rows are shown greyed and can't be opened.</summary>
    public bool Exists { get; }
}
