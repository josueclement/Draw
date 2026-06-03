using System;

namespace Draw.Model.Documents;

/// <summary>Non-visual bookkeeping for a document. Timestamps are stamped by the app via its clock.</summary>
public sealed class DocumentMetadata
{
    public string? Author { get; set; }

    public DateTimeOffset? CreatedUtc { get; set; }

    public DateTimeOffset? ModifiedUtc { get; set; }

    public string? AppVersion { get; set; }

    public DocumentMetadata Clone() => new()
    {
        Author = Author,
        CreatedUtc = CreatedUtc,
        ModifiedUtc = ModifiedUtc,
        AppVersion = AppVersion,
    };
}
