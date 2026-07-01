using Avalonia;
using Avalonia.Media;
using PhosphorIconsAvalonia;

namespace Draw.App.Rendering;

/// <summary>
/// Shared glyph-geometry lookups so the diagram context menu, the ribbon and the align/distribute
/// palette all draw their icons from one source: Phosphor icons (regular weight) and the local
/// <c>ToolIcon.*</c> geometries merged from <c>Resources/ToolIcons.axaml</c>.
/// </summary>
public static class IconGeometry
{
    /// <summary>A Phosphor (regular weight) icon as a fill geometry.</summary>
    public static Geometry Phosphor(Icon icon) => IconService.CreateGeometry(icon, IconType.regular);

    /// <summary>A <c>ToolIcon.*</c> geometry from the merged application resources, or null if absent.</summary>
    public static Geometry? Tool(string key)
        => Application.Current!.TryGetResource(key, null, out object? value) ? value as Geometry : null;
}
