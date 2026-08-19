using OfflineChatBot.Models;

namespace OfflineChatBot.Services
{
    public interface ILLMService
    {
        bool IsLoaded { get; }
        string LoadedModelPath { get; }

        Task LoadModelAsync(string modelPath, CancellationToken cancellationToken = default);
        Task UnloadModelAsync();

        IAsyncEnumerable<string> GenerateResponseStreamAsync(
            IEnumerable<ChatMessage> history,
            string userPrompt,
            CancellationToken cancellationToken = default);
    }
}