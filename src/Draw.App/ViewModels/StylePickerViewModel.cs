using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Services;
using Draw.Diagramming.Mnemonics;
using Draw.Diagramming.Styling;

namespace Draw.App.ViewModels;

/// <summary>A style swatch tile in the picker: its mnemonic plus the reusable quick-palette swatch
/// view-model that carries the preview colours and the apply action.</summary>
public sealed record StyleSwatchEntry(char Letter, StyleSwatchViewModel Swatch)
{
    /// <summary>The mnemonic as an uppercase string for the letter chip.</summary>
    public string LetterText => char.ToUpperInvariant(Letter).ToString();

    public string Name => Swatch.Name;
}

/// <summary>A non-swatch action tile in the picker (Reset, No-fill): its mnemonic and label.</summary>
public sealed record StyleActionEntry(char Letter, string Name)
{
    /// <summary>The mnemonic as an uppercase string for the letter chip.</summary>
    public string LetterText => char.ToUpperInvariant(Letter).ToString();
}

/// <summary>
/// Drives the Shift+Y style picker: a centered overlay (same chrome as the tool palette) that applies a
/// style to the current selection. It reuses the quick palette's <see cref="StyleSwatchViewModel"/> swatches
/// (built from <see cref="StylePalette.Swatches"/>) plus Reset and No-fill, each with an auto-assigned
/// mnemonic. Picking one (by letter or click) applies it via the existing
/// <see cref="DiagramDocumentViewModel.ApplyStyleSwatch"/> / <see cref="DiagramDocumentViewModel.ResetStyleToDefault"/> /
/// <see cref="DiagramDocumentViewModel.ApplyNoFill"/> (one undo step, nodes and connectors) and closes.
/// </summary>
public sealed class StylePickerViewModel : ViewModelBase, IOverlayPalette
{
    private const string ResetLabel = "Reset";
    private const string NoFillLabel = "No fill";

    private readonly IThemeService _theme;
    private DiagramDocumentViewModel? _activeDocument;

    public StylePickerViewModel(IThemeService theme)
    {
        _theme = theme ?? throw new ArgumentNullException(nameof(theme));

        IReadOnlyList<StyleSwatch> swatches = StylePalette.Swatches;

        // Assign mnemonics across the whole set (swatches + Reset + No-fill) in one pass so letters never collide.
        List<string> names = swatches.Select(s => s.Name).ToList();
        names.Add(ResetLabel);
        names.Add(NoFillLabel);
        char[] letters = MnemonicAssigner.Assign(names);

        List<StyleSwatchEntry> swatchEntries = new(swatches.Count);
        for (int i = 0; i < swatches.Count; i++)
        {
            swatchEntries.Add(new StyleSwatchEntry(letters[i], new StyleSwatchViewModel(swatches[i], _theme, ApplyAndClose)));
        }

        Swatches = swatchEntries;
        ResetEntry = new StyleActionEntry(letters[swatches.Count], ResetLabel);
        NoFillEntry = new StyleActionEntry(letters[swatches.Count + 1], NoFillLabel);

        PickSwatchCommand = new RelayCommand<StyleSwatchEntry>(OnPickSwatch);
        ResetCommand = new RelayCommand(OnReset);
        NoFillCommand = new RelayCommand(OnNoFill);
        DismissCommand = new RelayCommand(Close);

        _theme.ThemeChanged += OnThemeChanged;
    }

    /// <summary>The style swatch tiles.</summary>
    public IReadOnlyList<StyleSwatchEntry> Swatches { get; }

    /// <summary>The "Reset to default style" tile.</summary>
    public StyleActionEntry ResetEntry { get; }

    /// <summary>The "No fill" tile.</summary>
    public StyleActionEntry NoFillEntry { get; }

    /// <summary>Mouse-click on a swatch tile (applies it and closes).</summary>
    public RelayCommand<StyleSwatchEntry> PickSwatchCommand { get; }

    /// <summary>Mouse-click on the Reset tile.</summary>
    public RelayCommand ResetCommand { get; }

    /// <summary>Mouse-click on the No-fill tile.</summary>
    public RelayCommand NoFillCommand { get; }

    /// <summary>Click on the dim backdrop (closes the picker).</summary>
    public RelayCommand DismissCommand { get; }

    public bool IsOpen
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Heading showing how many items the style applies to.</summary>
    public string Title
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>False when nothing is selected — all style controls (swatches, Reset, No-fill) are disabled.</summary>
    public bool CanApply
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Points the picker at the active document (call when the active tab changes).</summary>
    public void SetActiveDocument(DiagramDocumentViewModel? document) => _activeDocument = document;

    /// <summary>Opens the picker for the current selection.</summary>
    public void Open()
    {
        if (_activeDocument is null)
        {
            return;
        }

        CanApply = _activeDocument.HasSelection;
        UpdateTitle();
        IsOpen = true;
    }

    public void Close() => IsOpen = false;

    public bool Back()
    {
        if (!IsOpen)
        {
            return false;
        }

        Close();
        return true;
    }

    public bool HandleLetter(char letter)
    {
        if (!IsOpen)
        {
            return false;
        }

        if (!CanApply)
        {
            // Controls are disabled (nothing selected): swallow the key so it can't leak to the canvas.
            // Required here (not just cosmetic): the apply handlers close the picker unconditionally,
            // so without this guard a keypress would dismiss it while doing nothing.
            return true;
        }

        char lower = char.ToLowerInvariant(letter);
        foreach (StyleSwatchEntry entry in Swatches)
        {
            if (entry.Letter == lower)
            {
                OnPickSwatch(entry);
                return true;
            }
        }

        if (ResetEntry.Letter == lower)
        {
            OnReset();
            return true;
        }

        if (NoFillEntry.Letter == lower)
        {
            OnNoFill();
            return true;
        }

        return false;
    }

    private void OnPickSwatch(StyleSwatchEntry? entry)
        => entry?.Swatch.ApplyCommand.Execute(null); // ApplyCommand routes to ApplyAndClose.

    private void ApplyAndClose(StyleSwatch swatch)
    {
        _activeDocument?.ApplyStyleSwatch(swatch);
        Close();
    }

    private void OnReset()
    {
        _activeDocument?.ResetStyleToDefault();
        Close();
    }

    private void OnNoFill()
    {
        _activeDocument?.ApplyNoFill();
        Close();
    }

    private void UpdateTitle()
    {
        int count = (_activeDocument?.SelectedNodes.Count() ?? 0) + (_activeDocument?.SelectedConnectors.Count() ?? 0);
        Title = count == 1 ? "Style (1 selected)" : $"Style ({count} selected)";
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        foreach (StyleSwatchEntry entry in Swatches)
        {
            entry.Swatch.RaisePreviewChanged();
        }
    }
}
