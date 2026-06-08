using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace Draw.App.Input;

/// <summary>Loads the keyboard keymap: baked-in defaults merged with the user's override file.</summary>
public interface IKeymapService
{
    /// <summary>The merged, parsed bindings (defaults overridden by the user file).</summary>
    IReadOnlyList<ParsedBinding> Bindings { get; }
}

/// <summary>
/// Reads the keymap from <c>%APPDATA%/Draw/keymap.json</c> (per-OS application data, mirroring
/// <see cref="Services.RecentFilesService"/>), merging it over the baked-in defaults. A missing or
/// corrupt user file is non-fatal — the defaults are used. A commented example file is written on first run.
/// </summary>
public sealed class KeymapService : IKeymapService
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly string _userPath;
    private readonly string _examplePath;
    private readonly List<ParsedBinding> _bindings = new();

    public KeymapService()
    {
        string dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Draw");
        Directory.CreateDirectory(dir);
        _userPath = Path.Combine(dir, "keymap.json");
        _examplePath = Path.Combine(dir, "keymap.example.json");

        WriteExampleIfMissing();
        Load();
    }

    public IReadOnlyList<ParsedBinding> Bindings => _bindings;

    private void Load()
    {
        // Start from the defaults, keyed by key sequence so user entries can override or unbind them.
        Dictionary<IReadOnlyList<KeyStroke>, ParsedBinding> merged = new(KeyStrokeSequenceComparer.Instance);
        foreach (ParsedBinding binding in ParseBindings(DefaultKeymap.DefaultJson))
        {
            merged[binding.Strokes] = binding;
        }

        KeymapFile? user = ReadUserFile();
        if (user?.Bindings is not null)
        {
            foreach (KeymapBinding raw in user.Bindings)
            {
                IReadOnlyList<KeyStroke>? strokes = KeyGestureParser.Parse(raw.Keys);
                if (strokes is null)
                {
                    Debug.WriteLine($"[keymap] Ignoring user binding with unparseable keys: '{raw.Keys}'.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(raw.Action))
                {
                    merged.Remove(strokes); // Empty action unbinds the default with the same keys.
                    continue;
                }

                merged[strokes] = new ParsedBinding(strokes, raw.Action);
            }
        }

        _bindings.Clear();
        _bindings.AddRange(merged.Values);
    }

    private KeymapFile? ReadUserFile()
    {
        try
        {
            if (!File.Exists(_userPath))
            {
                return null;
            }

            string json = File.ReadAllText(_userPath);
            return JsonSerializer.Deserialize<KeymapFile>(json, s_json);
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A corrupt or unreadable user keymap is non-fatal; fall back to the defaults already loaded.
            Debug.WriteLine($"[keymap] Failed to read user keymap; using defaults. {ex.Message}");
            return null;
        }
    }

    private void WriteExampleIfMissing()
    {
        try
        {
            if (!File.Exists(_examplePath))
            {
                File.WriteAllText(_examplePath, DefaultKeymap.ExampleJsonc);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Writing the example file is best-effort.
        }
    }

    private static IEnumerable<ParsedBinding> ParseBindings(string json)
    {
        KeymapFile? file = JsonSerializer.Deserialize<KeymapFile>(json, s_json);
        if (file?.Bindings is null)
        {
            yield break;
        }

        foreach (KeymapBinding raw in file.Bindings)
        {
            IReadOnlyList<KeyStroke>? strokes = KeyGestureParser.Parse(raw.Keys);
            if (strokes is not null && !string.IsNullOrWhiteSpace(raw.Action))
            {
                yield return new ParsedBinding(strokes, raw.Action);
            }
        }
    }
}
