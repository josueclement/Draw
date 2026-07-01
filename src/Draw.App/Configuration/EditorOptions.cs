namespace Draw.App.Configuration;

/// <summary>User-tunable editor behavior, bound from configuration via <c>IOptions</c>.</summary>
public sealed class EditorOptions
{
    public const string SectionName = "Editor";

    /// <summary>Grid spacing in document units.</summary>
    public double GridSize { get; set; } = 10d;

    /// <summary>Whether moves/resizes snap to the grid.</summary>
    public bool SnapToGrid { get; set; } = true;

    /// <summary>Default size for newly created shapes.</summary>
    public double DefaultShapeWidth { get; set; } = 120d;

    public double DefaultShapeHeight { get; set; } = 70d;

    /// <summary>Minimum zoom factor (1.0 == 100%).</summary>
    public double MinZoom { get; set; } = 0.1d;

    public double MaxZoom { get; set; } = 8d;

    /// <summary>
    /// When on (the default), connectors spread out automatically instead of stacking on a side's
    /// midpoint: a new connector's end takes a free slot on its side, and duplicating/pasting shapes
    /// re-spaces the connectors on the affected shapes. Turn off to keep every end on the side centre.
    /// </summary>
    public bool AutoSpaceConnectors { get; set; } = true;
}
