using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using Draw.Model.Connectors;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>Base for a selectable toolbox tool — the armed-tool discriminated union.</summary>
public abstract record ToolItem(string Name);

/// <summary>A selectable shape entry in the toolbox palette.</summary>
public sealed record ShapeToolItem(string Name, ShapeKind Kind) : ToolItem(Name);

/// <summary>A selectable connector (relationship) entry in the toolbox palette.</summary>
public sealed record ConnectorToolItem(string Name, RelationshipKind Kind) : ToolItem(Name);

/// <summary>A selectable class-diagram node entry in the toolbox palette.</summary>
public sealed record ClassNodeToolItem(string Name, ClassNodeKind Kind) : ToolItem(Name);

/// <summary>A selectable use-case-diagram node entry in the toolbox palette.</summary>
public sealed record UseCaseToolItem(string Name, UseCaseNodeKind Kind) : ToolItem(Name);

/// <summary>The ER table tool. There is a single entity kind, so it carries only a display name.</summary>
public sealed record EntityToolItem(string Name) : ToolItem(Name);

/// <summary>A selectable UML structural-node entry in the toolbox palette.</summary>
public sealed record UmlToolItem(string Name, UmlNodeKind Kind) : ToolItem(Name);

/// <summary>
/// Tracks the active drawing tool as a single <see cref="ArmedTool"/> (null = the select tool). The
/// per-category <c>Selected*</c> properties are typed projections over it, so mutual exclusion is
/// automatic: arming any tool replaces <see cref="ArmedTool"/> and every projection reflects the one
/// source of truth. The mode flags and dropdown headers are computed from it too.
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
        new ShapeToolItem("Hexagon", ShapeKind.Hexagon),
        new ShapeToolItem("Pentagon", ShapeKind.Pentagon),
        new ShapeToolItem("Octagon", ShapeKind.Octagon),
        new ShapeToolItem("Star", ShapeKind.Star),
        new ShapeToolItem("Cross", ShapeKind.Cross),
        new ShapeToolItem("Cloud", ShapeKind.Cloud),
        new ShapeToolItem("Callout", ShapeKind.Callout),
        new ShapeToolItem("Terminator", ShapeKind.Terminator),
        new ShapeToolItem("Cylinder", ShapeKind.Cylinder),
        new ShapeToolItem("Document", ShapeKind.Document),
        new ShapeToolItem("Predefined process", ShapeKind.PredefinedProcess),
        new ShapeToolItem("Manual input", ShapeKind.ManualInput),
        new ShapeToolItem("Off-page connector", ShapeKind.OffPageConnector),
        new ShapeToolItem("Display", ShapeKind.Display),
        new ShapeToolItem("Delay", ShapeKind.Delay),
        new ShapeToolItem("Arrow right", ShapeKind.ArrowRight),
        new ShapeToolItem("Arrow left", ShapeKind.ArrowLeft),
        new ShapeToolItem("Arrow up", ShapeKind.ArrowUp),
        new ShapeToolItem("Arrow down", ShapeKind.ArrowDown),
        new ShapeToolItem("Bidirectional", ShapeKind.ArrowDouble),
        new ShapeToolItem("Topic", ShapeKind.MindMapTopic),
        new ShapeToolItem("Rounded topic", ShapeKind.MindMapTopicRounded),
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
        new ConnectorToolItem("Mind-map branch", RelationshipKind.MindMapBranch),
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

    public ObservableCollection<UmlToolItem> UmlNodes { get; } = new()
    {
        new UmlToolItem("Package", UmlNodeKind.Package),
        new UmlToolItem("Component", UmlNodeKind.Component),
        new UmlToolItem("Deployment", UmlNodeKind.Deployment),
    };

    /// <summary>The single ER table tool, armed by the ER ribbon button.</summary>
    public EntityToolItem Entity { get; } = new("Table");

    public ToolboxViewModel()
    {
        // Each ribbon dropdown item arms a tool by its kind via the typed projection setters.
        SelectShapeToolCommand = new RelayCommand<ShapeKind>(kind => SelectedShape = Shapes.First(s => s.Kind == kind));
        SelectConnectorToolCommand = new RelayCommand<RelationshipKind>(kind => SelectedConnector = Connectors.First(c => c.Kind == kind));
        SelectClassNodeToolCommand = new RelayCommand<ClassNodeKind>(kind => SelectedClassNode = ClassNodes.First(c => c.Kind == kind));
        SelectUseCaseToolCommand = new RelayCommand<UseCaseNodeKind>(kind => SelectedUseCaseNode = UseCaseNodes.First(u => u.Kind == kind));
        SelectUmlToolCommand = new RelayCommand<UmlNodeKind>(kind => SelectedUmlNode = UmlNodes.First(u => u.Kind == kind));
        SelectEntityToolCommand = new RelayCommand(() => SelectedEntity = Entity);
    }

    public RelayCommand<ShapeKind> SelectShapeToolCommand { get; }

    public RelayCommand<RelationshipKind> SelectConnectorToolCommand { get; }

    public RelayCommand<ClassNodeKind> SelectClassNodeToolCommand { get; }

    public RelayCommand<UseCaseNodeKind> SelectUseCaseToolCommand { get; }

    public RelayCommand<UmlNodeKind> SelectUmlToolCommand { get; }

    public RelayCommand SelectEntityToolCommand { get; }

    /// <summary>The single armed tool, or null for the select tool. Arming a tool replaces it; the
    /// projections and mode flags below all derive from it, so adding a category needs no edits here.</summary>
    public ToolItem? ArmedTool
    {
        get;
        private set
        {
            if (SetProperty(ref field, value))
            {
                RaiseModes();
            }
        }
    }

    // Typed projections over ArmedTool. Setting one to non-null arms it (replacing any other tool);
    // setting it to null disarms only when it currently owns ArmedTool. No manual cross-nulling needed.
    public ShapeToolItem? SelectedShape
    {
        get => ArmedTool as ShapeToolItem;
        set => Arm(value, () => ArmedTool is ShapeToolItem);
    }

    public ConnectorToolItem? SelectedConnector
    {
        get => ArmedTool as ConnectorToolItem;
        set => Arm(value, () => ArmedTool is ConnectorToolItem);
    }

    public ClassNodeToolItem? SelectedClassNode
    {
        get => ArmedTool as ClassNodeToolItem;
        set => Arm(value, () => ArmedTool is ClassNodeToolItem);
    }

    public UseCaseToolItem? SelectedUseCaseNode
    {
        get => ArmedTool as UseCaseToolItem;
        set => Arm(value, () => ArmedTool is UseCaseToolItem);
    }

    public UmlToolItem? SelectedUmlNode
    {
        get => ArmedTool as UmlToolItem;
        set => Arm(value, () => ArmedTool is UmlToolItem);
    }

    public EntityToolItem? SelectedEntity
    {
        get => ArmedTool as EntityToolItem;
        set => Arm(value, () => ArmedTool is EntityToolItem);
    }

    public bool IsSelectTool => ArmedTool is null;

    public bool IsConnectorMode => ArmedTool is ConnectorToolItem;

    public bool IsClassNodeMode => ArmedTool is ClassNodeToolItem;

    public bool IsUseCaseNodeMode => ArmedTool is UseCaseToolItem;

    public bool IsUmlNodeMode => ArmedTool is UmlToolItem;

    public bool IsEntityNodeMode => ArmedTool is EntityToolItem;

    public bool IsShapeMode => ArmedTool is ShapeToolItem;

    // One armed shape feeds several dropdowns (Shapes/Flowchart/Arrows); each shows the pick only when the
    // armed kind belongs to its category, else its own label. Note has its own button, so no header claims it.
    private enum ShapeCategory
    {
        Basic,
        Flowchart,
        Arrow,
        MindMap,
        Other,
    }

    private static ShapeCategory CategoryOf(ShapeKind kind) => kind switch
    {
        ShapeKind.Terminator or ShapeKind.Cylinder or ShapeKind.Document or ShapeKind.PredefinedProcess
            or ShapeKind.ManualInput or ShapeKind.OffPageConnector or ShapeKind.Display or ShapeKind.Delay
            => ShapeCategory.Flowchart,
        ShapeKind.ArrowRight or ShapeKind.ArrowLeft or ShapeKind.ArrowUp or ShapeKind.ArrowDown or ShapeKind.ArrowDouble
            => ShapeCategory.Arrow,
        ShapeKind.MindMapTopic or ShapeKind.MindMapTopicRounded => ShapeCategory.MindMap,
        ShapeKind.Note => ShapeCategory.Other,
        _ => ShapeCategory.Basic,
    };

    private string HeaderFor(ShapeCategory category, string label) =>
        ArmedTool is ShapeToolItem shape && CategoryOf(shape.Kind) == category ? shape.Name : label;

    /// <summary>Dropdown-button captions: the active pick's name, or the category label when nothing is armed.</summary>
    public string ShapesHeader => HeaderFor(ShapeCategory.Basic, "Shapes");

    public string FlowchartHeader => HeaderFor(ShapeCategory.Flowchart, "Flowchart");

    public string ArrowsHeader => HeaderFor(ShapeCategory.Arrow, "Arrows");

    public string MindMapHeader => HeaderFor(ShapeCategory.MindMap, "Mind map");

    // One armed connector feeds two dropdowns; each shows the pick only when it owns that kind,
    // else its category label. ER "Relationship" lives in the ER group, so neither claims it.
    public string CommonConnectorsHeader =>
        ArmedTool is ConnectorToolItem c && IsCommonConnector(c.Kind) ? c.Name : "Connector";

    public string UmlConnectorsHeader =>
        ArmedTool is ConnectorToolItem c && !IsCommonConnector(c.Kind)
            && c.Kind != RelationshipKind.Relationship && c.Kind != RelationshipKind.MindMapBranch
            ? c.Name : "Relationship";

    private static bool IsCommonConnector(RelationshipKind kind) =>
        kind is RelationshipKind.Association or RelationshipKind.DirectedAssociation;

    public string ClassHeader => (ArmedTool as ClassNodeToolItem)?.Name ?? "Class diagram";

    public string UseCaseHeader => (ArmedTool as UseCaseToolItem)?.Name ?? "Use case";

    public string StructureHeader => (ArmedTool as UmlToolItem)?.Name ?? "Structure";

    /// <summary>
    /// Status-bar hint describing how to use the armed tool; <c>null</c> when the select tool is active.
    /// </summary>
    public string? ActiveToolHint => ArmedTool switch
    {
        ShapeToolItem shape => $"Click on the canvas to place {shape.Name}.",
        ClassNodeToolItem classNode => $"Click on the canvas to place {classNode.Name}.",
        UseCaseToolItem useCaseNode => $"Click on the canvas to place {useCaseNode.Name}.",
        UmlToolItem umlNode => $"Click on the canvas to place {umlNode.Name}.",
        EntityToolItem entity => $"Click on the canvas to place {entity.Name}.",
        ConnectorToolItem connector => $"Drag from one node to another to draw {connector.Name}.",
        _ => null,
    };

    public void ActivateSelectTool() => ArmedTool = null;

    // Arms the given tool, or — when disarming (null) — clears ArmedTool only if the caller's category
    // currently owns it, so disarming one projection never wipes a different armed tool.
    private void Arm(ToolItem? tool, System.Func<bool> ownsArmedTool)
    {
        if (tool is not null)
        {
            ArmedTool = tool;
        }
        else if (ownsArmedTool())
        {
            ArmedTool = null;
        }
    }

    private void RaiseModes()
    {
        OnPropertyChanged(nameof(SelectedShape));
        OnPropertyChanged(nameof(SelectedConnector));
        OnPropertyChanged(nameof(SelectedClassNode));
        OnPropertyChanged(nameof(SelectedUseCaseNode));
        OnPropertyChanged(nameof(SelectedUmlNode));
        OnPropertyChanged(nameof(SelectedEntity));
        OnPropertyChanged(nameof(IsSelectTool));
        OnPropertyChanged(nameof(IsShapeMode));
        OnPropertyChanged(nameof(IsConnectorMode));
        OnPropertyChanged(nameof(IsClassNodeMode));
        OnPropertyChanged(nameof(IsUseCaseNodeMode));
        OnPropertyChanged(nameof(IsUmlNodeMode));
        OnPropertyChanged(nameof(IsEntityNodeMode));
        OnPropertyChanged(nameof(ShapesHeader));
        OnPropertyChanged(nameof(FlowchartHeader));
        OnPropertyChanged(nameof(ArrowsHeader));
        OnPropertyChanged(nameof(MindMapHeader));
        OnPropertyChanged(nameof(CommonConnectorsHeader));
        OnPropertyChanged(nameof(UmlConnectorsHeader));
        OnPropertyChanged(nameof(ClassHeader));
        OnPropertyChanged(nameof(UseCaseHeader));
        OnPropertyChanged(nameof(StructureHeader));
        OnPropertyChanged(nameof(ActiveToolHint));
    }
}
