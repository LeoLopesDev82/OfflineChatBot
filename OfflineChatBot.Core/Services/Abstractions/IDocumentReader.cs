using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface IDocumentReader
    {
        bool CanRead(string filePath);

        Task<ReadDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default);

        ReadDocument Measure(string name, string text);
    }
}
