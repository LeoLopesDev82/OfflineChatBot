using System.Globalization;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Documents
{
    public sealed class SpreadsheetProfiler
    {
        private const int MostDistinctValuesListed = 12;
        private const double ShareToDecideKind = 0.6;

        private static readonly string[] CurrencySymbols = { "R$", "$", "€", "%" };

        public BlockProfile Profile(SheetBlock block)
        {
            var profile = new BlockProfile
            {
                SheetName = block.SheetName,
                Title = block.Title,
                Range = block.Range,
                HeaderRow = block.HeaderRow,
                RowCount = block.Rows.Count
            };

            profile.Totals.AddRange(block.TotalsRow.Where(cell => cell.Length > 0));

            for (var column = 0; column < block.Headers.Count; column++)
                profile.Columns.Add(ProfileColumn(block, column));

            return profile;
        }

        public static bool TryParseNumber(string value, out double number)
        {
            var text = Clean(value);

            number = 0;

            if (text.Length == 0)
                return false;

            return double.TryParse(Normalise(text), NumberStyles.Float, CultureInfo.InvariantCulture, out number);
        }

        #region Private Methods

        private static ColumnProfile ProfileColumn(SheetBlock block, int column)
        {
            var values = block.Rows
                .Select(row => column < row.Count ? row[column] : string.Empty)
                .Where(value => value.Length > 0)
                .ToList();

            var profile = new ColumnProfile
            {
                Name = HeaderOf(block, column),
                FilledRows = values.Count,
                Kind = KindOf(values)
            };

            Describe(profile, values);

            return profile;
        }

        private static string HeaderOf(SheetBlock block, int column)
        {
            var header = block.Headers[column];

            return header.Length > 0 ? header : $"Column {CellRange.ColumnName(block.Range.FirstColumn + column)}";
        }

        private static ValueKind KindOf(List<string> values)
        {
            if (values.Count == 0)
                return ValueKind.Empty;

            if (Share(values, IsDate) >= ShareToDecideKind)
                return ValueKind.Date;

            if (Share(values, value => TryParseNumber(value, out _)) >= ShareToDecideKind)
                return ValueKind.Number;

            return ValueKind.Text;
        }

        private static void Describe(ColumnProfile profile, List<string> values)
        {
            var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            profile.DistinctCount = distinct.Count;

            if (profile.Kind != ValueKind.Number && distinct.Count <= MostDistinctValuesListed)
                profile.DistinctValues.AddRange(distinct);

            if (profile.Kind == ValueKind.Number)
                DescribeNumbers(profile, values);

            if (profile.Kind == ValueKind.Date)
                DescribeDates(profile, values);
        }

        private static void DescribeNumbers(ColumnProfile profile, List<string> values)
        {
            var numbers = values.Select(value => TryParseNumber(value, out var number) ? number : (double?)null)
                .Where(number => number.HasValue)
                .Select(number => number!.Value)
                .ToList();

            if (numbers.Count == 0)
                return;

            profile.NumericRows = numbers.Count;
            profile.Sum = numbers.Sum();
            profile.Mean = numbers.Average();
            profile.Minimum = numbers.Min();
            profile.Maximum = numbers.Max();
        }

        private static void DescribeDates(ColumnProfile profile, List<string> values)
        {
            var dates = values.Where(IsDate).OrderBy(value => value, StringComparer.Ordinal).ToList();

            profile.Earliest = dates.First();
            profile.Latest = dates.Last();
        }

        private static double Share(List<string> values, Func<string, bool> matches)
        {
            return (double)values.Count(matches) / values.Count;
        }

        private static bool IsDate(string value)
        {
            return DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        }

        private static string Normalise(string text)
        {
            var comma = text.LastIndexOf(',');
            var dot = text.LastIndexOf('.');

            if (comma >= 0 && comma > dot)
                return text.Replace(".", string.Empty).Replace(',', '.');

            return comma >= 0 ? text.Replace(",", string.Empty) : text;
        }

        private static string Clean(string value)
        {
            var text = value.Trim();

            foreach (var symbol in CurrencySymbols)
                text = text.Replace(symbol, string.Empty);

            return new string(text.Where(character => !char.IsWhiteSpace(character)).ToArray());
        }

        #endregion
    }
}
