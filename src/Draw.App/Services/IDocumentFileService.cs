using System.Threading;
using System.Threading.Tasks;
using Draw.Model.Documents;

namespace Draw.App.Services;

/// <summary>Reads and writes <see cref="DiagramDocument"/> files (the serializer plus disk I/O).</summary>
public interface IDocumentFileService
{
    Task<DiagramDocument> LoadAsync(string path, CancellationToken cancellationToken = default);

    Task SaveAsync(DiagramDocument document, string path, CancellationToken cancellationToken = default);
}
