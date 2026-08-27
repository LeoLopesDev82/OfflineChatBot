using System.Globalization;
using System.Text;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Documents
{
    public sealed class SpreadsheetSummary
    {
        private readonly SpreadsheetProfiler _profiler;

        public SpreadsheetSummary(SpreadsheetProfiler profiler)
        {
            _profiler = profiler;
        }

        public string Describe(string fileName, List<SheetBlock> blocks)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"Spreadsheet {fileName}.");
            builder.AppendLine("The figures in each column summary were computed from the file itself. They are exact: quote them as they are and never round or estimate them.");

            foreach (var block in blocks)
                Append(builder, block);

            return builder.ToString();
        }

        #region Private Methods

        private void Append(StringBuilder builder, SheetBlock block)
        {
            var profile = _profiler.Profile(block);

            builder.AppendLine();
            builder.AppendLine($"Table in sheet \"{block.SheetName}\" at {block.Range}{TitleOf(profile)}, {profile.RowCount} rows, header on row {block.HeaderRow}.");

            foreach (var column in profile.Columns)
                builder.AppendLine($"  {Describe(column)}");

            if (profile.Totals.Count > 0)
                builder.AppendLine($"  A totals row at the bottom holds: {string.Join(", ", profile.Totals)}. It is not counted in the figures above.");
        }

        private static string TitleOf(BlockProfile profile)
        {
            return profile.Title.Length == 0 ? string.Empty : $" titled \"{profile.Title}\"";
        }

        private static string Describe(ColumnProfile column)
        {
            var text = $"{column.Name} ({Kind(column)}, {column.FilledRows} filled";

            if (column.Kind == ValueKind.Number)
                return text + $"{Numeric(column)}): sum {Number(column.Sum)}, average {Number(column.Mean)}, from {Number(column.Minimum)} to {Number(column.Maximum)}";

            if (column.Kind == ValueKind.Date)
                return text + $"): from {column.Earliest} to {column.Latest}";

            if (column.DistinctValues.Count > 0)
                return text + $", {column.DistinctCount} distinct): {string.Join(", ", column.DistinctValues)}";

            return text + $", {column.DistinctCount} distinct)";
        }

        private static string Numeric(ColumnProfile column)
        {
            if (column.NumericRows == column.FilledRows)
                return string.Empty;

            return $", only {column.NumericRows} of them numeric and the figures below cover those";
        }

        private static string Kind(ColumnProfile column)
        {
            return column.Kind switch
            {
                ValueKind.Number => "number",
                ValueKind.Date => "date",
                ValueKind.Empty => "empty",
                _ => "text"
            };
        }

        private static string Number(double? value)
        {
            return value?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-";
        }

        #endregion
    }
}
