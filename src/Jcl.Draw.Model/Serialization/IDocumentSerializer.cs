using Jcl.Draw.Model.Documents;

namespace Jcl.Draw.Model.Serialization;

/// <summary>Converts documents to/from their on-disk JSON representation and deep-clones them.</summary>
public interface IDocumentSerializer
{
    /// <summary>Serializes a document to indented JSON.</summary>
    string Serialize(DiagramDocument document);

    /// <summary>
    /// Deserializes a document, validating its schema version.
    /// </summary>
    /// <exception cref="InvalidDocumentException">The JSON is invalid or empty.</exception>
    /// <exception cref="UnsupportedSchemaVersionException">The document is from a newer schema.</exception>
    DiagramDocument Deserialize(string json);

    /// <summary>Produces a deep, independent copy (used by memento undo/redo).</summary>
    DiagramDocument Clone(DiagramDocument document);
}
