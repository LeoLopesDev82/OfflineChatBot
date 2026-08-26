using System.IO;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class CompositeTextExtractor : IDocumentTextExtractor
    {
        private readonly IReadOnlyList<IDocumentTextExtractor> _extractors;

        public CompositeTextExtractor(IEnumerable<IDocumentTextExtractor> extractors)
        {
            _extractors = extractors.ToList();
        }

        public bool CanHandle(string filePath)
        {
            return _extractors.Any(extractor => extractor.CanHandle(filePath));
        }

        public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var extractor = _extractors.FirstOrDefault(candidate => candidate.CanHandle(filePath));

            if (extractor == null)
                throw new NotSupportedException($"There is no reader for {Path.GetExtension(filePath)} files.");

            return extractor.ExtractAsync(filePath, cancellationToken);
        }
    }
}
