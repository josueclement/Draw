using Draw.App.Configuration;
using Draw.App.Services;
using Draw.Diagramming.Routing;
using Draw.Diagramming.Undo;
using Draw.Model.Documents;
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

    public DiagramDocumentViewModelFactory(
        IDocumentSerializer serializer,
        IConnectorRouter router,
        IOptions<EditorOptions> editorOptions,
        IOptions<UndoOptions> undoOptions,
        IThemeService theme)
    {
        _serializer = serializer;
        _router = router;
        _editorOptions = editorOptions;
        _undoOptions = undoOptions;
        _theme = theme;
    }

    public DiagramDocumentViewModel Create(DiagramDocument document, string? filePath)
        => new(
            document,
            new MementoUndoService(_serializer, _undoOptions.Value),
            _router,
            _serializer,
            _editorOptions.Value,
            _theme,
            filePath);

    public DiagramDocumentViewModel CreateNew(DiagramType type)
        => Create(DiagramDocument.CreateEmpty(type), filePath: null);
}
