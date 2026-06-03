using System;

namespace Draw.Model.Serialization;

/// <summary>Base type for recoverable document (de)serialization failures.</summary>
public class DocumentSerializationException : Exception
{
    public DocumentSerializationException(string message)
        : base(message)
    {
    }

    public DocumentSerializationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when the JSON is structurally invalid or does not represent a document.</summary>
public sealed class InvalidDocumentException : DocumentSerializationException
{
    public InvalidDocumentException(string message)
        : base(message)
    {
    }

    public InvalidDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Thrown when a document was written by a newer, unsupported schema version.</summary>
public sealed class UnsupportedSchemaVersionException : DocumentSerializationException
{
    public UnsupportedSchemaVersionException(int foundVersion, int supportedVersion)
        : base($"Document schema version {foundVersion} is newer than the supported version {supportedVersion}. Update the application to open it.")
    {
        FoundVersion = foundVersion;
        SupportedVersion = supportedVersion;
    }

    public int FoundVersion { get; }

    public int SupportedVersion { get; }
}
