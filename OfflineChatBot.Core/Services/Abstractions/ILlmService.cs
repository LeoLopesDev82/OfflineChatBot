using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface ILlmService
    {
        bool IsLoaded { get; }
        string LoadedModelPath { get; }

        Task LoadModelAsync(string modelPath, string? visionProjectionPath = null, CancellationToken cancellationToken = default);
        Task UnloadModelAsync();

        IAsyncEnumerable<string> GenerateResponseStreamAsync(
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            CancellationToken cancellationToken = default);
    }
}