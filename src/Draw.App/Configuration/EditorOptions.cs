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
}
