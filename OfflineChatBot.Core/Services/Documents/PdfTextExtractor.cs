using System.IO;
using OfflineChatBot.Services.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace OfflineChatBot.Services.Documents
{
    public sealed class PdfTextExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Extract(filePath, cancellationToken), cancellationToken);
        }

        #region Private Methods

        private static string Extract(string filePath, CancellationToken cancellationToken)
        {
            using var document = PdfDocument.Open(filePath);

            var pages = new List<string>();

            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();

                pages.Add(ContentOrderTextExtractor.GetText(page));
            }

            return string.Join("\n\n", pages);
        }

        #endregion
    }
}
