using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.Model.Connectors;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>A selectable shape entry in the toolbox palette.</summary>
public sealed record ShapeToolItem(string Name, ShapeKind Kind);

/// <summary>A selectable connector (relationship) entry in the toolbox palette.</summary>
public sealed record ConnectorToolItem(string Name, RelationshipKind Kind);

/// <summary>A selectable class-diagram node entry in the toolbox palette.</summary>
public sealed record ClassNodeToolItem(string Name, ClassNodeKind Kind);

/// <summary>A selectable use-case-diagram node entry in the toolbox palette.</summary>
public sealed record UseCaseToolItem(string Name, UseCaseNodeKind Kind);

/// <summary>The ER table tool. There is a single entity kind, so it carries only a display name.</summary>
public sealed record EntityToolItem(string Name);

/// <summary>
/// Tracks the active drawing tool: the select tool (both null), a shape to place
/// (<see cref="SelectedShape"/>), or a connector to draw (<see cref="SelectedConnector"/>).
/// </summary>
public sealed class ToolboxViewModel : ViewModelBase
{
    public ObservableCollection<ShapeToolItem> Shapes { get; } = new()
    {
        new ShapeToolItem("Rectangle", ShapeKind.Rectangle),
        new ShapeToolItem("Rounded rectangle", ShapeKind.RoundedRectangle),
        new ShapeToolItem("Ellipse", ShapeKind.Ellipse),
        new ShapeToolItem("Circle", ShapeKind.Circle),
        new ShapeToolItem("Diamond", ShapeKind.Diamond),
        new ShapeToolItem("Parallelogram", ShapeKind.Parallelogram),
        new ShapeToolItem("Trapezoid", ShapeKind.Trapezoid),
        new ShapeToolItem("Triangle", ShapeKind.Triangle),
        // Armed by the standalone UML-group "Note" button; intentionally absent from the Shapes dropdown.
        new ShapeToolItem("Note", ShapeKind.Note),
    };

    public ObservableCollection<ConnectorToolItem> Connectors { get; } = new()
    {
        new ConnectorToolItem("Association", RelationshipKind.Association),
        new ConnectorToolItem("Directed association", RelationshipKind.DirectedAssociation),
        new ConnectorToolItem("Aggregation", RelationshipKind.Aggregation),
        new ConnectorToolItem("Composition", RelationshipKind.Composition),
        new ConnectorToolItem("Generalization", RelationshipKind.Generalization),
        new ConnectorToolItem("Realization", RelationshipKind.Realization),
        new ConnectorToolItem("Dependency", RelationshipKind.Dependency),
        new ConnectorToolItem("Include", RelationshipKind.Include),
        new ConnectorToolItem("Extend", RelationshipKind.Extend),
        new ConnectorToolItem("Relationship", RelationshipKind.Relationship),
    };

    public ObservableCollection<ClassNodeToolItem> ClassNodes { get; } = new()
    {
        new ClassNodeToolItem("Class", ClassNodeKind.Class),
        new ClassNodeToolItem("Interface", ClassNodeKind.Interface),
        new ClassNodeToolItem("Enum", ClassNodeKind.Enum),
    };

    public ObservableCollection<UseCaseToolItem> UseCaseNodes { get; } = new()
    {
        new UseCaseToolItem("Actor", UseCaseNodeKind.Actor),
        new UseCaseToolItem("Use case", UseCaseNodeKind.UseCase),
        new UseCaseToolItem("System boundary", UseCaseNodeKind.SystemBoundary),
    };

    /// <summary>The single ER table tool, armed by the ER ribbon button.</summary>
    public EntityToolItem Entity { get; } = new("Table");

    public ToolboxViewModel()
    {
        // Each ribbon dropdown item arms a tool by its kind; reuse the mutually-exclusive Selected* setters.
        SelectShapeToolCommand = new RelayCommand<ShapeKind>(kind => SelectedShape = Shapes.First(s => s.Kind == kind));
        SelectConnectorToolCommand = new RelayCommand<RelationshipKind>(kind => SelectedConnector = Connectors.First(c => c.Kind == kind));
        SelectClassNodeToolCommand = new RelayCommand<ClassNodeKind>(kind => SelectedClassNode = ClassNodes.First(c => c.Kind == kind));
        SelectUseCaseToolCommand = new RelayCommand<UseCaseNodeKind>(kind => SelectedUseCaseNode = UseCaseNodes.First(u => u.Kind == kind));
        SelectEntityToolCommand = new RelayCommand(() => SelectedEntity = Entity);
    }

    public RelayCommand<ShapeKind> SelectShapeToolCommand { get; }

    public RelayCommand<RelationshipKind> SelectConnectorToolCommand { get; }

    public RelayCommand<ClassNodeKind> SelectClassNodeToolCommand { get; }

