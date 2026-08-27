using System.Globalization;
using System.Text;
using System.Text.Json;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Documents
{
    public sealed class QueryRunner
    {
        private const int DefaultLimit = 10;
        private const int NamesListed = 15;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static SpreadsheetQuery? Parse(string text)
        {
            var json = Extract(text);

            if (json == null)
                return null;

            try
            {
                var query = JsonSerializer.Deserialize<SpreadsheetQuery>(json, Options);

                return IsWorthRunning(query) ? query : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        public QueryOutcome Run(List<SheetBlock> blocks, SpreadsheetQuery query)
        {
            var block = Choose(blocks, query.Table);

            if (block == null)
                return new QueryOutcome(false, "The spreadsheet has no table by that name.");

            var rejection = Reject(block, query);

            if (rejection != null)
                return new QueryOutcome(false, rejection);

            var rows = Filter(block, query.Filters);

            if (query.Operation.Equals("count", StringComparison.OrdinalIgnoreCase))
                return new QueryOutcome(true, Count(block, rows));

            if (query.Operation.Equals("list", StringComparison.OrdinalIgnoreCase))
                return List(block, rows, query.Limit);

            if (query.Operation.Equals("distinct", StringComparison.OrdinalIgnoreCase))
                return Distinct(block, rows, query.Column);

            return Aggregate(block, rows, query);
        }

        #region Private Methods

        private static bool IsWorthRunning(SpreadsheetQuery? query)
        {
            if (query == null || string.IsNullOrWhiteSpace(query.Operation))
                return false;

            var needsFilters = query.Operation.Equals("list", StringComparison.OrdinalIgnoreCase)
                || query.Operation.Equals("count", StringComparison.OrdinalIgnoreCase);

            return !needsFilters || query.Filters.Count > 0;
        }

        private static string? Extract(string text)
        {
            var start = text.IndexOf('{');

            if (start < 0)
                return null;

            var builder = new StringBuilder();
            var expected = new Stack<char>();
            var inString = false;
            var escaped = false;

            for (var position = start; position < text.Length; position++)
            {
                if (Consume(builder, expected, text[position], ref inString, ref escaped) && expected.Count == 0)
                    break;
            }

            while (expected.Count > 0)
                builder.Append(expected.Pop());

            return builder.Length > 1 ? builder.ToString() : null;
        }

        private static bool Consume(StringBuilder builder, Stack<char> expected, char character, ref bool inString, ref bool escaped)
        {
            if (inString)
            {
                builder.Append(character);

                inString = escaped || character != '"';
                escaped = !escaped && character == '\\';

                return false;
            }

            if (character == '"')
                inString = true;

            if (character is '{' or '[')
                expected.Push(character == '{' ? '}' : ']');
            else if (character is '}' or ']')
            {
                if (expected.Count == 0 || expected.Peek() != character)
                    return false;

                expected.Pop();
                builder.Append(character);

                return true;
            }

            builder.Append(character);

            return false;
        }

        private static SheetBlock? Choose(List<SheetBlock> blocks, string table)
        {
            if (blocks.Count == 1 || string.IsNullOrWhiteSpace(table))
                return blocks.FirstOrDefault();

            return blocks.FirstOrDefault(block => Names(block, table)) ?? blocks.FirstOrDefault();
        }

        private static bool Names(SheetBlock block, string table)
        {
            return block.Title.Contains(table, StringComparison.OrdinalIgnoreCase)
                || block.SheetName.Contains(table, StringComparison.OrdinalIgnoreCase);
        }

        private static string? Reject(SheetBlock block, SpreadsheetQuery query)
        {
            foreach (var filter in query.Filters)
            {
                var index = IndexOf(block, filter.Column);

                if (index < 0)
                    return Missing(block, filter.Column);

                if (filter.EqualTo.Length > 0 && !Occurs(block, index, filter.EqualTo))
                    return $"No row holds \"{filter.EqualTo}\" in {block.Headers[index]}, so the condition matches nothing.";
            }

            return null;
        }

        private static bool Occurs(SheetBlock block, int index, string value)
        {
            return block.Rows.Any(row => index < row.Count && row[index].Equals(value, StringComparison.OrdinalIgnoreCase));
        }

        private static List<List<string>> Filter(SheetBlock block, List<QueryFilter> filters)
        {
            return block.Rows.Where(row => filters.All(filter => Keeps(block, row, filter))).ToList();
        }

        private static bool Keeps(SheetBlock block, List<string> row, QueryFilter filter)
        {
            var index = IndexOf(block, filter.Column);

            if (index < 0 || index >= row.Count)
                return false;

            var value = row[index];

            if (filter.EqualTo.Length > 0)
                return value.Equals(filter.EqualTo, StringComparison.OrdinalIgnoreCase);

            return filter.Contains.Length == 0 || value.Contains(filter.Contains, StringComparison.OrdinalIgnoreCase);
        }

        private static string Missing(SheetBlock block, string column)
        {
            var candidates = Candidates(block, column);

            if (candidates.Count > 1)
                return $"\"{column}\" is ambiguous: this table has {string.Join(" and ", candidates.Select(name => $"\"{name}\""))}. Ask again naming one of them.";

            return $"This table has no column called \"{column}\".";
        }

        private static List<string> Candidates(SheetBlock block, string column)
        {
            return block.Headers
                .Where(header => header.Length > 0 && header.StartsWith(column, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static int IndexOf(SheetBlock block, string column)
        {
            var exact = block.Headers.FindIndex(header => header.Equals(column, StringComparison.OrdinalIgnoreCase));

            if (exact >= 0)
                return exact;

            var starting = block.Headers
                .Select((header, index) => (Header: header, Index: index))
                .Where(entry => entry.Header.Length > 0 && entry.Header.StartsWith(column, StringComparison.OrdinalIgnoreCase))
                .ToList();

            return starting.Count == 1 ? starting[0].Index : -1;
        }

        private static QueryOutcome Aggregate(SheetBlock block, List<List<string>> rows, SpreadsheetQuery query)
        {
            var index = IndexOf(block, query.Column);

            if (index < 0)
                return new QueryOutcome(false, Missing(block, query.Column));

            var numbers = rows
                .Where(row => index < row.Count)
                .Select(row => SpreadsheetProfiler.TryParseNumber(row[index], out var number) ? number : (double?)null)
                .Where(number => number.HasValue)
                .Select(number => number!.Value)
                .ToList();

            if (numbers.Count == 0)
                return new QueryOutcome(false, $"{block.Headers[index]} holds no numeric values for those rows.");

            return new QueryOutcome(true, $"{Describe(query.Operation, numbers)} over {numbers.Count} rows of {block.Headers[index]}.");
        }

        private static string Describe(string operation, List<double> numbers)
        {
            var value = operation.ToLowerInvariant() switch
            {
                "sum" => numbers.Sum(),
                "average" or "mean" => numbers.Average(),
                "min" or "minimum" => numbers.Min(),
                "max" or "maximum" => numbers.Max(),
                _ => numbers.Sum()
            };

            return $"{operation} = {value.ToString("0.##", CultureInfo.InvariantCulture)}";
        }

        private static string Count(SheetBlock block, List<List<string>> rows)
        {
            var named = Names(block, rows);

            return named.Length == 0 ? $"**{rows.Count}** rows match." : $"**{rows.Count}** rows match: {named}.";
        }

        private static string Names(SheetBlock block, List<List<string>> rows)
        {
            if (rows.Count == 0 || rows.Count > NamesListed || block.Headers.Count == 0)
                return string.Empty;

            return string.Join(", ", rows.Where(row => row.Count > 0 && row[0].Length > 0).Select(row => row[0]));
        }

        private static QueryOutcome List(SheetBlock block, List<List<string>> rows, int? limit)
        {
            if (rows.Count == 0)
                return new QueryOutcome(true, "No rows match that condition.");

            var take = limit is > 0 ? limit.Value : DefaultLimit;
            var lines = rows.Take(take).Select(row => $"- {string.Join("; ", Pairs(block, row))}");

            return new QueryOutcome(true, $"**{rows.Count}** rows match, showing {Math.Min(take, rows.Count)}:\n{string.Join("\n", lines)}");
        }

        private static IEnumerable<string> Pairs(SheetBlock block, List<string> row)
        {
            return row
                .Select((value, index) => (Header: block.Headers[index], Value: value))
                .Where(pair => pair.Value.Length > 0)
                .Select(pair => $"{pair.Header}={pair.Value}");
        }

        private static QueryOutcome Distinct(SheetBlock block, List<List<string>> rows, string column)
        {
            var index = IndexOf(block, column);

            if (index < 0)
                return new QueryOutcome(false, Missing(block, column));

            var values = rows
                .Where(row => index < row.Count && row[index].Length > 0)
                .Select(row => row[index])
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return new QueryOutcome(true, $"{values.Count} distinct values in {block.Headers[index]}: {string.Join(", ", values.Take(30))}");
        }

        #endregion
    }
}
