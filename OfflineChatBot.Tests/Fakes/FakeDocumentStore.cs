using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeDocumentStore : IDocumentStore
    {
        public Dictionary<string, IndexedDocument> Stored { get; } = new Dictionary<string, IndexedDocument>();
        public List<string> Deleted { get; } = new List<string>();

        public Task SaveAsync(string sessionId, IndexedDocument document)
        {
            Stored[sessionId] = document;

            return Task.CompletedTask;
        }

        public Task<IndexedDocument?> LoadAsync(string sessionId)
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
