using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Rendering;
using Draw.Diagramming.Layout;
using PhosphorIconsAvalonia;

namespace Draw.App.ViewModels;

/// <summary>A tile in the align/distribute palette: its mnemonic, label, glyph and current enabled state.</summary>
public sealed record ArrangeTile(char Letter, string Label, Geometry? Glyph, bool IsEnabled)
{
    /// <summary>The mnemonic as an uppercase string for the letter chip.</summary>
    public string LetterText => char.ToUpperInvariant(Letter).ToString();
}

/// <summary>
/// Drives the Shift+A align &amp; distribute palette: a centered overlay (same chrome as the style picker)
/// that reuses the active document's existing <see cref="DiagramDocumentViewModel.AlignCommand"/> /
/// <see cref="DiagramDocumentViewModel.DistributeCommand"/> and the relative-alignment commands. It is
/// mode-aware: with no reference set the align tiles line the selection up against its own bounding box
/// and a "Set as reference" tile is offered (which captures the selection and closes so the movers can be
/// picked on the canvas); once a reference is active the same align tiles line the movers up against it,
/// and a "Clear reference" tile is offered instead. Align/distribute picks keep the palette open so
/// several can be chained; the palette is modal, so the selection cannot change while it is open (the
/// only in-place state change is clearing the reference, which refreshes via the document's hub).
/// </summary>
public sealed class AlignmentPickerViewModel : ViewModelBase, IOverlayPalette
{
    private static readonly HashSet<string> RefreshTriggers =
    [
        nameof(DiagramDocumentViewModel.HasReference),
        nameof(DiagramDocumentViewModel.CanAlignSelection),
        nameof(DiagramDocumentViewModel.CanDistributeSelection),
        nameof(DiagramDocumentViewModel.CanSetReference),
        nameof(DiagramDocumentViewModel.CanAlignToReference),
    ];

    // Phosphor align glyphs are pure (no application/resource lookup), so they are safe to build up-front.
    private readonly Geometry _left = IconGeometry.Phosphor(Icon.align_left);
    private readonly Geometry _centerHorizontal = IconGeometry.Phosphor(Icon.align_center_horizontal);
    private readonly Geometry _right = IconGeometry.Phosphor(Icon.align_right);
    private readonly Geometry _top = IconGeometry.Phosphor(Icon.align_top);
    private readonly Geometry _centerVertical = IconGeometry.Phosphor(Icon.align_center_vertical);
    private readonly Geometry _bottom = IconGeometry.Phosphor(Icon.align_bottom);
    private readonly Geometry _setReference = IconGeometry.Phosphor(Icon.push_pin);
    private readonly Geometry _clearReference = IconGeometry.Phosphor(Icon.x);

    // The Distribute glyphs come from merged app resources (ToolIcon.*); resolved lazily on first open so
    // the constructor never depends on Application.Current / resource-merge ordering at DI time.
    private Geometry? _distributeHorizontal;
    private Geometry? _distributeVertical;
    private bool _toolGlyphsResolved;

    private DiagramDocumentViewModel? _activeDocument;
    private DiagramDocumentViewModel? _subscribed;

    public AlignmentPickerViewModel()
    {
        DismissCommand = new RelayCommand(Close);
        ReferenceTile = new ArrangeTile('s', "Set as reference", _setReference, false);
    }

    /// <summary>Click on the dim backdrop (closes the palette).</summary>
    public RelayCommand DismissCommand { get; }

    /// <summary>The six alignment tiles (left/center/right/top/middle/bottom).</summary>
    public IReadOnlyList<ArrangeTile> AlignTiles
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    /// <summary>The two distribution tiles (horizontal/vertical).</summary>
    public IReadOnlyList<ArrangeTile> DistributeTiles
    {
        get;
        private set => SetProperty(ref field, value);
    } = [];

    /// <summary>The single reference-action tile: "Set as reference" normally, "Clear reference" while one is set.</summary>
    public ArrangeTile ReferenceTile
    {
        get;
        private set => SetProperty(ref field, value);
    }

    public bool IsOpen
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Heading: whether the palette aligns the selection or the movers, plus the selection count.</summary>
    public string Title
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>True while an alignment reference is active (drives the banner and align-to-reference mode).</summary>
    public bool HasReference
    {
        get;
        private set => SetProperty(ref field, value);
    }

    /// <summary>Banner text shown while a reference is active; empty otherwise.</summary>
    public string ReferenceBanner
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    /// <summary>Points the palette at the active document (call when the active tab changes).</summary>
    public void SetActiveDocument(DiagramDocumentViewModel? document)
    {
        // The palette is modal, so the tab cannot change while it is open; keep the subscription pointed
        // at the live document defensively in case that ever changes.
        if (IsOpen && !ReferenceEquals(document, _subscribed))
        {
            Unsubscribe();
            if (document is not null)
            {
                Subscribe(document);
            }
        }

        _activeDocument = document;
        if (IsOpen)
        {
            Refresh();
        }
    }

    /// <summary>Opens the palette for the current selection.</summary>
    public void Open()
    {
        if (_activeDocument is null)
        {
            return;
        }

        Subscribe(_activeDocument);
        Refresh();
        IsOpen = true;
    }

