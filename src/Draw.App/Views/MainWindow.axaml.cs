using System;
using System.IO;
using System.Linq;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Carbon.Avalonia.Desktop.Controls.Ribbon;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Input;
using Draw.App.Services;
using Draw.App.ViewModels;
using Draw.Diagramming.Layout;
using Draw.Model.Connectors;
using Draw.Model.Nodes;

namespace Draw.App.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel? _shell;
    private readonly IImageExportService? _exporter;
    private readonly IFileDialogService? _fileDialogs;
    private readonly IDialogService? _dialogs;
    private readonly ChordInputDispatcher? _dispatcher;

    // Parameterless constructor for the XAML previewer/designer.
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(
        ShellViewModel shell,
        IImageExportService exporter,
        IFileDialogService fileDialogs,
        IDialogService dialogs,
        ChordInputDispatcher dispatcher)
        : this()
    {
        _shell = shell;
        _exporter = exporter;
        _fileDialogs = fileDialogs;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        DataContext = shell;
        shell.ExportImageRequested += OnExportImageRequested;
        shell.ExportSvgRequested += OnExportSvgRequested;
        shell.CopyImageRequested += OnCopyImageRequested;
        WireToolDropdowns(shell.Toolbox);
        WireAlignDropdown();
        WireToolMenus(shell.Toolbox);
        shell.ToolMenuRequested += OnToolMenuRequested;

        // Global keyboard shortcuts (single gestures + multi-key chords) come from the JSON keymap.
        // Tunnel so plain letters reach the dispatcher before a focused control consumes them; a chord
        // mid-flight is reset when the window loses focus. Text-entry surfaces are skipped (see handler).
        AddHandler(InputElement.KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
        Deactivated += OnWindowDeactivated;

        // Open on the Insert (tools) tab. This must be set here, not as a literal SelectedIndex in XAML:
        // the XAML attribute is applied while Ribbon.Tabs is still empty, so Ribbon never syncs SelectedTab
        // and its OnApplyTemplate then forces Tabs[0]. Setting it now (after InitializeComponent populated
        // Tabs) is a real 0 -> 1 change that selects Tabs[1] and survives template application.
        MainRibbon.SelectedIndex = 1;
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnWindowDeactivated(object? sender, EventArgs e) => _dispatcher?.Reset();

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // Never intercept keys while a text-entry surface has focus (renaming, the inspector, etc.).
        if (_dispatcher is null || IsTextEntryFocused())
        {
            return;
        }

        if (_dispatcher.HandleKeyDown(e))
        {
            e.Handled = true;
        }
    }

    // At tunnel time e.Source is the window, so consult the focus manager. AutoCompleteBox/ComboBox host
    // an inner TextBox as the focused element, so an ancestor walk catches those too.
    private bool IsTextEntryFocused()
    {
        IInputElement? focused = FocusManager?.GetFocusedElement();
        if (focused is TextBox or AutoCompleteBox)
        {
            return true;
        }

        return focused is Visual visual
            && (visual.FindAncestorOfType<TextBox>() is not null
                || visual.FindAncestorOfType<AutoCompleteBox>() is not null
                || visual.FindAncestorOfType<ComboBox>() is not null);
    }

    // Carbon's RibbonMenuItem is a plain AvaloniaObject (no DataContext), so its Command cannot be data-bound
    // in XAML. Each dropdown's items share one "arm tool" command; assign it here. The per-item kind is set
    // via CommandParameter in XAML.
    private void WireToolDropdowns(ToolboxViewModel toolbox)
    {
        WireDropdown(ShapesDropDown, toolbox.SelectShapeToolCommand);
        WireDropdown(ConnectorsDropDown, toolbox.SelectConnectorToolCommand);
        WireDropdown(ClassDropDown, toolbox.SelectClassNodeToolCommand);
        WireDropdown(UseCaseDropDown, toolbox.SelectUseCaseToolCommand);
    }

    // Carbon doesn't close the popup when a RibbonMenuItem is clicked (it dismisses only on an outside
    // click), so the first canvas click would otherwise be spent dismissing the popup. Wrap each arm
    // command to close the dropdown on selection; the per-item kind still arrives via the XAML
    // CommandParameter. Closing also ends the open-popup state that made the button re-measure.
    private static void WireDropdown<TKind>(RibbonDropDownButton dropdown, RelayCommand<TKind> arm)
    {
        RelayCommand<TKind> wrapper = new(kind =>
        {
            arm.Execute(kind);
            dropdown.IsDropDownOpen = false;
        });

        foreach (RibbonMenuItem item in dropdown.Items)
        {
            item.Command = wrapper;
        }
    }

    // The Align dropdown is one-shot (not an armed tool), so its shared command resolves the active
    // document at click time — that way it follows tab switches without re-wiring. Each item's
    // AlignmentMode arrives via the XAML CommandParameter; enable/disable is handled by the button's
    // IsEnabled binding to ActiveDocument.CanAlignSelection.
    private void WireAlignDropdown()
    {
        RelayCommand<AlignmentMode> align = new(mode =>
        {
            if (mode is { } m)
            {
                _shell?.ActiveDocument?.AlignSelected(m);
            }

            AlignDropDown.IsDropDownOpen = false;
        });

        foreach (RibbonMenuItem item in AlignDropDown.Items)
        {
            item.Command = align;
        }
    }

    // The Shift+S / Shift+C tool menus are declared statically in XAML (icons + access keys); assign each
    // leaf item's arm command here by its CommandParameter type, mirroring WireToolDropdowns. Selecting a
    // ContextMenu item closes the menu automatically, so the raw arm command can be used directly.
    private void WireToolMenus(ToolboxViewModel toolbox)
    {
        WireToolMenu((ContextMenu)this.FindResource("ShapesToolMenu")!, toolbox);
        WireToolMenu((ContextMenu)this.FindResource("ConnectorsToolMenu")!, toolbox);
    }

    private static void WireToolMenu(ContextMenu menu, ToolboxViewModel toolbox)
    {
        foreach (object? top in menu.Items)
        {
            if (top is not MenuItem category)
            {
                continue;
            }

            foreach (object? leaf in category.Items)
            {
                if (leaf is MenuItem item)
                {
                    item.Command = ArmCommandFor(item, toolbox);
                }
            }
        }
    }

    private static ICommand? ArmCommandFor(MenuItem item, ToolboxViewModel toolbox) => item.CommandParameter switch
    {
        ShapeKind => toolbox.SelectShapeToolCommand,
        RelationshipKind => toolbox.SelectConnectorToolCommand,
        ClassNodeKind => toolbox.SelectClassNodeToolCommand,
        UseCaseNodeKind => toolbox.SelectUseCaseToolCommand,
        _ when (item.Tag as string) == "entity" => toolbox.SelectEntityToolCommand,
        _ => item.Command,
    };

    private void OnToolMenuRequested(object? sender, ToolMenuFamily family)
    {
        string key = family == ToolMenuFamily.Shapes ? "ShapesToolMenu" : "ConnectorsToolMenu";
        if (this.FindResource(key) is ContextMenu menu)
        {
            ActiveDiagramView()?.OpenToolMenu(menu);
        }
    }

    private DiagramView? ActiveDiagramView()
        => _shell is null
            ? null
            : this.GetVisualDescendants()
                .OfType<DiagramView>()
                .FirstOrDefault(v => ReferenceEquals(v.DataContext, _shell.ActiveDocument));

    private async void OnExportImageRequested(object? sender, EventArgs e)
    {
        if (_exporter is null || _fileDialogs is null)
        {
            return;
        }

        DiagramView? view = ActiveDiagramView();
        if (view is null)
        {
            return;
        }

        string? path = await _fileDialogs.PickSaveImageAsync("diagram");
        if (path is null)
        {
            return;
        }

        // Render after the picker so the synchronous grid-free/identity-transform swap never reaches the
        // screen. Null means the diagram is empty — nothing to export.
        using RenderTargetBitmap? bitmap = view.RenderContentBitmap();
        if (bitmap is null)
        {
            return;
        }

        try
        {
            await _exporter.SaveAsync(bitmap, path, FormatFromPath(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_dialogs is not null)
            {
                await _dialogs.ShowErrorAsync("Export failed", ex.Message);
            }
        }
    }

    private async void OnExportSvgRequested(object? sender, EventArgs e)
    {
        if (_fileDialogs is null)
        {
            return;
        }

        DiagramView? view = ActiveDiagramView();
        if (view is null)
        {
            return;
        }

        string? path = await _fileDialogs.PickSaveSvgAsync("diagram");
        if (path is null)
        {
            return;
        }

        string? svg = view.BuildSvgDocument();
        if (svg is null)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(path, svg);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (_dialogs is not null)
            {
                await _dialogs.ShowErrorAsync("Export failed", ex.Message);
            }
        }
    }

    private async void OnCopyImageRequested(object? sender, EventArgs e)
    {
        if (_exporter is null)
        {
            return;
        }

        DiagramView? view = ActiveDiagramView();
        if (view is null)
        {
            return;
        }

        using RenderTargetBitmap? bitmap = view.RenderContentBitmap();
        if (bitmap is null)
        {
            return;
        }

        try
        {
            await _exporter.CopyToClipboardAsync(bitmap);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            if (_dialogs is not null)
            {
                await _dialogs.ShowErrorAsync("Copy failed", ex.Message);
            }
        }
    }

    private static ImageExportFormat FormatFromPath(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" ? ImageExportFormat.Jpeg : ImageExportFormat.Png;
    }
}