    public RelayCommand<UseCaseNodeKind> SelectUseCaseToolCommand { get; }

    public RelayCommand SelectEntityToolCommand { get; }

    public ShapeToolItem? SelectedShape
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is not null)
                {
                    SelectedConnector = null;
                    SelectedClassNode = null;
                    SelectedUseCaseNode = null;
                    SelectedEntity = null;
                }

                RaiseModes();
            }
        }
    }

    public ConnectorToolItem? SelectedConnector
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is not null)
                {
                    SelectedShape = null;
                    SelectedClassNode = null;
                    SelectedUseCaseNode = null;
                    SelectedEntity = null;
                }

                RaiseModes();
            }
        }
    }

    public ClassNodeToolItem? SelectedClassNode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is not null)
                {
                    SelectedShape = null;
                    SelectedConnector = null;
                    SelectedUseCaseNode = null;
                    SelectedEntity = null;
                }

                RaiseModes();
            }
        }
    }

    public UseCaseToolItem? SelectedUseCaseNode
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is not null)
                {
                    SelectedShape = null;
                    SelectedConnector = null;
                    SelectedClassNode = null;
                    SelectedEntity = null;
                }

                RaiseModes();
            }
        }
    }

    public EntityToolItem? SelectedEntity
    {
        get;
        set
        {
            if (SetProperty(ref field, value))
            {
                if (value is not null)
                {
                    SelectedShape = null;
                    SelectedConnector = null;
                    SelectedClassNode = null;
                    SelectedUseCaseNode = null;
                }

                RaiseModes();
            }
        }
    }

    public bool IsSelectTool => SelectedShape is null && SelectedConnector is null
        && SelectedClassNode is null && SelectedUseCaseNode is null && SelectedEntity is null;

    public bool IsConnectorMode => SelectedConnector is not null;

    public bool IsClassNodeMode => SelectedClassNode is not null;

    public bool IsUseCaseNodeMode => SelectedUseCaseNode is not null;

    public bool IsEntityNodeMode => SelectedEntity is not null;

    public bool IsShapeMode => SelectedShape is not null;

    /// <summary>Dropdown-button captions: the active pick's name, or the category label when nothing is armed.</summary>
    public string ShapesHeader => SelectedShape?.Name ?? "Shapes";

    // One armed connector feeds two dropdowns; each shows the pick only when it owns that kind,
    // else its category label. ER "Relationship" lives in the ER group, so neither claims it.
    public string CommonConnectorsHeader =>
        SelectedConnector is { } c && IsCommonConnector(c.Kind) ? c.Name : "Connector";

    public string UmlConnectorsHeader =>
        SelectedConnector is { } c && !IsCommonConnector(c.Kind) && c.Kind != RelationshipKind.Relationship
            ? c.Name : "Relationship";

    private static bool IsCommonConnector(RelationshipKind kind) =>
        kind is RelationshipKind.Association or RelationshipKind.DirectedAssociation;

    public string ClassHeader => SelectedClassNode?.Name ?? "Class diagram";

    public string UseCaseHeader => SelectedUseCaseNode?.Name ?? "Use case";

    /// <summary>
    /// Status-bar hint describing how to use the armed tool; <c>null</c> when the select tool is active.
    /// </summary>
    public string? ActiveToolHint
    {
        get
        {
            if (SelectedShape is { } shape)
            {
                return $"Click on the canvas to place {shape.Name}.";
            }

            if (SelectedClassNode is { } classNode)
            {
                return $"Click on the canvas to place {classNode.Name}.";
            }

            if (SelectedUseCaseNode is { } useCaseNode)
            {
                return $"Click on the canvas to place {useCaseNode.Name}.";
            }

            if (SelectedEntity is { } entity)
            {
                return $"Click on the canvas to place {entity.Name}.";
            }

            if (SelectedConnector is { } connector)
            {
                return $"Drag from one node to another to draw {connector.Name}.";
            }

            return null;
        }
    }

    public void ActivateSelectTool()
    {
        SelectedShape = null;
        SelectedConnector = null;
        SelectedClassNode = null;
        SelectedUseCaseNode = null;
        SelectedEntity = null;
    }

    private void RaiseModes()
    {
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsShapeMode));
        OnPropertyChanged(nameof(IsConnectorMode));
        OnPropertyChanged(nameof(IsClassNodeMode));
        OnPropertyChanged(nameof(IsUseCaseNodeMode));
        OnPropertyChanged(nameof(IsEntityNodeMode));
        OnPropertyChanged(nameof(ShapesHeader));
        OnPropertyChanged(nameof(CommonConnectorsHeader));
        OnPropertyChanged(nameof(UmlConnectorsHeader));
        OnPropertyChanged(nameof(ClassHeader));
        OnPropertyChanged(nameof(UseCaseHeader));
        OnPropertyChanged(nameof(ActiveToolHint));
    }
}
