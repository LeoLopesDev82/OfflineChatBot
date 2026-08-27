using System.IO;
using System.Text;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class SpreadsheetTextExtractor : IDocumentTextExtractor
    {
        private readonly WorkbookReader _reader;
        private readonly BlockDetector _detector;
        private readonly SpreadsheetSummary _summary;

        public SpreadsheetTextExtractor(WorkbookReader reader, BlockDetector detector, SpreadsheetSummary summary)
        {
            _reader = reader;
            _detector = detector;
            _summary = summary;
        }

        public bool CanHandle(string filePath)
        {
            return Path.GetExtension(filePath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        }

        public Task<string> ExtractAsync(string filePath, CancellationToken cancellationToken = default)
        {
            return Task.Run(() => Extract(filePath), cancellationToken);
        }

        #region Private Methods

        private string Extract(string filePath)
        {
            var grids = _reader.Read(filePath);
            var blocks = grids.SelectMany(_detector.Detect).ToList();
            var builder = new StringBuilder();

            builder.Append(_summary.Describe(Path.GetFileName(filePath), blocks));

            foreach (var grid in grids)
                AppendTable(builder, grid);

            return builder.ToString();
        }

        private static void AppendTable(StringBuilder builder, SheetGrid grid)
        {
            builder.AppendLine();
            builder.AppendLine($"Sheet \"{grid.Name}\" as a tab separated table, exactly as it appears in Excel. Empty cells are empty columns, so a value always stays under its own heading.");
            builder.AppendLine();

            for (var row = 1; row <= grid.LastRow; row++)
                builder.AppendLine(string.Join("\t", Cells(grid, row)));
        }

        private static IEnumerable<string> Cells(SheetGrid grid, int row)
        {
            return Enumerable.Range(1, grid.LastColumn).Select(column => grid[row, column]);
        }

        #endregion
    }
}
