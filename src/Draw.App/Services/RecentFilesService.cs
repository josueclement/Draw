using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Draw.App.Configuration;
using Microsoft.Extensions.Options;

namespace Draw.App.Services;

public sealed class RecentFilesService : IRecentFilesService
{
    private readonly int _maxEntries;
    private readonly string _storePath;
    private readonly List<string> _files = new();

    public RecentFilesService(IOptions<RecentFilesOptions> options)
    {
        _maxEntries = Math.Max(1, options.Value.MaxEntries);
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Draw");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "recent.json");
        Load();
    }

    public IReadOnlyList<string> Files => _files;

    public event EventHandler? Changed;

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _files.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        _files.Insert(0, path);
        while (_files.Count > _maxEntries)
        {
            _files.RemoveAt(_files.Count - 1);
        }

        Save();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Remove(string path)
    {
        if (_files.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)) > 0)
        {
            Save();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_storePath))
            {
                return;
            }

            string json = File.ReadAllText(_storePath);
            string[]? saved = JsonSerializer.Deserialize<string[]>(json);
            if (saved is not null)
            {
                _files.AddRange(saved.Take(_maxEntries));
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable recent-files store is non-fatal; start empty.
        }
    }

    private void Save()
    {
        try
        {
            File.WriteAllText(_storePath, JsonSerializer.Serialize(_files));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Persisting the recent list is best-effort.
        }
    }
}
