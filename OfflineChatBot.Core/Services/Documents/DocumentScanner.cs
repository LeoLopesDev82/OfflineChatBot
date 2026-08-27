using System.Diagnostics;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class DocumentScanner : IDocumentScanner
    {
        private const string NothingFound = "NOTHING";
        private const int MaxCondenseRounds = 2;

        private readonly ILlmService _llmService;
        private readonly IDocumentReader _reader;
        private readonly TextSplitter _splitter;
        private readonly ITokenCounter _tokenCounter;
        private readonly ILogger<DocumentScanner> _logger;

        public DocumentScanner(
            ILlmService llmService,
            IDocumentReader reader,
            TextSplitter splitter,
            ITokenCounter tokenCounter,
            ILogger<DocumentScanner> logger)
        {
            _llmService = llmService;
            _reader = reader;
            _splitter = splitter;
            _tokenCounter = tokenCounter;
            _logger = logger;
        }

        public async Task<string> ScanAsync(
            ReadDocument document,
            string question,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var watch = Stopwatch.StartNew();
            var notes = await CollectAsync(document.Text, question, progress, cancellationToken);

            notes = Introduce(await CondenseAsync(notes, question, cancellationToken), document.Parts);

            _logger.LogInformation(
                "Scanned {DocumentName} in {Elapsed:F0}s, ending with {TokenCount} tokens of notes",
                document.Name,
                watch.Elapsed.TotalSeconds,
                _tokenCounter.Count(notes));

            return notes;
        }

        #region Private Methods

        private async Task<string> CollectAsync(
            string text,
            string question,
            IProgress<ScanProgress>? progress,
            CancellationToken cancellationToken)
        {
            var parts = _splitter.Split(text, _reader.RoomPerPass);
            var notes = new List<string>();
            var watch = Stopwatch.StartNew();

            for (var index = 0; index < parts.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                progress?.Report(new ScanProgress(index + 1, parts.Count, Remaining(watch.Elapsed, index, parts.Count)));

                var found = await _llmService.CompleteAsync(Instruction(question, index + 1, parts.Count), parts[index], cancellationToken);

                if (HasSomething(found))
                    notes.Add($"From part {index + 1} of {parts.Count}:\n{found.Trim()}");
            }

            _logger.LogInformation("Read {PartCount} parts in {Elapsed:F0}s, {NoteCount} had something to contribute", parts.Count, watch.Elapsed.TotalSeconds, notes.Count);

            return string.Join("\n\n", notes);
        }

        private async Task<string> CondenseAsync(string notes, string question, CancellationToken cancellationToken)
        {
            for (var round = 0; round < MaxCondenseRounds && !Fits(notes); round++)
            {
                _logger.LogInformation("The notes hold {TokenCount} tokens and do not fit, condensing them again", _tokenCounter.Count(notes));

                notes = await CollectAsync(notes, question, null, cancellationToken);
            }

            return Fits(notes) ? notes : Trim(notes);
        }

        private bool Fits(string notes)
        {
            return _tokenCounter.Count(notes) <= _reader.RoomForAnswer;
        }

        private string Trim(string notes)
        {
            var kept = _splitter.Split(notes, _reader.RoomForAnswer).First();

            _logger.LogWarning(
                "The notes still hold {TokenCount} tokens after condensing them, so only the first {KeptCount} were kept",
                _tokenCounter.Count(notes),
                _tokenCounter.Count(kept));

            return kept;
        }

        private static string Introduce(string notes, int totalParts)
        {
            if (string.IsNullOrWhiteSpace(notes))
                return string.Empty;

            return $"The document was too long to read at once, so it was read in {totalParts} parts and these notes were taken from each of them separately. "
                 + "They describe one single document, so reconcile them into one consistent answer instead of answering part by part, "
                 + $"and never state a conclusion twice.\n\n{notes}";
        }

        private static string Instruction(string question, int part, int totalParts)
        {
            return $"This is part {part} of {totalParts} of a longer document. Write down everything in it that helps answer the following question, quoting the relevant wording. "
                 + $"Be brief and write only what the text supports. If this part contains nothing that helps, reply with the single word {NothingFound}.\n\nQuestion: {question}";
        }

        private static bool HasSomething(string found)
        {
            if (string.IsNullOrWhiteSpace(found))
                return false;

            return !found.Trim().TrimEnd('.').Equals(NothingFound, StringComparison.OrdinalIgnoreCase);
        }

        private static TimeSpan Remaining(TimeSpan elapsed, int completed, int totalParts)
        {
            if (completed == 0)
                return TimeSpan.Zero;

            return elapsed / completed * (totalParts - completed);
        }

        #endregion
    }
}
