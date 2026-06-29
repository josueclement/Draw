using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Rendering;
using Draw.Diagramming.Mnemonics;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>One marker tile in the icon palette: its mnemonic, glyph, colour and label, plus a checked
/// state that reflects whether every selected node currently carries the marker.</summary>
public sealed class IconPaletteEntry : ViewModelBase
{
    public IconPaletteEntry(char letter, NodeMarkerVisual visual)
    {
        Letter = letter;
        Marker = visual.Marker;
        Icon = visual.Icon;
        Brush = visual.Brush;
        Label = visual.Label;
    }

    public char Letter { get; }

    /// <summary>The mnemonic as an uppercase string for the letter chip.</summary>
    public string LetterText => char.ToUpperInvariant(Letter).ToString();

    public NodeMarker Marker { get; }

    public Geometry Icon { get; }

    public IBrush Brush { get; }

    public string Label { get; }

    /// <summary>True when the whole selection carries this marker (drives the checkmark).</summary>
    public bool IsActive
    {
        get;
        set => SetProperty(ref field, value);
    }
}

/// <summary>
/// Drives the Shift+I icon palette: a centered overlay (same chrome as the tool palette) that toggles
/// status-marker badges (<see cref="NodeMarker"/>) on the selected nodes. It is a flat, multi-toggle grid
/// — selecting a tile (by letter or click) toggles that marker across the whole selection via the
/// existing <see cref="DiagramDocumentViewModel.ToggleNodeMarker"/> (one undo step) and the palette stays
/// open so several can be set; Esc closes. Marker glyphs/colours/labels and their display order are
/// reused from <see cref="NodeMarkerVisuals"/>; only Avalonia.Media value types reach this view model.
/// </summary>
public sealed class IconPaletteViewModel : ViewModelBase, IOverlayPalette
{
    private DiagramDocumentViewModel? _activeDocument;

    public IconPaletteViewModel()
    {
        IReadOnlyList<NodeMarker> order = NodeMarkerVisuals.Order;
        char[] letters = MnemonicAssigner.Assign(order.Select(m => NodeMarkerVisuals.For(m).Label).ToList());
        List<IconPaletteEntry> entries = new(order.Count);
        for (int i = 0; i < order.Count; i++)
        {
            entries.Add(new IconPaletteEntry(letters[i], NodeMarkerVisuals.For(order[i])));
        }

        Entries = entries;
        ToggleCommand = new RelayCommand<IconPaletteEntry>(OnToggle);
        DismissCommand = new RelayCommand(Close);
    }

    /// <summary>The marker tiles, in <see cref="NodeMarkerVisuals.Order"/>.</summary>
    public IReadOnlyList<IconPaletteEntry> Entries { get; }

    /// <summary>Mouse-click on a tile (toggles its marker on the selection).</summary>
    public RelayCommand<IconPaletteEntry> ToggleCommand { get; }

    /// <summary>Click on the dim backdrop (closes the palette).</summary>
    public RelayCommand DismissCommand { get; }

    public bool IsOpen
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Heading showing how many nodes the toggles apply to.</summary>
    public string Title
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Points the palette at the active document (call when the active tab changes).</summary>
    public void SetActiveDocument(DiagramDocumentViewModel? document) => _activeDocument = document;

    /// <summary>Opens the palette, syncing the checkmarks to the current selection.</summary>
    public void Open()
    {
        if (_activeDocument is null)
        {
            return;
        }

        RefreshActive();
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

        char lower = char.ToLowerInvariant(letter);
        foreach (IconPaletteEntry entry in Entries)
        {
            if (entry.Letter == lower)
            {
                OnToggle(entry);
                return true;
            }
        }

        return false;
    }

    private void OnToggle(IconPaletteEntry? entry)
    {
        if (entry is null || _activeDocument is null)
        {
            return;
        }

        _activeDocument.ToggleNodeMarker(entry.Marker);
        RefreshActive();
    }

    private void RefreshActive()
    {
        foreach (IconPaletteEntry entry in Entries)
        {
            entry.IsActive = _activeDocument?.SelectionHasMarker(entry.Marker) ?? false;
        }
    }

    private void UpdateTitle()
    {
        int count = _activeDocument?.SelectedNodes.Count() ?? 0;
        Title = count == 1 ? "Icons (1 node selected)" : $"Icons ({count} nodes selected)";
    }
}
