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

        Migrate(document);
        return document;
    }

    public DiagramDocument Clone(DiagramDocument document) => Deserialize(Serialize(document));

    /// <summary>
    /// Forward-compatibility seam: brings a document written by an older schema up to the current
    /// shape before it reaches the rest of the app, then stamps it to <see cref="DocumentSchema.CurrentVersion"/>.
    /// No transforms are registered yet (nothing predates version 1), so today this only re-stamps the
    /// version. Before the first schema bump, add the upgrades here — walk one version at a time, e.g.:
    /// <code>
    /// switch (document.SchemaVersion)
    /// {
    ///     case 1: UpgradeV1ToV2(document); goto case 2;
    ///     case 2: UpgradeV2ToV3(document); goto case 3;
    /// }
    /// </code>
    /// so an old file is migrated rather than silently mishandled.
    /// </summary>
    private static void Migrate(DiagramDocument document)
    {
        if (document.SchemaVersion >= DocumentSchema.CurrentVersion)
        {
            return;
        }

        // No migrations registered yet (CurrentVersion == 1). Register version-stepped upgrades above
        // this line as the schema evolves; the final step always stamps to the current version.
        document.SchemaVersion = DocumentSchema.CurrentVersion;
    }
}
