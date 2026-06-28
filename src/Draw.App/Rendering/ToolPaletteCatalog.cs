using System.Collections.Generic;
using Avalonia;
using Avalonia.Media;
using Draw.App.ViewModels;
using Draw.Model.Connectors;
using Draw.Model.Nodes;
using PhosphorIconsAvalonia;

namespace Draw.App.Rendering;

/// <summary>
/// The tool a palette entry arms — a closed union over the toolbox's <c>Select*ToolCommand</c>
/// parameters. The palette dispatches on it to arm exactly the tool the old context menu did.
/// </summary>
public abstract record ToolArm;

/// <summary>Arms a basic/flowchart/arrow/mind-map shape.</summary>
public sealed record ShapeArm(ShapeKind Kind) : ToolArm;

/// <summary>Arms a connector (relationship).</summary>
public sealed record ConnectorArm(RelationshipKind Kind) : ToolArm;

/// <summary>Arms a UML class-diagram node (class / interface / enum).</summary>
public sealed record ClassNodeArm(ClassNodeKind Kind) : ToolArm;

/// <summary>Arms a use-case-diagram node (actor / use case / system boundary).</summary>
public sealed record UseCaseArm(UseCaseNodeKind Kind) : ToolArm;

/// <summary>Arms a UML structural node (package / component / deployment).</summary>
public sealed record UmlArm(UmlNodeKind Kind) : ToolArm;

/// <summary>Arms the single ER table tool (it carries no kind).</summary>
public sealed record EntityArm : ToolArm;

/// <summary>One selectable entry: a display name, its resolved icon glyph, and the tool it arms.</summary>
public sealed record ToolCatalogEntry(string Name, Geometry Icon, ToolArm Arm);

/// <summary>A named group of entries within a palette family.</summary>
public sealed record ToolCatalogCategory(string Name, IReadOnlyList<ToolCatalogEntry> Items);

/// <summary>A whole palette family (shapes / connectors): its ordered categories.</summary>
public sealed record ToolCatalogFamily(IReadOnlyList<ToolCatalogCategory> Categories);

/// <summary>
/// The declarative source of truth for the keyboard tool palette — a literal transcription of the
/// retired <c>Resources/ToolMenus.axaml</c> (same names, same glyphs, same arm payloads). Icons resolve
/// the same two ways the ribbon does (Phosphor via <see cref="IconService"/>, custom via the merged
/// <c>ToolIcons.axaml</c> resources), so a view-model can hold the resulting <see cref="Geometry"/>
/// without referencing any Avalonia control. Each family is built once and cached (glyphs are immutable
/// by use, safe to bind to many controls — see <see cref="NodeMarkerVisuals"/>).
///
/// NOTE: this is one of three places the shape/connector taxonomy lives — the others are the ribbon
/// dropdowns (<c>MainWindow.axaml</c>) and <see cref="ToolboxViewModel"/>'s collections — kept in sync
/// by hand. Adding a kind means editing the ribbon and this catalog.
/// </summary>
public static class ToolPaletteCatalog
{
    private static ToolCatalogFamily? _shapes;
    private static ToolCatalogFamily? _connectors;

    /// <summary>The catalog for a palette family, resolving and caching its icon geometries on first use.</summary>
    public static ToolCatalogFamily For(ToolMenuFamily family) => family switch
    {
        ToolMenuFamily.Connectors => _connectors ??= BuildConnectors(),
        _ => _shapes ??= BuildShapes(),
    };

