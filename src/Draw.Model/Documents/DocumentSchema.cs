namespace Draw.Model.Documents;

/// <summary>Versioning constants for the on-disk document schema.</summary>
public static class DocumentSchema
{
    /// <summary>The schema version written by this build. v2: default fill/text colours are stored as a
    /// null colour ("follow the theme") instead of the literal default value (see JsonDocumentSerializer).</summary>
    public const int CurrentVersion = 2;
}
