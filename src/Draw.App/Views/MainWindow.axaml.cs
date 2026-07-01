using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Carbon.Avalonia.Desktop.Controls.Ribbon;
using IContentDialogService = Carbon.Avalonia.Desktop.Services.IContentDialogService;
using CommunityToolkit.Mvvm.Input;
using Draw.App.Input;
using Draw.App.Services;
using Draw.App.ViewModels;
using Draw.Diagramming.Layout;

namespace Draw.App.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel? _shell;
    private readonly IImageExportService? _exporter;
    private readonly IFileDialogService? _fileDialogs;
    private readonly IDialogService? _dialogs;
    private readonly ChordInputDispatcher? _dispatcher;

    // Set once the user has confirmed (or there was nothing to save), so the re-entrant Close() passes through.
    private bool _forceClose;

    // Inspector collapse: width of the re-open strip, and the last expanded width restored on reopen.
    private const double InspectorStripWidth = 32;
    private double _inspectorWidth = 520;

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
        IContentDialogService contentDialog,
        ChordInputDispatcher dispatcher)
        : this()
    {
        _shell = shell;
        _exporter = exporter;
        _fileDialogs = fileDialogs;
        _dialogs = dialogs;
        _dispatcher = dispatcher;
        DataContext = shell;

        // The overlay ContentDialog declared in XAML is the single host the service drives.
        contentDialog.RegisterHost(DialogHost);
        shell.ExportImageRequested += OnExportImageRequested;
        shell.ExportSvgRequested += OnExportSvgRequested;
        shell.CopyImageRequested += OnCopyImageRequested;
        // The ':' palette hosts a focused input, so it runs commands by handing the typed text back here
        // (execution needs the window — :qa closes it); closing it returns keyboard focus to the canvas.
        shell.CommandPalette.RunRequested += OnCommandPaletteRun;
        shell.CommandPalette.PropertyChanged += OnCommandPalettePropertyChanged;
        WireToolDropdowns(shell.Toolbox);
        WireAlignmentDropdown(AlignDropDown, m => _shell?.ActiveDocument?.AlignSelected(m));
        WireAlignmentDropdown(AlignToReferenceDropDown, m => _shell?.ActiveDocument?.AlignSelectedToReference(m));
        WireOrderDropdown(OrderDropDown, op => _shell?.ActiveDocument?.ReorderSelected(op));
        shell.PropertyChanged += OnShellPropertyChanged;

        // Match the inspector column to the shell's initial IsInspectorOpen (closed by default): the XAML
        // column width is the open width, so without this an initially-closed inspector would hide its
        // content yet leave the column at full width. ActualWidth is 0 pre-layout, so the closed branch
        // keeps the remembered reopen width rather than overwriting it.
        ApplyInspectorState();

        // Global keyboard shortcuts (single gestures + multi-key chords) come from the JSON keymap.
        // Tunnel so plain letters reach the dispatcher before a focused control consumes them; a chord
        // mid-flight is reset when the window loses focus. Text-entry surfaces are skipped (see handler).
        AddHandler(InputElement.KeyDownEvent, OnGlobalKeyDown, RoutingStrategies.Tunnel);
        // The vim ':' is matched on the typed character (layout-independent) rather than a physical key,
        // since ':' sits on different keys across keyboard layouts.
        AddHandler(InputElement.TextInputEvent, OnGlobalTextInput, RoutingStrategies.Tunnel);
        Deactivated += OnWindowDeactivated;

        // Open on the Insert (tools) tab. This must be set here, not as a literal SelectedIndex in XAML:
        // the XAML attribute is applied while Ribbon.Tabs is still empty, so Ribbon never syncs SelectedTab
        // and its OnApplyTemplate then forces Tabs[0]. Setting it now (after InitializeComponent populated
        // Tabs) is a real 0 -> 1 change that selects Tabs[1] and survives template application.
        MainRibbon.SelectedIndex = 1;
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.IsInspectorOpen))
        {
            ApplyInspectorState();
        }
    }

    // The inspector column can't both stay splitter-resizable and collapse to a strip in pure XAML (a
    // GridSplitter won't size an Auto column, and a fixed-pixel column won't shrink when its child hides),
    // so drive the width here. Reopening restores the width the user last dragged to.
    private void ApplyInspectorState()
    {
        ColumnDefinition column = EditorGrid.ColumnDefinitions[2];
        if (_shell?.IsInspectorOpen ?? false)
        {
            column.MinWidth = 220;
            column.Width = new GridLength(_inspectorWidth);
        }
        else
        {
            if (column.ActualWidth > InspectorStripWidth)
            {
                _inspectorWidth = column.ActualWidth;
            }

            column.MinWidth = 0;
            column.Width = new GridLength(InspectorStripWidth);
        }
    }

    // Closing the window (X button, File ▸ Exit, OS quit) discards every open document, so guard it the
    // same way a tab close is guarded. Closing is synchronous; the dialog is async — so cancel the close
    // synchronously, run the prompts, then re-issue Close() once the user has committed.
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_forceClose || _shell is null || !_shell.HasUnsavedChanges)
        {
            return;
        }

        e.Cancel = true;
        _ = ConfirmAndCloseAsync();
    }

    private async Task ConfirmAndCloseAsync()
    {
        if (_shell is not null && await _shell.TryCloseAllAsync())
        {
            _forceClose = true;
            Close();
        }
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => _dispatcher?.Reset();

    private void OnGlobalKeyDown(object? sender, KeyEventArgs e)
    {
        // Never intercept keys while a text-entry surface has focus (renaming, the inspector, etc.).
        if (_dispatcher is null || IsTextEntryFocused())
        {
            return;
        }

        // While any overlay is open (tool palette / icons / styles / help) it owns the keyboard: Esc backs
        // out one level (then closes) and an unmodified letter is routed to the overlay (drill/arm/toggle).
        // Modified chords still fall through to the dispatcher, so Shift+S / Shift+C / Shift+I / Shift+Y /
        // Shift+H re-open or switch overlay via the normal action path, and a stray plain letter never
        // starts a chord behind the overlay.
        if (_shell?.ActiveOverlay is { IsOpen: true } overlay)
        {
            if (e.Key == Key.Escape)
            {
                overlay.Back();
                e.Handled = true;
                return;
            }

            if (e.KeyModifiers == KeyModifiers.None && e.Key is >= Key.A and <= Key.Z)
            {
                overlay.HandleLetter((char)('a' + (e.Key - Key.A)));
                e.Handled = true;
                return;
            }
        }

        // Escape also clears any alignment reference on the active document (the keymap still gets the
        // key — Escape arms the select tool as before).
        if (e.Key == Key.Escape && _shell?.ActiveDocument is { HasReference: true } doc)
        {
            doc.ClearReference();
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

    // --- Vim ':' command line ----------------------------------------------------------------------

    // Opens the palette when ':' is typed outside any text field / overlay. Matching the character
    // (not a physical key) keeps it working regardless of where ':' sits on the user's keyboard layout.
    private void OnGlobalTextInput(object? sender, TextInputEventArgs e)
    {
        if (_shell is null || IsTextEntryFocused() || _shell.CommandPalette.IsOpen)
        {
            return;
        }

        if (e.Text == ":" && _shell.ActiveOverlay is not { IsOpen: true })
        {
            BeginCommandLine();
            e.Handled = true;
        }
    }

    private void BeginCommandLine()
    {
        if (_shell is null)
        {
            return;
        }

        _shell.OpenCommandPalette();
        // The overlay was just made visible; defer focus until after the layout pass so it can take focus.
        Dispatcher.UIThread.Post(CommandPaletteOverlay.FocusInput);
    }

    // Enter / a row click in the palette hands the text here to run (the palette can't own execution
    // because :qa closes the window). Close first so the text survives the clear, then execute.
    private void OnCommandPaletteRun(string text)
    {
        _shell?.CommandPalette.Close();
        _ = ExecuteCommandLineAsync(text);
    }

    // When the palette closes (run / Esc / backdrop), return keyboard focus to the canvas.
    private void OnCommandPalettePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CommandPaletteViewModel.IsOpen) && _shell is { CommandPalette.IsOpen: false })
        {
            this.FindDescendantOfType<DiagramView>()?.Focus();
        }
    }

    private async Task ExecuteCommandLineAsync(string text)
    {
        if (_shell is null)
        {
            return;
        }

        if (VimExCommand.Parse(text) is not { } command)
        {
            _dispatcher?.Flash($"Not an editor command: {text.Trim()}");
            return;
        }

        switch (command.Kind)
        {
            case VimExKind.Write:
                await _shell.SaveActiveDocumentAsync();
                break;
            case VimExKind.Quit:
                await _shell.CloseActiveDocumentAsync(command.Bang);
                break;
            case VimExKind.WriteQuit:
                if (await _shell.SaveActiveDocumentAsync())
                {
                    await _shell.CloseActiveDocumentAsync(force: false);
                }

                break;
            case VimExKind.QuitAll:
                await QuitAllAsync(command.Bang);
                break;
        }
    }

    // :qa quits the whole app (prompting per modified tab, reusing the window-close path); :qa! discards all.
    private async Task QuitAllAsync(bool force)
    {
        if (force)
        {
            _forceClose = true;
            Close();
            return;
        }

        await ConfirmAndCloseAsync();
    }

    // Carbon's RibbonMenuItem is a plain AvaloniaObject (no DataContext), so its Command cannot be data-bound
    // in XAML. Each dropdown's items share one "arm tool" command; assign it here. The per-item kind is set
    // via CommandParameter in XAML.
    private void WireToolDropdowns(ToolboxViewModel toolbox)
    {
        WireDropdown(ShapesDropDown, toolbox.SelectShapeToolCommand);
        WireDropdown(FlowchartDropDown, toolbox.SelectShapeToolCommand);
        WireDropdown(ArrowsDropDown, toolbox.SelectShapeToolCommand);
        WireDropdown(CommonConnectorsDropDown, toolbox.SelectConnectorToolCommand);
        WireDropdown(UmlConnectorsDropDown, toolbox.SelectConnectorToolCommand);
        WireDropdown(ClassDropDown, toolbox.SelectClassNodeToolCommand);
        WireDropdown(UseCaseDropDown, toolbox.SelectUseCaseToolCommand);
        WireDropdown(StructureDropDown, toolbox.SelectUmlToolCommand);
        WireDropdown(MindMapDropDown, toolbox.SelectShapeToolCommand);
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

    // The Align dropdowns are one-shot (not armed tools), so their shared command resolves the active
    // document at click time — that way they follow tab switches without re-wiring. Each item's
    // AlignmentMode arrives via the XAML CommandParameter; enable/disable is handled by the button's
    // IsEnabled binding to ActiveDocument.CanAlignSelection.
    private static void WireAlignmentDropdown(RibbonDropDownButton dropDown, Action<AlignmentMode> apply)
    {
        RelayCommand<AlignmentMode> command = new(mode =>
        {
            if (mode is { } m)
            {
                apply(m);
            }

            dropDown.IsDropDownOpen = false;
        });

        foreach (RibbonMenuItem item in dropDown.Items)
        {
            item.Command = command;
        }
    }

    // Z-order dropdown: same one-shot pattern as the align dropdowns — a shared command resolves the
    // active document at click time, each item's ZOrderOperation arrives via the XAML CommandParameter,
    // and enable/disable is driven by the button's IsEnabled binding to ActiveDocument.HasNodeSelection.
    private static void WireOrderDropdown(RibbonDropDownButton dropDown, Action<ZOrderOperation> apply)
    {
        RelayCommand<ZOrderOperation> command = new(op =>
        {
            apply(op);
            dropDown.IsDropDownOpen = false;
        });

        foreach (RibbonMenuItem item in dropDown.Items)
        {
            item.Command = command;
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

        await RunWithErrorDialogAsync("Export failed", () => _exporter.SaveAsync(bitmap, path, FormatFromPath(path)));
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

        await RunWithErrorDialogAsync("Export failed", () => File.WriteAllTextAsync(path, svg));
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

        await RunWithErrorDialogAsync("Copy failed", () => _exporter.CopyToClipboardAsync(bitmap));
    }

    // Shared tail for the export/copy actions: run the I/O and surface any expected failure in an error
    // dialog. The filter is the union of what the individual actions can throw (file I/O plus the
    // clipboard's invalid-operation/not-supported cases); unexpected exceptions still propagate.
    private async Task RunWithErrorDialogAsync(string errorTitle, Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
            or InvalidOperationException or NotSupportedException)
        {
            if (_dialogs is not null)
            {
                await _dialogs.ShowErrorAsync(errorTitle, ex.Message);
            }
        }
    }

    private static ImageExportFormat FormatFromPath(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".jpg" or ".jpeg" ? ImageExportFormat.Jpeg : ImageExportFormat.Png;
    }
}
