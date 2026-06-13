using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Draw.Model.Connectors;
using Draw.Model.Documents;
using Draw.Model.Nodes;
using Draw.Model.Styling;

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
    /// Upgrades run one version at a time, so a multi-version-old file steps through each transform; add
    /// the next as <c>if (document.SchemaVersion &lt;= N) UpgradeVNToVN1(document);</c> below the last.
    /// </summary>
    private static void Migrate(DiagramDocument document)
    {
        if (document.SchemaVersion >= DocumentSchema.CurrentVersion)
        {
            return;
        }

        if (document.SchemaVersion <= 1)
        {
            UpgradeV1ToV2(document);
        }

        // The final step always stamps to the current version.
        document.SchemaVersion = DocumentSchema.CurrentVersion;
    }

    /// <summary>
    /// v1 → v2: v1 stored the theme-following default fill/text as their literal colour values (the old
    /// value-equality sentinel); v2 represents "follow the theme" as a null colour. Null any colour that
    /// exactly equals a legacy default so a migrated document keeps following the theme rather than pinning
    /// that colour. (A v1 user who deliberately picked exactly the default colour as a custom value can't
    /// be distinguished — but v1 couldn't represent that distinctly either; this is the ambiguity v2 removes.)
    /// </summary>
    private static void UpgradeV1ToV2(DiagramDocument document)
    {
        foreach (NodeBase node in document.Nodes)
        {
            if (node.Style.Fill == ShapeStyle.DefaultFill)
            {
                node.Style.Fill = null;
            }

            if (node.Style.Font.Color == FontSpec.DefaultColor)
            {
                node.Style.Font.Color = null;
            }
        }

        foreach (Connector connector in document.Connectors)
        {
            if (connector.Style.Font.Color == FontSpec.DefaultColor)
            {
                connector.Style.Font.Color = null;
            }
        }
    }
}
