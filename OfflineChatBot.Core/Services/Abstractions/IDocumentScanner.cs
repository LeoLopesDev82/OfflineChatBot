using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface IDocumentScanner
    {
        Task<string> ScanAsync(
            ReadDocument document,
            string question,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
