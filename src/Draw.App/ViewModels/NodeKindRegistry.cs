using System;
using System.Collections.Generic;
using System.Linq;
using Draw.App.Services;
using Draw.Model.Nodes;

namespace Draw.App.ViewModels;

/// <summary>
/// The single registration point for node kinds: it pairs each <see cref="NodeBase"/> subtype with its
/// view-model factory, default placement size and stacking band. Replaces the runtime-throwing
/// type switch in <c>DiagramDocumentViewModel.CreateNodeViewModel</c>; a node kind without an entry
/// fails <see cref="ValidateCoverage"/> at construction (DI/startup) rather than silently at runtime.
/// </summary>
public sealed class NodeKindRegistry
{
    private readonly IReadOnlyDictionary<Type, NodeKindDescriptor> _byModelType;

    public NodeKindRegistry()
    {
        NodeKindDescriptor[] descriptors =
        {
            new(typeof(ShapeNode), 120d, 70d, NodeZBand.Ordinary,
                (m, _, t) => new ShapeNodeViewModel((ShapeNode)m, t)),
            new(typeof(ClassNode), 170d, 110d, NodeZBand.Ordinary,
                (m, ctx, t) => new ClassNodeViewModel((ClassNode)m, ctx, t)),
            new(typeof(EntityNode), 180d, 120d, NodeZBand.Ordinary,
                (m, ctx, t) => new EntityNodeViewModel((EntityNode)m, ctx, t)),
            new(typeof(ActorNode), 48d, 84d, NodeZBand.Ordinary,
                (m, _, t) => new ActorNodeViewModel((ActorNode)m, t)),
            new(typeof(UseCaseNode), 130d, 72d, NodeZBand.Ordinary,
                (m, _, t) => new UseCaseNodeViewModel((UseCaseNode)m, t)),
            new(typeof(SystemBoundaryNode), 320d, 220d, NodeZBand.Background,
                (m, _, t) => new SystemBoundaryNodeViewModel((SystemBoundaryNode)m, t)),
            new(typeof(ImageNode), 200d, 200d, NodeZBand.Ordinary,
                (m, _, t) => new ImageNodeViewModel((ImageNode)m, t)),
        };

        _byModelType = descriptors.ToDictionary(d => d.ModelType);
        ValidateCoverage();
    }

    /// <summary>The descriptor for <paramref name="node"/>'s concrete type.</summary>
    public NodeKindDescriptor For(NodeBase node) => _byModelType[node.GetType()];

    /// <summary>Builds the view model matching <paramref name="node"/>'s concrete type.</summary>
    public NodeViewModelBase CreateViewModel(NodeBase node, INodeEditContext context, IThemeService theme)
        => For(node).CreateViewModel(node, context, theme);

    // Fail loudly at construction (a DI singleton, so at startup) if a concrete NodeBase subtype has no
    // descriptor — the closest App-layer equivalent to a compile-time net, surfacing a forgotten
    // registration immediately rather than as a runtime cast/lookup failure when the kind is first placed.
    private void ValidateCoverage()
    {
        foreach (Type kind in typeof(NodeBase).Assembly.GetTypes()
            .Where(t => typeof(NodeBase).IsAssignableFrom(t) && !t.IsAbstract))
        {
            if (!_byModelType.ContainsKey(kind))
            {
                throw new InvalidOperationException(
                    $"NodeKindRegistry has no descriptor for node kind '{kind.Name}'. Add an entry in NodeKindRegistry.");
            }
        }
    }
}
