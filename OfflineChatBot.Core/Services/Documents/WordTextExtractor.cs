using System.IO;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class WordTextExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".docx", StringComparison.OrdinalIgnoreCase);
        }

        public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Extract(filePath, cancellationToken), cancellationToken);
        }

        #region Private Methods

        private static string Extract(string filePath, CancellationToken cancellationToken)
        {
            using var document = WordprocessingDocument.Open(filePath, false);

            var body = document.MainDocumentPart?.Document.Body;

            if (body == null)
                return string.Empty;

            var builder = new StringBuilder();

            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                cancellationToken.ThrowIfCancellationRequested();

                builder.AppendLine(paragraph.InnerText);
            }

            return builder.ToString();
        }

        #endregion
    }
}
