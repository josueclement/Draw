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

    public ToolboxViewModel()
    {
        // Each ribbon dropdown item arms a tool by its kind; reuse the mutually-exclusive Selected* setters.
        SelectShapeToolCommand = new RelayCommand<ShapeKind>(kind => SelectedShape = Shapes.First(s => s.Kind == kind));
        SelectConnectorToolCommand = new RelayCommand<RelationshipKind>(kind => SelectedConnector = Connectors.First(c => c.Kind == kind));
        SelectClassNodeToolCommand = new RelayCommand<ClassNodeKind>(kind => SelectedClassNode = ClassNodes.First(c => c.Kind == kind));
        SelectUseCaseToolCommand = new RelayCommand<UseCaseNodeKind>(kind => SelectedUseCaseNode = UseCaseNodes.First(u => u.Kind == kind));
    }

    public RelayCommand<ShapeKind> SelectShapeToolCommand { get; }

    public RelayCommand<RelationshipKind> SelectConnectorToolCommand { get; }

    public RelayCommand<ClassNodeKind> SelectClassNodeToolCommand { get; }

    public RelayCommand<UseCaseNodeKind> SelectUseCaseToolCommand { get; }

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
                }

                RaiseModes();
            }
        }
    }

    public bool IsSelectTool => SelectedShape is null && SelectedConnector is null
        && SelectedClassNode is null && SelectedUseCaseNode is null;

    public bool IsConnectorMode => SelectedConnector is not null;

    public bool IsClassNodeMode => SelectedClassNode is not null;

    public bool IsUseCaseNodeMode => SelectedUseCaseNode is not null;

    public bool IsShapeMode => SelectedShape is not null;

    /// <summary>Dropdown-button captions: the active pick's name, or the category label when nothing is armed.</summary>
    public string ShapesHeader => SelectedShape?.Name ?? "Shapes";

    public string ConnectorsHeader => SelectedConnector?.Name ?? "Connectors";

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
    }

    private void RaiseModes()
    {
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsShapeMode));
        OnPropertyChanged(nameof(IsConnectorMode));
        OnPropertyChanged(nameof(IsClassNodeMode));
        OnPropertyChanged(nameof(IsUseCaseNodeMode));
        OnPropertyChanged(nameof(ShapesHeader));
        OnPropertyChanged(nameof(ConnectorsHeader));
        OnPropertyChanged(nameof(ClassHeader));
        OnPropertyChanged(nameof(UseCaseHeader));
        OnPropertyChanged(nameof(ActiveToolHint));
    }
}
