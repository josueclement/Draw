using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Rendering;
using Draw.Diagramming.Mnemonics;

namespace Draw.App.ViewModels;

/// <summary>Which screen the tool palette is showing: the category list or one category's items.</summary>
public enum PaletteScreen
{
    Categories,
    Items,
}

/// <summary>A category on the first screen, with its auto-assigned mnemonic and the catalog it drills into.</summary>
public sealed record PaletteCategoryEntry(char Letter, string Name, ToolCatalogCategory Category)
{
    /// <summary>The mnemonic as an uppercase string for the letter chip.</summary>
    public string LetterText => char.ToUpperInvariant(Letter).ToString();
}

/// <summary>An item on the second screen, with its mnemonic, resolved icon glyph, and the tool it arms.</summary>
public sealed record PaletteItemEntry(char Letter, string Name, Geometry Icon, ToolArm Arm)
{
    /// <summary>The mnemonic as an uppercase string for the letter chip.</summary>
    public string LetterText => char.ToUpperInvariant(Letter).ToString();
}

/// <summary>
/// Drives the neovim-style tool palette: a centered overlay that replaces the old Shift+S / Shift+C
/// context menus. It is a two-screen drill-down — categories, then the chosen category's items — with a
/// mnemonic letter on every entry (<see cref="MnemonicAssigner"/>, re-derived per screen). Choosing an
/// item arms the matching toolbox tool (the existing place/drag flow is unchanged) and closes the
/// palette. The view-model holds only <see cref="Geometry"/> from Avalonia.Media — no controls, no input
/// types — so the keyboard is translated to its semantic <see cref="Back"/> / <see cref="HandleLetter"/>
/// methods by the window.
/// </summary>
public sealed class ToolPaletteViewModel : ViewModelBase, IOverlayPalette
{
    private readonly ToolboxViewModel _toolbox;
    private readonly Func<ToolMenuFamily, ToolCatalogFamily> _catalogFor;

    public ToolPaletteViewModel(ToolboxViewModel toolbox)
        : this(toolbox, ToolPaletteCatalog.For)
    {
    }

    /// <summary>Test seam: inject a catalog provider so the view-model never touches Application.Current.</summary>
    public ToolPaletteViewModel(ToolboxViewModel toolbox, Func<ToolMenuFamily, ToolCatalogFamily> catalogFor)
    {
        _toolbox = toolbox;
        _catalogFor = catalogFor;
        SelectCategoryCommand = new RelayCommand<PaletteCategoryEntry>(OnSelectCategory);
        SelectItemCommand = new RelayCommand<PaletteItemEntry>(OnSelectItem);
        DismissCommand = new RelayCommand(Close);
    }

    /// <summary>Mouse-click on a category tile (drills into its items).</summary>
    public RelayCommand<PaletteCategoryEntry> SelectCategoryCommand { get; }

    /// <summary>Mouse-click on an item tile (arms the tool and closes).</summary>
    public RelayCommand<PaletteItemEntry> SelectItemCommand { get; }

    /// <summary>Click on the dim backdrop (closes the palette).</summary>
    public RelayCommand DismissCommand { get; }

    public ObservableCollection<PaletteCategoryEntry> Categories { get; } = new();

    public ObservableCollection<PaletteItemEntry> Items { get; } = new();

