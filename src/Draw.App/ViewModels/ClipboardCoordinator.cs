using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Draw.App.Services;
using Draw.Diagramming.Geometry;
using Draw.Diagramming.Layout;
using Draw.Model.Connectors;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Primitives;
using Draw.Model.Serialization;

namespace Draw.App.ViewModels;

/// <summary>
/// Clipboard and cloning operations for one diagram tab: copy / cut / paste / duplicate, plus image
/// insertion (paste of an external bitmap, file insert, drag-drop). Drives the document through
/// <see cref="IDocumentEditContext"/>; the pure clone/translate/restack lives in
/// <see cref="CloneArranger"/>.
/// </summary>
public sealed class ClipboardCoordinator
{
    private readonly IDocumentEditContext _context;
    private readonly IDocumentSerializer _serializer;
    private readonly IClipboardService _clipboard;

    public ClipboardCoordinator(IDocumentEditContext context, IDocumentSerializer serializer, IClipboardService clipboard)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
    }

    /// <summary>Copies the selected nodes (and any connector whose endpoints are both selected) to the
    /// clipboard. A lone selected image also writes a bitmap so other apps can paste it. No-op without
    /// a node selection; never mutates the document.</summary>
    public async Task CopySelectionAsync()
    {
        List<NodeViewModelBase> selected = _context.SelectedNodes.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        HashSet<Guid> ids = selected.Select(n => n.Id).ToHashSet();
        DiagramDocument clip = DiagramDocument.CreateEmpty(_context.Document.DiagramType);
        foreach (NodeViewModelBase vm in selected)
        {
            clip.Nodes.Add(vm.Model.Clone());
        }

        foreach (Connector connector in _context.Document.Connectors)
        {
            if (ids.Contains(connector.SourceNodeId) && ids.Contains(connector.TargetNodeId))
            {
                clip.Connectors.Add(connector.Clone());
            }
        }

        string json = _serializer.Serialize(clip);
        Bitmap? bitmap = selected is [ImageNodeViewModel image] ? image.Image : null;
        await _clipboard.SetClipAsync(json, bitmap);
    }

    /// <summary>Copies the selection, then deletes it (one undo step from the delete).</summary>
    public async Task CutSelectionAsync()
    {
        if (!_context.SelectedNodes.Any())
        {
            return;
        }

        await CopySelectionAsync();
        _context.DeleteSelected();
    }

    /// <summary>Pastes the Draw clipboard payload centred on the viewport; falling back to a bitmap on
    /// the clipboard (e.g. an external screenshot), which becomes an image node. No-op otherwise.</summary>
    public async Task PasteAsync()
    {
        string? json = await _clipboard.TryGetClipAsync();
        if (json is not null)
        {
            DiagramDocument clip;
            try
            {
                clip = _serializer.Deserialize(json);
            }
            catch (DocumentSerializationException)
            {
                return;
            }

            if (clip.Nodes.Count == 0)
            {
                return;
            }

            Rect2D bounds = UnionBounds(clip.Nodes);
            Point2D centre = _context.ViewportCenterWorld();
            Point2D delta = new(centre.X - bounds.Center.X, centre.Y - bounds.Center.Y);
            PlaceClones(clip.Nodes, clip.Connectors, delta);
            return;
        }

        Bitmap? bitmap = await _clipboard.TryGetBitmapAsync();
        if (bitmap is not null)
        {
            byte[] data = EncodePng(bitmap);
            bitmap.Dispose();
            AddImageNode(_context.ViewportCenterWorld(), data, "png");
        }
    }

    /// <summary>Clones the selection in place with a small offset (no clipboard). One undo step. When
    /// <paramref name="includeConnectors"/> is true, any connector touching the selection is duplicated too:
    /// one between two selected shapes reconnects to both clones, while a "boundary" connector to a
    /// non-selected shape keeps that original neighbour. When false, only the shapes are cloned.</summary>
    public void DuplicateSelection(bool includeConnectors)
    {
        List<NodeViewModelBase> selected = _context.SelectedNodes.ToList();
        if (selected.Count == 0)
        {
            return;
        }

        List<NodeBase> nodes = selected.Select(n => n.Model).ToList();
        List<Connector> connectors;
        if (includeConnectors)
        {
            HashSet<Guid> ids = selected.Select(n => n.Id).ToHashSet();
            connectors = _context.Document.Connectors
                .Where(c => ids.Contains(c.SourceNodeId) || ids.Contains(c.TargetNodeId))
                .ToList();
        }
        else
        {
            connectors = new List<Connector>();
        }

        double offset = _context.GridSize > 0 ? _context.GridSize : 16d;
        PlaceClones(nodes, connectors, new Point2D(offset, offset));
    }

    /// <summary>Inserts an image node centred on <paramref name="centre"/>, sized from the image's native
    /// pixels (capped to the viewport). One undo step. Used by paste, file insert and drag-drop.</summary>
    public ImageNodeViewModel AddImageNode(Point2D centre, byte[] data, string format)
    {
        _context.CaptureUndo();

        (int pixelWidth, int pixelHeight) = DecodePixelSize(data);
        (double w, double h) = InitialImageSize(pixelWidth, pixelHeight);
        Rect2D bounds = new(centre.X - (w / 2), centre.Y - (h / 2), w, h);
        if (_context.SnapEnabled)
        {
            bounds = bounds.PositionSnappedToGrid(_context.GridSize);
        }

        ImageNode node = new()
        {
            Data = data,
            Format = format,
            PixelWidth = pixelWidth,
            PixelHeight = pixelHeight,
            Bounds = bounds,
            Style = _context.Document.DefaultShapeStyle.Clone(),
            ZIndex = _context.NextZIndex(),
        };

        _context.Document.Nodes.Add(node);
        ImageNodeViewModel vm = (ImageNodeViewModel)_context.CreateNodeViewModel(node);
        _context.Nodes.Add(vm);
        _context.SelectNodes(new NodeViewModelBase[] { vm });
        _context.MarkModified();
        return vm;
    }

    /// <summary>Inserts an image centred on the current viewport (for the file-picker entry point).</summary>
    public ImageNodeViewModel AddImageAtViewportCenter(byte[] data, string format)
        => AddImageNode(_context.ViewportCenterWorld(), data, format);

    // Clones the given source nodes + connectors into the document with fresh ids, translated by
    // <paramref name="delta"/>, and makes the result the selection. One undo step. Connector endpoints are
    // repointed at their clone, or kept on the original node when it isn't part of the batch (see
    // CloneArranger); a connector resolving to neither is skipped.
    private void PlaceClones(IReadOnlyList<NodeBase> sourceNodes, IReadOnlyList<Connector> sourceConnectors, Point2D delta)
    {
        if (sourceNodes.Count == 0)
        {
            return;
        }

        _context.CaptureUndo();

        // Fresh ids, translated/snapped bounds and a restacked ZIndex are computed in the (testable)
        // Diagramming layer; this coordinator owns only the document/collection wiring below.
        CloneArranger.ClonedGraph cloned = CloneArranger.Clone(
            sourceNodes, sourceConnectors, _context.Document.Nodes, delta, _context.SnapEnabled ? _context.GridSize : null);

        List<NodeViewModelBase> pasted = new();
        foreach (NodeBase clone in cloned.Nodes)
        {
            _context.Document.Nodes.Add(clone);
            NodeViewModelBase vm = _context.CreateNodeViewModel(clone);

            // A boundary draws behind the nodes it encloses (same rule as AddUseCaseNode).
            if (clone is SystemBoundaryNode)
            {
                _context.Nodes.Insert(0, vm);
            }
            else
            {
                _context.Nodes.Add(vm);
            }

            pasted.Add(vm);
        }

        HashSet<Guid> connectedShapes = new();
        foreach (Connector clone in cloned.Connectors)
        {
            _context.Document.Connectors.Add(clone);
            // Both endpoints: covers the cloned shapes and any non-selected neighbour a boundary
            // connector now reconnects to (that neighbour gained an extra end).
            connectedShapes.Add(clone.SourceNodeId);
            connectedShapes.Add(clone.TargetNodeId);
        }

        _context.RebuildConnectors();

        // Fan the just-cloned connectors out over the shapes they touch (a no-op when the auto-space
        // option is off). The undo snapshot captured above already precedes these anchor changes, so the
        // re-spacing folds into the same single undo step as the duplicate/paste.
        if (connectedShapes.Count > 0)
        {
            _context.AutoSpaceConnectorsForShapes(connectedShapes);
        }

        _context.SelectNodes(pasted);
        _context.MarkModified();
    }

    private static Rect2D UnionBounds(IReadOnlyList<NodeBase> nodes)
    {
        Rect2D union = nodes[0].Bounds;
        for (int i = 1; i < nodes.Count; i++)
        {
            union = union.Union(nodes[i].Bounds);
        }

        return union;
    }

    private (double Width, double Height) InitialImageSize(int pixelWidth, int pixelHeight)
    {
        const double fallback = 200d;
        double w = pixelWidth > 0 ? pixelWidth : fallback;
        double h = pixelHeight > 0 ? pixelHeight : fallback;

        // Cap to ~80% of the visible viewport (converted to world units) so a large image doesn't
        // paste bigger than the canvas; preserve aspect ratio.
        double zoom = _context.Zoom <= 0 ? 1d : _context.Zoom;
        double maxW = _context.ViewportWidth > 0 ? _context.ViewportWidth * 0.8d / zoom : w;
        double maxH = _context.ViewportHeight > 0 ? _context.ViewportHeight * 0.8d / zoom : h;
        double scale = Math.Min(1d, Math.Min(maxW / w, maxH / h));
        return (w * scale, h * scale);
    }

    private static (int Width, int Height) DecodePixelSize(byte[] data)
    {
        if (data.Length == 0)
        {
            return (0, 0);
        }

        // Untrusted bytes: a decode failure must not crash the insert — fall back to a default size.
        try
        {
            using MemoryStream stream = new(data);
            using Bitmap bitmap = new(stream);
            return (bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        }
        catch (Exception)
        {
            return (0, 0);
        }
    }

    private static byte[] EncodePng(Bitmap bitmap)
    {
        using MemoryStream stream = new();
        bitmap.Save(stream);
        return stream.ToArray();
    }
}
