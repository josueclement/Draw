using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using Draw.App.ViewModels;
using Draw.Diagramming.Layout;
using Draw.Model.Connectors;
using Draw.Model.Nodes;

namespace Draw.App.Input;

/// <summary>An action id resolved to the command (and parameter) it invokes when its shortcut fires.</summary>
public sealed record KeymapAction(string Id, bool ArmsTool, Func<(ICommand Command, object? Parameter)?> Resolve);

/// <summary>
/// Maps keymap action ids (e.g. <c>tool.shape.rectangle</c>, <c>align.left</c>, <c>file.save</c>) to the
/// command they invoke. Commands are resolved lazily against the live <see cref="ShellViewModel"/> so
/// document-scoped actions always target the current tab. "Add" actions arm a tool; the rest run immediately.
/// </summary>
public sealed class KeymapActionRegistry
{
    private readonly ShellViewModel _shell;
    private readonly Dictionary<string, KeymapAction> _actions = new(StringComparer.Ordinal);
    private readonly RelayCommand _selectToolCommand;

    public KeymapActionRegistry(ShellViewModel shell)
    {
        _shell = shell;
        _selectToolCommand = new RelayCommand(() => _shell.Toolbox.ActivateSelectTool());
        Build();
    }

    public bool Contains(string actionId) => _actions.ContainsKey(actionId);

    public bool TryGet(string actionId, [MaybeNullWhen(false)] out KeymapAction action)
        => _actions.TryGetValue(actionId, out action);

    private void Build()
    {
        // Tool arming: an "add" arms the matching tool; the user then clicks/drags on the canvas to place.
        foreach (ShapeKind kind in Enum.GetValues<ShapeKind>())
        {
            Arm($"tool.shape.{Id(kind)}", () => (_shell.Toolbox.SelectShapeToolCommand, kind));
        }

        foreach (RelationshipKind kind in Enum.GetValues<RelationshipKind>())
        {
            Arm($"tool.connector.{Id(kind)}", () => (_shell.Toolbox.SelectConnectorToolCommand, kind));
        }

        foreach (ClassNodeKind kind in Enum.GetValues<ClassNodeKind>())
        {
            Arm($"tool.classNode.{Id(kind)}", () => (_shell.Toolbox.SelectClassNodeToolCommand, kind));
        }

        foreach (UseCaseNodeKind kind in Enum.GetValues<UseCaseNodeKind>())
        {
            Arm($"tool.useCase.{Id(kind)}", () => (_shell.Toolbox.SelectUseCaseToolCommand, kind));
        }

        Arm("tool.entity", () => (_shell.Toolbox.SelectEntityToolCommand, null));
        Add("tool.select", armsTool: true, () => (_selectToolCommand, null));

        // File / shell commands.
        Add("file.new", false, () => (_shell.NewCommand, null));
        Add("file.open", false, () => (_shell.OpenCommand, null));
        Add("file.save", false, () => (_shell.SaveCommand, null));
        Add("file.saveAs", false, () => (_shell.SaveAsCommand, null));
        Add("file.close", false, () => _shell.ActiveDocument is { } d ? ((ICommand)_shell.CloseDocumentCommand, (object?)d) : null);

        // Edit.
        Add("edit.undo", false, () => (_shell.UndoCommand, null));
        Add("edit.redo", false, () => (_shell.RedoCommand, null));
        Add("edit.copy", false, () => (_shell.CopyCommand, null));
        Add("edit.cut", false, () => (_shell.CutCommand, null));
        Add("edit.paste", false, () => (_shell.PasteCommand, null));
        Add("edit.duplicate", false, () => (_shell.DuplicateCommand, null));
        Add("edit.duplicateWithConnectors", false, () => (_shell.DuplicateWithConnectorsCommand, null));
        Add("edit.delete", false, () => (_shell.DeleteCommand, null));
        Add("edit.insertImage", false, () => (_shell.InsertImageCommand, null));
        AddDoc("edit.selectAll", d => (d.SelectAllCommand, null));

        // Document arrange (resolved against the active tab).
        foreach (AlignmentMode mode in Enum.GetValues<AlignmentMode>())
        {
            AddDoc($"align.{Id(mode)}", d => (d.AlignCommand, mode));
        }

        foreach (DistributionMode mode in Enum.GetValues<DistributionMode>())
        {
            AddDoc($"distribute.{Id(mode)}", d => (d.DistributeCommand, mode));
        }

        foreach (ZOrderOperation op in Enum.GetValues<ZOrderOperation>())
        {
            AddDoc($"zorder.{Id(op)}", d => (d.OrderCommand, op));
        }

        AddDoc("edit.spaceConnections", d => (d.SpaceConnectionsCommand, null));
        AddDoc("edit.mergeConnections", d => (d.MergeConnectionsCommand, null));

        // View.
        AddDoc("view.zoomIn", d => (d.ZoomInCommand, null));
        AddDoc("view.zoomOut", d => (d.ZoomOutCommand, null));
        AddDoc("view.zoomReset", d => (d.ZoomResetCommand, null));
        AddDoc("view.fitToContent", d => (d.FitToContentCommand, null));
        Add("view.toggleTheme", false, () => (_shell.ToggleThemeCommand, null));
        Add("view.toggleInspector", false, () => (_shell.ToggleInspectorCommand, null));
        AddDoc("view.toggleGrid", d => (d.ToggleGridCommand, null));

        // Export.
        Add("export.image", false, () => (_shell.ExportImageCommand, null));
        Add("export.svg", false, () => (_shell.ExportSvgCommand, null));
        Add("export.copyImage", false, () => (_shell.CopyImageCommand, null));

        // Keyboard overlays (centered, letter-driven cards).
        Add("menu.shapes", false, () => (_shell.ShowToolMenuCommand, ToolMenuFamily.Shapes));
        Add("menu.connectors", false, () => (_shell.ShowToolMenuCommand, ToolMenuFamily.Connectors));
        Add("menu.icons", false, () => (_shell.ShowIconPaletteCommand, null));
        Add("menu.styles", false, () => (_shell.ShowStylePickerCommand, null));
        Add("menu.align", false, () => (_shell.ShowAlignmentPickerCommand, null));
        Add("menu.help", false, () => (_shell.ShowHelpCommand, null));
    }

    private void Arm(string id, Func<(ICommand Command, object? Parameter)> resolve)
        => Add(id, armsTool: true, () => resolve());

    private void Add(string id, bool armsTool, Func<(ICommand Command, object? Parameter)?> resolve)
        => _actions[id] = new KeymapAction(id, armsTool, resolve);

    private void AddDoc(string id, Func<DiagramDocumentViewModel, (ICommand Command, object? Parameter)> resolve)
        => Add(id, armsTool: false, () => _shell.ActiveDocument is { } d ? resolve(d) : null);

    private static string Id<TEnum>(TEnum value) where TEnum : struct, Enum
        => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
}
