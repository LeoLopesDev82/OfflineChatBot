using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeChatStorageService : IChatStorageService
    {
        public List<ChatSession> Stored { get; set; } = new List<ChatSession>();
        public int SaveCount { get; private set; }

        public Task<List<ChatSession>> LoadSessionsAsync()
        {
            return Task.FromResult(Stored.ToList());
        }

        public Task SaveSessionsAsync(IEnumerable<ChatSession> sessions)
        {
            SaveCount++;
            Stored = sessions.ToList();

            return Task.CompletedTask;
        }
    }
}
