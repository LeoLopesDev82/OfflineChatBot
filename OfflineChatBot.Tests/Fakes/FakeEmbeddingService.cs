using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeEmbeddingService : IEmbeddingService
    {
        public bool IsLoaded { get; private set; }
        public string? LoadedPath { get; private set; }

        public Task LoadAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            IsLoaded = true;
            LoadedPath = modelPath;

            return Task.CompletedTask;
        }

        public Task UnloadAsync()
        {
            IsLoaded = false;

            return Task.CompletedTask;
        }

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { text.Length / 100f, 1f, 0f });
        }
    }
}
