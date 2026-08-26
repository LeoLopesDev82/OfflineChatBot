using System.IO;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeDocumentIndexService : IDocumentIndexService
    {
        public Exception? IndexFailure { get; set; }
        public string? LastQuestion { get; private set; }
        public TaskCompletionSource? Gate { get; set; }

        public bool CanRead(string filePath)
        {
            return true;
        }

        public async Task<IndexedDocument> IndexAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (IndexFailure != null)
                throw IndexFailure;

            if (Gate != null)
                await Gate.Task;

            progress?.Report(100);

            var chunks = new[]
            {
                new IndexedChunk(0, "The delivery takes thirty days.", [1f, 0f, 0f]),
                new IndexedChunk(1, "The warranty lasts twelve months.", [0f, 1f, 0f])
            };

            return new IndexedDocument { Name = Path.GetFileName(filePath), Chunks = chunks };
        }

        public Task<IReadOnlyList<IndexedChunk>> FindRelevantAsync(
            IndexedDocument document,
            string question,
            int count,
            CancellationToken cancellationToken = default)
        {
            LastQuestion = question;

            return Task.FromResult<IReadOnlyList<IndexedChunk>>(document.Chunks.Take(count).ToList());
        }
    }
}
