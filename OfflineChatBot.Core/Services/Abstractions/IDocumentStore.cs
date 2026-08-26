namespace OfflineChatBot.Services.Abstractions
{
    public interface IDocumentStore
    {
        Task SaveAsync(string sessionId, string text);
        Task<string?> LoadAsync(string sessionId);
        Task DeleteAsync(string sessionId);
    }
}
