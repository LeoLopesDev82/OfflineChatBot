using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeDocumentStore : IDocumentStore
    {
        public Dictionary<string, string> Stored { get; } = new Dictionary<string, string>();
        public List<string> Deleted { get; } = new List<string>();

        public Task SaveAsync(string sessionId, string text)
        {
            Stored[sessionId] = text;

            return Task.CompletedTask;
        }

        public Task<string?> LoadAsync(string sessionId)
        {
            return Task.FromResult(Stored.GetValueOrDefault(sessionId));
        }

        public Task DeleteAsync(string sessionId)
        {
            Deleted.Add(sessionId);
            Stored.Remove(sessionId);

            return Task.CompletedTask;
        }
    }
}