    private static ToolCatalogFamily BuildShapes() => new(new[]
    {
        new ToolCatalogCategory("Common", new[]
        {
            E("Rectangle", Phosphor(Icon.rectangle), new ShapeArm(ShapeKind.Rectangle)),
            E("Rounded rectangle", Tool("ToolIcon.RoundedRectangle"), new ShapeArm(ShapeKind.RoundedRectangle)),
            E("Ellipse", Tool("ToolIcon.Ellipse"), new ShapeArm(ShapeKind.Ellipse)),
            E("Circle", Phosphor(Icon.circle), new ShapeArm(ShapeKind.Circle)),
            E("Diamond", Phosphor(Icon.diamond), new ShapeArm(ShapeKind.Diamond)),
            E("Parallelogram", Phosphor(Icon.parallelogram), new ShapeArm(ShapeKind.Parallelogram)),
            E("Trapezoid", Tool("ToolIcon.Trapezoid"), new ShapeArm(ShapeKind.Trapezoid)),
            E("Triangle", Phosphor(Icon.triangle), new ShapeArm(ShapeKind.Triangle)),
            E("Hexagon", Tool("ToolIcon.Hexagon"), new ShapeArm(ShapeKind.Hexagon)),
            E("Pentagon", Tool("ToolIcon.Pentagon"), new ShapeArm(ShapeKind.Pentagon)),
            E("Octagon", Tool("ToolIcon.Octagon"), new ShapeArm(ShapeKind.Octagon)),
            E("Star", Tool("ToolIcon.Star"), new ShapeArm(ShapeKind.Star)),
            E("Cross", Tool("ToolIcon.Cross"), new ShapeArm(ShapeKind.Cross)),
            E("Cloud", Tool("ToolIcon.Cloud"), new ShapeArm(ShapeKind.Cloud)),
            E("Callout", Tool("ToolIcon.Callout"), new ShapeArm(ShapeKind.Callout)),
        }),
        new ToolCatalogCategory("Flowchart", new[]
        {
            E("Terminator", Tool("ToolIcon.Terminator"), new ShapeArm(ShapeKind.Terminator)),
            E("Cylinder", Tool("ToolIcon.Cylinder"), new ShapeArm(ShapeKind.Cylinder)),
            E("Document", Tool("ToolIcon.Document"), new ShapeArm(ShapeKind.Document)),
            E("Predefined process", Tool("ToolIcon.PredefinedProcess"), new ShapeArm(ShapeKind.PredefinedProcess)),
            E("Manual input", Tool("ToolIcon.ManualInput"), new ShapeArm(ShapeKind.ManualInput)),
            E("Off-page connector", Tool("ToolIcon.OffPageConnector"), new ShapeArm(ShapeKind.OffPageConnector)),
            E("Display", Tool("ToolIcon.Display"), new ShapeArm(ShapeKind.Display)),
            E("Delay", Tool("ToolIcon.Delay"), new ShapeArm(ShapeKind.Delay)),
        }),
        new ToolCatalogCategory("Arrows", new[]
        {
            E("Arrow right", Tool("ToolIcon.ArrowRight"), new ShapeArm(ShapeKind.ArrowRight)),
            E("Arrow left", Tool("ToolIcon.ArrowLeft"), new ShapeArm(ShapeKind.ArrowLeft)),
            E("Arrow up", Tool("ToolIcon.ArrowUp"), new ShapeArm(ShapeKind.ArrowUp)),
            E("Arrow down", Tool("ToolIcon.ArrowDown"), new ShapeArm(ShapeKind.ArrowDown)),
            E("Bidirectional", Tool("ToolIcon.ArrowDouble"), new ShapeArm(ShapeKind.ArrowDouble)),
        }),
        new ToolCatalogCategory("UML", new[]
        {
            E("Note", Tool("ToolIcon.Note"), new ShapeArm(ShapeKind.Note)),
            E("Class", Phosphor(Icon.square_split_horizontal), new ClassNodeArm(ClassNodeKind.Class)),
            E("Interface", Tool("ToolIcon.Interface"), new ClassNodeArm(ClassNodeKind.Interface)),
            E("Enum", Phosphor(Icon.list_numbers), new ClassNodeArm(ClassNodeKind.Enum)),
            E("Actor", Phosphor(Icon.user), new UseCaseArm(UseCaseNodeKind.Actor)),
            E("Use case", Tool("ToolIcon.Ellipse"), new UseCaseArm(UseCaseNodeKind.UseCase)),
            E("System boundary", Phosphor(Icon.bounding_box), new UseCaseArm(UseCaseNodeKind.SystemBoundary)),
            E("Package", Tool("ToolIcon.Package"), new UmlArm(UmlNodeKind.Package)),
            E("Component", Tool("ToolIcon.Component"), new UmlArm(UmlNodeKind.Component)),
            E("Deployment", Tool("ToolIcon.Deployment"), new UmlArm(UmlNodeKind.Deployment)),
        }),
        new ToolCatalogCategory("ER", new[]
        {
            E("Table", Phosphor(Icon.table), new EntityArm()),
        }),
        new ToolCatalogCategory("Mind map", new[]
        {
            E("Topic", Phosphor(Icon.rectangle), new ShapeArm(ShapeKind.MindMapTopic)),
            E("Rounded topic", Tool("ToolIcon.RoundedRectangle"), new ShapeArm(ShapeKind.MindMapTopicRounded)),
        }),
    });

    private static ToolCatalogFamily BuildConnectors() => new(new[]
    {
        new ToolCatalogCategory("Common", new[]
        {
            E("Association", Tool("ToolIcon.Association"), new ConnectorArm(RelationshipKind.Association)),
            E("Directed association", Tool("ToolIcon.DirectedAssociation"), new ConnectorArm(RelationshipKind.DirectedAssociation)),
        }),
        new ToolCatalogCategory("UML", new[]
        {
            E("Aggregation", Tool("ToolIcon.Aggregation"), new ConnectorArm(RelationshipKind.Aggregation)),
            E("Composition", Tool("ToolIcon.Composition"), new ConnectorArm(RelationshipKind.Composition)),
            E("Generalization", Tool("ToolIcon.Generalization"), new ConnectorArm(RelationshipKind.Generalization)),
            E("Realization", Tool("ToolIcon.Realization"), new ConnectorArm(RelationshipKind.Realization)),
            E("Dependency", Tool("ToolIcon.Dependency"), new ConnectorArm(RelationshipKind.Dependency)),
            E("Include", Tool("ToolIcon.Dependency"), new ConnectorArm(RelationshipKind.Include)),
            E("Extend", Tool("ToolIcon.Dependency"), new ConnectorArm(RelationshipKind.Extend)),
        }),
        new ToolCatalogCategory("ER", new[]
        {
            E("Relationship", Phosphor(Icon.line_segment), new ConnectorArm(RelationshipKind.Relationship)),
        }),
        new ToolCatalogCategory("Mind map", new[]
        {
            E("Branch", Tool("ToolIcon.MindMapBranch"), new ConnectorArm(RelationshipKind.MindMapBranch)),
        }),
    });

    private static ToolCatalogEntry E(string name, Geometry icon, ToolArm arm) => new(name, icon, arm);

    private static Geometry Phosphor(Icon icon) => IconService.CreateGeometry(icon, IconType.regular);

    private static Geometry Tool(string key)
        => Application.Current!.TryGetResource(key, null, out object? value) && value is Geometry g
            ? g
            : new StreamGeometry();
}
