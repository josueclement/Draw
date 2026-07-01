using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;

namespace Draw.App.ViewModels;

/// <summary>A row in the command palette: the vim syntax (e.g. <c>:w</c>) and a one-line description.</summary>
public sealed record CommandPaletteEntry(string Syntax, string Description);

/// <summary>
/// Drives the vim <c>:</c> command palette: a centered overlay (same chrome as the other palettes) with a
/// focused input prefilled <c>:</c> and a live-filtered list of the commands the editor understands
/// (<c>:w</c> / <c>:q</c> / <c>:wq</c> / <c>:qa</c>, with a trailing <c>!</c> to force). The list is for
/// discovery only — a run still goes through <see cref="Input.VimExCommand.Parse"/>. Because the view hosts a
/// focused <c>TextBox</c>, keystrokes never reach the window's overlay letter/Esc routing (the focused-text
/// guard suppresses it); input flows through the box, and <see cref="Run"/> hands the typed text back to the
/// window (via <see cref="RunRequested"/>), which owns execution because <c>:qa</c> must close the window.
/// </summary>
public sealed class CommandPaletteViewModel : ViewModelBase, IOverlayPalette
{
    public CommandPaletteViewModel()
    {
        Entries =
        [
            new CommandPaletteEntry(":w", "Save the active document"),
            new CommandPaletteEntry(":q", "Close the active tab (:q! discards changes)"),
            new CommandPaletteEntry(":wq", "Save, then close the tab"),
            new CommandPaletteEntry(":qa", "Quit the app (:qa! discards all)"),
        ];

        RunCommand = new RelayCommand<CommandPaletteEntry>(OnRun);
        DismissCommand = new RelayCommand(Close);
        RebuildFilter();
    }

    /// <summary>Raised when the user commits a command (Enter or a row click); the window executes it.</summary>
    public event Action<string>? RunRequested;

    /// <summary>Every command the palette knows about (immutable).</summary>
    public IReadOnlyList<CommandPaletteEntry> Entries { get; }

    /// <summary>The subset of <see cref="Entries"/> matching the current <see cref="Text"/>.</summary>
    public ObservableCollection<CommandPaletteEntry> FilteredEntries { get; } = new();

    /// <summary>Click on a command row (runs that command).</summary>
    public RelayCommand<CommandPaletteEntry> RunCommand { get; }

    /// <summary>Click on the dim backdrop (closes the palette).</summary>
    public RelayCommand DismissCommand { get; }

    public bool IsOpen
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>The text being typed, including the leading <c>:</c>. Editing it re-filters the list.</summary>
    public string Text
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                RebuildFilter();
            }
        }
    } = string.Empty;

    /// <summary>Opens the palette, prefilled with the leading <c>:</c> and the full command list.</summary>
    public void Open()
    {
        Text = ":";
        IsOpen = true;
    }

    public void Close()
    {
        IsOpen = false;
        Text = string.Empty;
    }

    public bool Back()
    {
        if (!IsOpen)
        {
            return false;
        }

        Close();
        return true;
    }

    // Input arrives through the focused TextBox, not this route; swallow while open so a stray letter
    // (e.g. before focus lands) can never leak to the canvas.
    public bool HandleLetter(char letter) => IsOpen;

    /// <summary>Commits the given command text for execution (does not close — the window's handler does).</summary>
    public void Run(string text) => RunRequested?.Invoke(text);

    private void OnRun(CommandPaletteEntry? entry)
    {
        if (entry is not null)
        {
            Run(entry.Syntax);
        }
    }

    // Prefix-match the typed command word (leading ':' and trailing '!' stripped) against each entry, so
    // ':' shows all, ':w' shows :w/:wq, ':q' shows :q/:qa. Empty input shows everything.
    private void RebuildFilter()
    {
        string typed = Text.TrimStart(':').Trim().TrimEnd('!').Trim().ToLowerInvariant();
        FilteredEntries.Clear();
        foreach (CommandPaletteEntry entry in Entries)
        {
            string word = entry.Syntax.TrimStart(':');
            if (word.StartsWith(typed, StringComparison.Ordinal))
            {
                FilteredEntries.Add(entry);
            }
        }
    }
}