    public bool IsOpen
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsCategoriesScreen));
                OnPropertyChanged(nameof(IsItemsScreen));
            }
        }
    }

    public PaletteScreen Screen
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                OnPropertyChanged(nameof(IsCategoriesScreen));
                OnPropertyChanged(nameof(IsItemsScreen));
            }
        }
    }

    /// <summary>Breadcrumb heading: the family name ("Shapes"/"Connectors") or the drilled-into category name.</summary>
    public string Title
    {
        get;
        private set => SetProperty(ref field, value);
    } = string.Empty;

    public bool IsCategoriesScreen => IsOpen && Screen == PaletteScreen.Categories;

    public bool IsItemsScreen => IsOpen && Screen == PaletteScreen.Items;

    /// <summary>Opens the palette on the category screen for a family — or switches to it if already open.</summary>
    public void Open(ToolMenuFamily family)
    {
        ToolCatalogFamily catalog = _catalogFor(family);
        Title = family == ToolMenuFamily.Connectors ? "Connectors" : "Shapes";

        Items.Clear();
        Categories.Clear();
        char[] letters = MnemonicAssigner.Assign(CategoryNames(catalog.Categories));
        for (int i = 0; i < catalog.Categories.Count; i++)
        {
            ToolCatalogCategory category = catalog.Categories[i];
            Categories.Add(new PaletteCategoryEntry(letters[i], category.Name, category));
        }

        Screen = PaletteScreen.Categories;
        IsOpen = true;
    }

    /// <summary>Closes the palette and clears its transient state.</summary>
    public void Close()
    {
        IsOpen = false;
        Screen = PaletteScreen.Categories;
        Items.Clear();
        Categories.Clear();
        Title = string.Empty;
    }

    /// <summary>Esc semantics: items → categories; categories → closed. Returns true when it consumed the key.</summary>
    public bool Back()
    {
        if (!IsOpen)
        {
            return false;
        }

        if (Screen == PaletteScreen.Items)
        {
            Items.Clear();
            Screen = PaletteScreen.Categories;
            return true;
        }

        Close();
        return true;
    }

    /// <summary>Routes a letter to the current screen: drill into a category, or arm an item. True if it matched.</summary>
    public bool HandleLetter(char letter)
    {
        if (!IsOpen)
        {
            return false;
        }

        char lower = char.ToLowerInvariant(letter);
        if (Screen == PaletteScreen.Categories)
        {
            foreach (PaletteCategoryEntry entry in Categories)
            {
                if (entry.Letter == lower)
                {
                    OnSelectCategory(entry);
                    return true;
                }
            }

            return false;
        }

        foreach (PaletteItemEntry entry in Items)
        {
            if (entry.Letter == lower)
            {
                OnSelectItem(entry);
                return true;
            }
        }

        return false;
    }

    private void OnSelectCategory(PaletteCategoryEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        Items.Clear();
        char[] letters = MnemonicAssigner.Assign(ItemNames(entry.Category.Items));
        for (int i = 0; i < entry.Category.Items.Count; i++)
        {
            ToolCatalogEntry item = entry.Category.Items[i];
            Items.Add(new PaletteItemEntry(letters[i], item.Name, item.Icon, item.Arm));
        }

        Title = entry.Name;
        Screen = PaletteScreen.Items;
    }

    private void OnSelectItem(PaletteItemEntry? entry)
    {
        if (entry is null)
        {
            return;
        }

        Arm(entry.Arm);
        Close();
    }

    // Mirrors the retired ArmCommandFor: route to the same Select*ToolCommand the ribbon dropdowns use,
    // so the armed-tool state and the canvas place/drag flow behave exactly as before.
    private void Arm(ToolArm arm)
    {
        switch (arm)
        {
            case ShapeArm shape:
                _toolbox.SelectShapeToolCommand.Execute(shape.Kind);
                break;
            case ConnectorArm connector:
                _toolbox.SelectConnectorToolCommand.Execute(connector.Kind);
                break;
            case ClassNodeArm classNode:
                _toolbox.SelectClassNodeToolCommand.Execute(classNode.Kind);
                break;
            case UseCaseArm useCase:
                _toolbox.SelectUseCaseToolCommand.Execute(useCase.Kind);
                break;
            case UmlArm uml:
                _toolbox.SelectUmlToolCommand.Execute(uml.Kind);
                break;
            case EntityArm:
                _toolbox.SelectEntityToolCommand.Execute(null);
                break;
        }
    }

    private static IReadOnlyList<string> CategoryNames(IReadOnlyList<ToolCatalogCategory> categories)
    {
        string[] names = new string[categories.Count];
        for (int i = 0; i < categories.Count; i++)
        {
            names[i] = categories[i].Name;
        }

        return names;
    }

    private static IReadOnlyList<string> ItemNames(IReadOnlyList<ToolCatalogEntry> items)
    {
        string[] names = new string[items.Count];
        for (int i = 0; i < items.Count; i++)
        {
            names[i] = items[i].Name;
        }

        return names;
    }
}
