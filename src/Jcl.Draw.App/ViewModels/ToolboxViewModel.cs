using System.Collections.ObjectModel;
using Jcl.Draw.Model.Connectors;
using Jcl.Draw.Model.Nodes;

namespace Jcl.Draw.App.ViewModels;

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
        OnPropertyChanged(nameof(IsConnectorMode));
        OnPropertyChanged(nameof(IsClassNodeMode));
        OnPropertyChanged(nameof(IsUseCaseNodeMode));
        OnPropertyChanged(nameof(ActiveToolHint));
    }
}
