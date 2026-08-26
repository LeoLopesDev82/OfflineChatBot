namespace OfflineChatBot.Services.Abstractions
{
    public interface IDocumentTextExtractor
    {
        bool CanHandle(string filePath);

        Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default);
    }
}
