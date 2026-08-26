namespace OfflineChatBot.Services.Abstractions
{
    public interface IEmbeddingService
    {
        bool IsLoaded { get; }

        Task LoadAsync(string modelPath, CancellationToken cancellationToken = default);
        Task UnloadAsync();

        Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
    }
}
