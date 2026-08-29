using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeChatStorageService : IChatStorageService
    {
        private int _running;

        public List<ChatSession> Stored { get; set; } = new List<ChatSession>();
        public int SaveCount { get; private set; }
        public int EnteredSaves { get; private set; }
        public int MaxConcurrentSaves { get; private set; }
        public TaskCompletionSource? Gate { get; set; }

        public Task<List<ChatSession>> LoadSessionsAsync()
        {
            return Task.FromResult(Stored.ToList());
        }

        public async Task SaveSessionsAsync(IEnumerable<ChatSession> sessions)
        {
            EnteredSaves++;
            MaxConcurrentSaves = Math.Max(MaxConcurrentSaves, Interlocked.Increment(ref _running));

            if (Gate != null)
                await Gate.Task;

            SaveCount++;
            Stored = sessions.ToList();

            Interlocked.Decrement(ref _running);
        }
    }
}
