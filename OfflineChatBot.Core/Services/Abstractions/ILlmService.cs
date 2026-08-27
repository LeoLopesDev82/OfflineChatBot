using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface ILlmService
    {
        bool IsLoaded { get; }
        string LoadedModelPath { get; }

        bool UseGpu { get; set; }
        BackendStatus Backend { get; }
        double LastTokensPerSecond { get; }

        Task LoadModelAsync(string modelPath, string? visionProjectionPath = null, CancellationToken cancellationToken = default);
        Task UnloadModelAsync();

        Task<string> CompleteAsync(string question, string content, CancellationToken cancellationToken = default);

        IAsyncEnumerable<string> GenerateResponseStreamAsync(
            string conversationId,
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            string documentContext = "",
            CancellationToken cancellationToken = default);
    }
}