using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Carbon.Avalonia.Desktop.Controls.Ribbon;
using CommunityToolkit.Mvvm.Input;
using Jcl.Draw.App.Services;
using Jcl.Draw.App.ViewModels;

namespace Jcl.Draw.App.Views;

public partial class MainWindow : Window
{
    private readonly ShellViewModel? _shell;
    private readonly IImageExportService? _exporter;
    private readonly IFileDialogService? _fileDialogs;
    private readonly IDialogService? _dialogs;

    // Parameterless constructor for the XAML previewer/designer.
    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(
        ShellViewModel shell,
        IImageExportService exporter,
        IFileDialogService fileDialogs,
        IDialogService dialogs)
        : this()
    {
        _shell = shell;
        _exporter = exporter;
        _fileDialogs = fileDialogs;
        _dialogs = dialogs;
        DataContext = shell;
        shell.ExportPngRequested += OnExportPngRequested;
        shell.CopyImageRequested += OnCopyImageRequested;
        WireToolDropdowns(shell.Toolbox);
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

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

    private DiagramView? ActiveDiagramView()
        => _shell is null
            ? null
            : this.GetVisualDescendants()
                .OfType<DiagramView>()
                .FirstOrDefault(v => ReferenceEquals(v.DataContext, _shell.ActiveDocument));

    private async void OnExportPngRequested(object? sender, EventArgs e)
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

        string? path = await _fileDialogs.PickSavePngAsync("diagram");
        if (path is null)
        {
            return;
        }

        try
        {
            await _exporter.ExportPngAsync(view.ExportTarget, path);
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

        try
        {
            await _exporter.CopyPngToClipboardAsync(view.ExportTarget);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or NotSupportedException)
        {
            if (_dialogs is not null)
            {
                await _dialogs.ShowErrorAsync("Copy failed", ex.Message);
            }
        }
    }
}
