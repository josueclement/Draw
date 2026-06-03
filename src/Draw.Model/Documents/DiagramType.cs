namespace Draw.Model.Documents;

/// <summary>
/// The kind of diagram a document holds. Chosen at creation; it sets the default
/// palette but does not restrict which node types the canvas accepts.
/// </summary>
public enum DiagramType
{
    Freeform = 0,
    Class = 1,
    UseCase = 2,
    Er = 3,
}
