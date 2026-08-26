using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface IDocumentIndexService
    {
        bool CanRead(string filePath);

        Task<IndexedDocument> IndexAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<IndexedChunk>> FindRelevantAsync(IndexedDocument document, string question, int count, CancellationToken cancellationToken = default);
    }
}
