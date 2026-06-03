using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Draw.Model.Documents;

namespace Draw.Model.Serialization;

/// <summary>System.Text.Json implementation of <see cref="IDocumentSerializer"/>.</summary>
public sealed class JsonDocumentSerializer : IDocumentSerializer
{
    private readonly JsonSerializerOptions _options;

    public JsonDocumentSerializer()
    {
        _options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Converters =
            {
                new JsonStringEnumConverter(),
                new ArgbColorJsonConverter(),
            },
        };
    }

    public string Serialize(DiagramDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return JsonSerializer.Serialize(document, _options);
    }

    public DiagramDocument Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidDocumentException("Document content is empty.");
        }

        DiagramDocument? document;
        try
        {
            document = JsonSerializer.Deserialize<DiagramDocument>(json, _options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDocumentException("Document JSON is malformed.", ex);
        }

        if (document is null)
        {
            throw new InvalidDocumentException("Document JSON deserialized to null.");
        }

        if (document.SchemaVersion > DocumentSchema.CurrentVersion)
        {
            throw new UnsupportedSchemaVersionException(document.SchemaVersion, DocumentSchema.CurrentVersion);
        }

        return document;
    }

    public DiagramDocument Clone(DiagramDocument document) => Deserialize(Serialize(document));
}
