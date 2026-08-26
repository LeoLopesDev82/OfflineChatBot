using System.IO;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class PlainTextExtractor : IDocumentTextExtractor
    {
        private static readonly string[] Extensions = { ".txt", ".md", ".csv", ".log" };

        public bool CanHandle(string filePath)
        {
            return Extensions.Contains(Path.GetExtension(filePath), StringComparer.OrdinalIgnoreCase);
        }

        public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return File.ReadAllTextAsync(filePath, cancellationToken);
        }
    }
}
