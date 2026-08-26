using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class DocumentIndexService : IDocumentIndexService
    {
        private const string PassagePrefix = "passage: ";
        private const string QueryPrefix = "query: ";

        private readonly IDocumentTextExtractor _extractor;
        private readonly IEmbeddingService _embeddings;
        private readonly TextChunker _chunker;
        private readonly DocumentOptions _options;
        private readonly ILogger<DocumentIndexService> _logger;

        public DocumentIndexService(
            IDocumentTextExtractor extractor,
            IEmbeddingService embeddings,
            TextChunker chunker,
            IOptions<DocumentOptions> options,
            ILogger<DocumentIndexService> logger)
        {
            _extractor = extractor;
            _embeddings = embeddings;
            _chunker = chunker;
            _options = options.Value;
            _logger = logger;
        }

        public bool CanRead(string filePath)
        {
            return _extractor.CanHandle(filePath);
        }

        public async Task<IndexedDocument> IndexAsync(string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            var text = await _extractor.ExtractAsync(filePath, cancellationToken);

            EnsureReadable(text, filePath);

            var chunks = _chunker.Split(text, _options.ChunkTokens, _options.OverlapTokens);
            var indexed = new List<IndexedChunk>();

            foreach (var chunk in chunks)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var embedding = await _embeddings.EmbedAsync(PassagePrefix + chunk.Text, cancellationToken);

                indexed.Add(new IndexedChunk(chunk.Index, chunk.Text, embedding));

                progress?.Report((double)indexed.Count / chunks.Count * 100);
            }

            _logger.LogInformation("Indexed {FileName} into {ChunkCount} chunks", Path.GetFileName(filePath), indexed.Count);

            return new IndexedDocument { Name = Path.GetFileName(filePath), Chunks = indexed };
        }

        public async Task<IReadOnlyList<IndexedChunk>> FindRelevantAsync(
            IndexedDocument document,
            string question,
            int count,
            CancellationToken cancellationToken = default)
        {
            var asked = await _embeddings.EmbedAsync(QueryPrefix + question, cancellationToken);

            return document.Chunks
                .OrderByDescending(chunk => Similarity(asked, chunk.Embedding))
                .Take(count)
                .OrderBy(chunk => chunk.Index)
                .ToList();
        }

        #region Private Methods

        private static void EnsureReadable(string text, string filePath)
        {
            if (text.Trim().Length >= 1)
                return;

            throw new InvalidOperationException(
                $"No text could be read from {Path.GetFileName(filePath)}. Scanned documents need character recognition, which is not supported.");
        }

        private static float Similarity(float[] question, float[] chunk)
        {
            var total = 0f;

            for (var index = 0; index < question.Length && index < chunk.Length; index++)
                total += question[index] * chunk[index];

            return total;
        }

        #endregion
    }
}
