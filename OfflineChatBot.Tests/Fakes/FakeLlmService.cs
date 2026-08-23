using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeLlmService : ILlmService
    {
        private readonly string[] _tokens;

        public FakeLlmService(params string[] tokens)
        {
            _tokens = tokens;
        }

        public bool IsLoaded { get; set; } = true;
        public string LoadedModelPath { get; set; } = FakeModelManagerService.ModelPath;

        public string? LastPrompt { get; private set; }
        public string? LastImagePath { get; private set; }
        public List<ChatMessage> LastHistory { get; private set; } = new List<ChatMessage>();
        public int LoadCount { get; private set; }
        public int UnloadCount { get; private set; }

        public Task LoadModelAsync(string modelPath, string? visionProjectionPath = null, CancellationToken cancellationToken = default)
        {
            LoadCount++;
            LoadedModelPath = modelPath;
            IsLoaded = true;

            return Task.CompletedTask;
        }

        public Task UnloadModelAsync()
        {
            UnloadCount++;
            IsLoaded = false;

            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastHistory = history.ToList();
            LastPrompt = userPrompt;
            LastImagePath = imagePath;

            foreach (var token in _tokens)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return token;

                await Task.Yield();
            }
        }
    }
}