    public void Close()
    {
        Unsubscribe();
        IsOpen = false;
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

    public bool HandleLetter(char letter)
    {
        if (!IsOpen)
        {
            return false;
        }

        char lower = char.ToLowerInvariant(letter);
        if (lower is 'l' or 'c' or 'r' or 't' or 'm' or 'b' or 'h' or 'v' or 's' or 'x')
        {
            Activate(lower);
            return true;
        }

        return false;
    }

    /// <summary>Invokes the tile bound to <paramref name="letter"/> (by key press or click), if enabled.</summary>
    public void Activate(char letter)
    {
        if (_activeDocument is null || !IsOpen)
        {
            return;
        }

        DiagramDocumentViewModel doc = _activeDocument;
        switch (char.ToLowerInvariant(letter))
        {
            case 'l': Align(doc, AlignmentMode.Left); break;
            case 'c': Align(doc, AlignmentMode.CenterHorizontal); break;
            case 'r': Align(doc, AlignmentMode.Right); break;
            case 't': Align(doc, AlignmentMode.Top); break;
            case 'm': Align(doc, AlignmentMode.CenterVertical); break;
            case 'b': Align(doc, AlignmentMode.Bottom); break;
            case 'h': Invoke(doc.DistributeCommand, DistributionMode.Horizontal); break;
            case 'v': Invoke(doc.DistributeCommand, DistributionMode.Vertical); break;
            case 's':
                // Set as reference: capture the selection, then close so the movers can be picked on the
                // canvas (the on-canvas amber banner then guides the rest of the flow).
                if (!doc.HasReference && doc.SetReferenceCommand.CanExecute(null))
                {
                    doc.SetReferenceCommand.Execute(null);
                    Close();
                }

                break;
            case 'x':
                // Clear reference is transient and keeps the palette open; the document's selection-changed
                // hub raises HasReference, so Refresh() runs via OnDocumentPropertyChanged.
                if (doc.HasReference && doc.ClearReferenceCommand.CanExecute(null))
                {
                    doc.ClearReferenceCommand.Execute(null);
                }

                break;
        }
    }

    // With a reference set the align tiles line the movers up against it; otherwise they align the
    // selection to its own bounding box. Either way the palette stays open so picks can be chained.
    private static void Align(DiagramDocumentViewModel doc, AlignmentMode mode)
    {
        if (doc.HasReference)
        {
            Invoke(doc.AlignToReferenceCommand, mode);
        }
        else
        {
            Invoke(doc.AlignCommand, mode);
        }
    }

    private static void Invoke<T>(RelayCommand<T> command, T parameter)
    {
        if (command.CanExecute(parameter))
        {
            command.Execute(parameter);
        }
    }

    private void Subscribe(DiagramDocumentViewModel document)
    {
        Unsubscribe();
        document.PropertyChanged += OnDocumentPropertyChanged;
        _subscribed = document;
    }

    private void Unsubscribe()
    {
        if (_subscribed is null)
        {
            return;
        }

        _subscribed.PropertyChanged -= OnDocumentPropertyChanged;
        _subscribed = null;
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null || RefreshTriggers.Contains(e.PropertyName))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        EnsureToolGlyphs();

        DiagramDocumentViewModel? doc = _activeDocument;
        bool hasReference = doc?.HasReference ?? false;
        int selected = doc?.SelectedNodes.Count() ?? 0;

        HasReference = hasReference;
        Title = hasReference
            ? $"Align to reference ({selected} selected)"
            : $"Align & distribute ({selected} selected)";

        if (hasReference)
        {
            int referenceCount = doc!.ReferenceNodes.Count();
            ReferenceBanner = $"{referenceCount} reference shape{(referenceCount == 1 ? "" : "s")} pinned — align the selected shapes to it.";
        }
        else
        {
            ReferenceBanner = string.Empty;
        }

        bool canAlign = hasReference ? (doc?.CanAlignToReference ?? false) : (doc?.CanAlignSelection ?? false);
        bool canDistribute = doc?.CanDistributeSelection ?? false;

        AlignTiles =
        [
            new ArrangeTile('l', "Align left", _left, canAlign),
            new ArrangeTile('c', "Align center", _centerHorizontal, canAlign),
            new ArrangeTile('r', "Align right", _right, canAlign),
            new ArrangeTile('t', "Align top", _top, canAlign),
            new ArrangeTile('m', "Align middle", _centerVertical, canAlign),
            new ArrangeTile('b', "Align bottom", _bottom, canAlign),
        ];

        DistributeTiles =
        [
            new ArrangeTile('h', "Distribute horizontally", _distributeHorizontal, canDistribute),
            new ArrangeTile('v', "Distribute vertically", _distributeVertical, canDistribute),
        ];

        ReferenceTile = hasReference
            ? new ArrangeTile('x', "Clear reference", _clearReference, true)
            : new ArrangeTile('s', "Set as reference", _setReference, doc?.CanSetReference ?? false);
    }

    private void EnsureToolGlyphs()
    {
        if (_toolGlyphsResolved)
        {
            return;
        }

        _distributeHorizontal = IconGeometry.Tool("ToolIcon.DistributeHorizontal");
        _distributeVertical = IconGeometry.Tool("ToolIcon.DistributeVertical");
        _toolGlyphsResolved = true;
    }
}
