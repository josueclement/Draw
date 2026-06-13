using System;
using System.Collections.Generic;

namespace Draw.App.Services;

/// <summary>Tracks recently opened files, persisted to the per-OS application-data folder.</summary>
public interface IRecentFilesService
{
    IReadOnlyList<string> Files { get; }

    event EventHandler? Changed;

    void Add(string path);

    void Remove(string path);
}
