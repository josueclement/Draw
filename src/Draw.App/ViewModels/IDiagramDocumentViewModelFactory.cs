using Draw.App.Configuration;
using Draw.App.Services;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Styling;
using Draw.Diagramming.Undo;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;
using Microsoft.Extensions.Options;

namespace Draw.App.ViewModels;

/// <summary>Creates document view models, each with its own undo history.</summary>
public interface IDiagramDocumentViewModelFactory
{
    DiagramDocumentViewModel Create(DiagramDocument document, string? filePath);

    DiagramDocumentViewModel CreateNew(DiagramType type);
}

public sealed class DiagramDocumentViewModelFactory : IDiagramDocumentViewModelFactory
{
    private readonly IDocumentSerializer _serializer;
    private readonly IConnectorRouter _router;
    private readonly IOptions<EditorOptions> _editorOptions;
    private readonly IOptions<UndoOptions> _undoOptions;
    private readonly IThemeService _theme;
    private readonly IClipboardService _clipboard;
    private readonly NodeKindRegistry _nodeKinds;

    public DiagramDocumentViewModelFactory(
        IDocumentSerializer serializer,
        IConnectorRouter router,
        IOptions<EditorOptions> editorOptions,
        IOptions<UndoOptions> undoOptions,
        IThemeService theme,
        IClipboardService clipboard,
        NodeKindRegistry nodeKinds)
    {
        _serializer = serializer;
        _router = router;
        _editorOptions = editorOptions;
        _undoOptions = undoOptions;
        _theme = theme;
        _clipboard = clipboard;
        _nodeKinds = nodeKinds;
    }

    public DiagramDocumentViewModel Create(DiagramDocument document, string? filePath)
        => new(
            document,
            new MementoUndoService(_serializer, _undoOptions.Value),
            _router,
            _serializer,
            _editorOptions.Value,
            _theme,
            _clipboard,
            _nodeKinds,
            filePath);

    public DiagramDocumentViewModel CreateNew(DiagramType type)
    {
        DiagramDocument document = DiagramDocument.CreateEmpty(type);
        document.DefaultShapeStyle = StylePalette.Default.ToShapeStyle(_theme.IsDark);

        if (type == DiagramType.MindMap)
        {
            SeedCentralTopic(document);
        }

        return Create(document, filePath: null);
    }

    // A new mind map opens with one central topic — the taper root — placed near the top-left of the
    // initial viewport (pan 0 / zoom 1), ready for the user to rename and grow with the hover '+' buttons.
    private void SeedCentralTopic(DiagramDocument document)
    {
        EditorOptions options = _editorOptions.Value;
        double w = options.DefaultShapeWidth;
        double h = options.DefaultShapeHeight;
        const double centerX = 320d;
        const double centerY = 240d;
        document.Nodes.Add(new ShapeNode
        {
            Kind = ShapeKind.MindMapTopicRounded,
            Text = "Central topic",
            Bounds = new Rect2D(centerX - (w / 2d), centerY - (h / 2d), w, h),
            Style = document.DefaultShapeStyle.Clone(),
        });
    }
}
