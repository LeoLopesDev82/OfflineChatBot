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
        public bool UseGpu { get; set; }
        public BackendStatus Backend { get; set; } = BackendStatus.Cpu;
        public double LastTokensPerSecond { get; set; }
        public Exception? LoadFailure { get; set; }
        public string LoadedModelPath { get; set; } = FakeModelManagerService.ModelPath;

        public string? LastPrompt { get; private set; }
        public string? LastImagePath { get; private set; }
        public string LastDocumentContext { get; private set; } = string.Empty;
        public string LastConversationId { get; private set; } = string.Empty;
        public List<string> CompletedParts { get; } = new List<string>();
        public string CompletionAnswer { get; set; } = "NOTHING";
        public List<ChatMessage> LastHistory { get; private set; } = new List<ChatMessage>();
        public int LoadCount { get; private set; }
        public int UnloadCount { get; private set; }

        public Task LoadModelAsync(string modelPath, string? visionProjectionPath = null, CancellationToken cancellationToken = default)
        {
            LoadCount++;

            if (LoadFailure != null)
                throw LoadFailure;

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

        public Task<string> CompleteAsync(string question, string content, CancellationToken cancellationToken = default)
        {
            CompletedParts.Add(content);

            return Task.FromResult(CompletionAnswer);
        }

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
            string conversationId,
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            string documentContext = "",
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            LastConversationId = conversationId;
            LastHistory = history.ToList();
            LastPrompt = userPrompt;
            LastImagePath = imagePath;
            LastDocumentContext = documentContext;

            foreach (var token in _tokens)
            {
                cancellationToken.ThrowIfCancellationRequested();

                yield return token;

                await Task.Yield();
            }
        }
    }
}