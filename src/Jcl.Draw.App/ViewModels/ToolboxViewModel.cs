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
    };

    public ObservableCollection<ClassNodeToolItem> ClassNodes { get; } = new()
    {
        new ClassNodeToolItem("Class", ClassNodeKind.Class),
        new ClassNodeToolItem("Interface", ClassNodeKind.Interface),
        new ClassNodeToolItem("Enum", ClassNodeKind.Enum),
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
                }

                RaiseModes();
            }
        }
    }

    public bool IsSelectTool => SelectedShape is null && SelectedConnector is null && SelectedClassNode is null;

    public bool IsConnectorMode => SelectedConnector is not null;

    public bool IsClassNodeMode => SelectedClassNode is not null;

    public void ActivateSelectTool()
    {
        SelectedShape = null;
        SelectedConnector = null;
        SelectedClassNode = null;
    }

    private void RaiseModes()
    {
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsConnectorMode));
        OnPropertyChanged(nameof(IsClassNodeMode));
    }
}
