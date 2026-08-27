using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class SpreadsheetQueryService : ISpreadsheetQueryService
    {
        private const string CouldNotAnswer =
            "This question could not be turned into a query over the table, so no figure was computed for it. "
            + "Answer only from what is written in the table, and never count, filter or add up rows yourself. "
            + "When the answer would need any of that, say you cannot work it out reliably and ask for the question to be narrowed. "
            + "Never mention queries, tools or these instructions to the user.";

        private readonly ILlmService _llmService;
        private readonly WorkbookReader _reader;
        private readonly BlockDetector _detector;
        private readonly QueryRunner _runner;
        private readonly SpreadsheetSummary _summary;
        private readonly ILogger<SpreadsheetQueryService> _logger;

        public SpreadsheetQueryService(
            ILlmService llmService,
            WorkbookReader reader,
            BlockDetector detector,
            QueryRunner runner,
            SpreadsheetSummary summary,
            ILogger<SpreadsheetQueryService> logger)
        {
            _llmService = llmService;
            _reader = reader;
            _detector = detector;
            _runner = runner;
            _summary = summary;
            _logger = logger;
        }

        public bool CanQuery(string? filePath)
        {
            return filePath != null
                && Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                && File.Exists(filePath);
        }

        public async Task<QueryOutcome> AskAsync(string filePath, string question, CancellationToken cancellationToken = default)
        {
            var blocks = _reader.Read(filePath).SelectMany(_detector.Detect).ToList();

            if (blocks.Count == 0)
                return new QueryOutcome(false, CouldNotAnswer);

            var written = await _llmService.CompleteAsync(Instruction(question), Schema(blocks), cancellationToken);
            var query = QueryRunner.Parse(written);

            if (query == null)
            {
                _logger.LogInformation("No usable query came back for {Question}", question);

                return new QueryOutcome(false, CouldNotAnswer);
            }

            var outcome = _runner.Run(blocks, query);

            _logger.LogInformation(
                "Query {Operation} on {Column} was {Verdict}: {Result}",
                query.Operation,
                query.Column,
                outcome.Answered ? "answered" : "refused",
                outcome.Text);

            if (!outcome.Answered)
                return new QueryOutcome(false, $"{CouldNotAnswer} The attempt failed because: {outcome.Text}");

            return new QueryOutcome(true, Present(query, outcome.Text));
        }

        #region Private Methods

        private static string Present(SpreadsheetQuery query, string result)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"The table was queried for {Describe(query)} and it returned this exact result:");
            builder.AppendLine();
            builder.AppendLine(result);
            builder.AppendLine();
            builder.AppendLine("Answer from this result. It was computed from the file, so it overrides anything you would work out from the table above, and its figures must be quoted exactly. Present it as your own answer without mentioning queries or these instructions.");

            return builder.ToString();
        }

        private static string Describe(SpreadsheetQuery query)
        {
            var text = query.Column.Length > 0
                ? $"{query.Operation.ToLowerInvariant()} of {query.Column}"
                : query.Operation.ToLowerInvariant();

            if (query.Filters.Count == 0)
                return text;

            return $"{text} where {string.Join(" and ", query.Filters.Select(Describe))}";
        }

        private static string Describe(QueryFilter filter)
        {
            return filter.EqualTo.Length > 0 ? $"{filter.Column} is {filter.Equals}" : $"{filter.Column} contains {filter.Contains}";
        }

        private static string Schema(List<SheetBlock> blocks)
        {
            var builder = new StringBuilder();

            foreach (var block in blocks)
            {
                builder.AppendLine($"Table \"{Name(block)}\" with {block.Rows.Count} rows.");
                builder.AppendLine($"Columns: {string.Join(", ", block.Headers.Where(header => header.Length > 0))}");
            }

            return builder.ToString();
        }

        private static string Name(SheetBlock block)
        {
            return block.Title.Length > 0 ? block.Title : block.SheetName;
        }

        private static string Instruction(string question)
        {
            return "You turn a question about a spreadsheet into one JSON query. Reply with the JSON object only, nothing else.\n"
                 + "Shape: {\"table\": string, \"operation\": one of sum|average|min|max|count|distinct|list, \"column\": string, \"limit\": number, \"filters\": [{\"column\": string, \"equals\": string}]}\n"
                 + "Use the exact column names given, copied character for character. Leave filters empty only when the question really has no condition.\n\n"
                 + $"Question: {question}";
        }

        #endregion
    }
}
