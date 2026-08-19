using OfflineChatBot.Models;

namespace OfflineChatBot.Services
{
    public interface IChatStorageService
    {
        Task<List<ChatSession>> LoadSessionsAsync();
        Task SaveSessionsAsync(IEnumerable<ChatSession> sessions);
    }
}