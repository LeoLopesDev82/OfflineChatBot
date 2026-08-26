using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface IDocumentStore
    {
        Task SaveAsync(string sessionId, IndexedDocument document);
        Task<IndexedDocument?> LoadAsync(string sessionId);
        Task DeleteAsync(string sessionId);
    }
}
