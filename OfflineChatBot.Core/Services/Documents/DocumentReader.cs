using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class DocumentReader : IDocumentReader
    {
        private const int ReservedForQuestionAndWrapper = 512;

        private readonly IDocumentTextExtractor _extractor;
        private readonly ITokenCounter _tokenCounter;
        private readonly GenerationOptions _options;
        private readonly ILogger<DocumentReader> _logger;

        public DocumentReader(
            IDocumentTextExtractor extractor,
            ITokenCounter tokenCounter,
            IOptions<GenerationOptions> options,
            ILogger<DocumentReader> logger)
        {
            _extractor = extractor;
            _tokenCounter = tokenCounter;
            _options = options.Value;
            _logger = logger;
        }

        public int RoomPerPass => (int)_options.ContextSize - _options.MaxNoteTokens - ReservedForQuestionAndWrapper;

        public int RoomForAnswer => (int)_options.ContextSize - _options.MaxTokens - ReservedForQuestionAndWrapper;

        public bool CanRead(string filePath)
        {
            return _extractor.CanHandle(filePath);
        }

        public async Task<ReadDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            var text = await _extractor.ExtractAsync(filePath, cancellationToken);

            EnsureReadable(text, filePath);

            var document = Measure(Path.GetFileName(filePath), text);

            _logger.LogInformation(
                "Read {DocumentName} with {TokenCount} tokens, which needs {PartCount} pass(es) against a context of {ContextSize}",
                document.Name,
                document.Tokens,
                document.Parts,
                _options.ContextSize);

            return document;
        }

        public ReadDocument Measure(string name, string text)
        {
            var tokens = _tokenCounter.Count(text);

            return new ReadDocument
            {
                Name = name,
                Text = text,
                Tokens = tokens,
                Parts = PartsFor(tokens),
                FitsInOnePass = tokens <= RoomForAnswer
            };
        }

        #region Private Methods

        private int PartsFor(int tokens)
        {
            var room = RoomPerPass;

            if (room <= 0)
                return int.MaxValue;

            return (tokens + room - 1) / room;
        }

        private static void EnsureReadable(string text, string filePath)
        {
            if (!string.IsNullOrWhiteSpace(text))
                return;

            throw new InvalidOperationException(
                $"No text could be read from {Path.GetFileName(filePath)}. Scanned documents are images of text and are not supported.");
        }

        #endregion
    }
}
