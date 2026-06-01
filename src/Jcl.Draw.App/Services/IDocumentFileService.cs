using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Jcl.Draw.Model.Documents;
using Jcl.Draw.Model.Serialization;

namespace Jcl.Draw.App.Services;

/// <summary>Reads and writes <see cref="DiagramDocument"/> files (the serializer plus disk I/O).</summary>
public interface IDocumentFileService
{
    Task<DiagramDocument> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(DiagramDocument document, string path, CancellationToken cancellationToken = default);
}

public sealed class DocumentFileService : IDocumentFileService
{
    private readonly IDocumentSerializer _serializer;
    private readonly IClock _clock;

    public DocumentFileService(IDocumentSerializer serializer, IClock clock)
    {
        _serializer = serializer;
        _clock = clock;
    }

    public async Task<DiagramDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        string json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return _serializer.Deserialize(json);
    }

    public async Task SaveAsync(DiagramDocument document, string path, CancellationToken cancellationToken = default)
    {
        document.Metadata.CreatedUtc ??= _clock.UtcNow;
        document.Metadata.ModifiedUtc = _clock.UtcNow;
        string json = _serializer.Serialize(document);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }
}
