using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Documents
{
    public sealed class BlockDetector
    {

        public List<SheetBlock> Detect(SheetGrid grid)
        {
            return Join(Bands(grid).Select(band => Build(grid, band)).ToList());
        }

        #region Private Methods

        private static List<SheetBlock> Join(List<SheetBlock> blocks)
        {
            var joined = new List<SheetBlock>();

            for (var index = 0; index < blocks.Count; index++)
            {
                var block = blocks[index];

                if (IsLooseTitle(block) && index + 1 < blocks.Count)
                {
                    blocks[index + 1].Title = Combine(block.Headers.First(header => header.Length > 0), blocks[index + 1].Title);

                    continue;
                }

                if (block.Rows.Count > 0 || block.HasHeaders)
                    joined.Add(block);
            }

            return joined;
        }

        private static bool IsLooseTitle(SheetBlock block)
        {
            return block.Rows.Count == 0 && block.Headers.Count(header => header.Length > 0) == 1;
        }

        private static string Combine(string title, string existing)
        {
            return existing.Length == 0 ? title : $"{title} / {existing}";
        }

        private static List<CellRange> Bands(SheetGrid grid)
        {
            var bands = new List<CellRange>();
            var start = 0;

            for (var row = 1; row <= grid.LastRow + 1; row++)
            {
                var empty = row > grid.LastRow || grid.IsEmptyRow(row);

                if (!empty && start == 0)
                    start = row;

                if (empty && start > 0)
                {
                    bands.Add(Bound(grid, start, row - 1));
                    start = 0;
                }
            }

            return bands;
        }

        private static CellRange Bound(SheetGrid grid, int firstRow, int lastRow)
        {
            var columns = Enumerable.Range(firstRow, lastRow - firstRow + 1).SelectMany(grid.FilledColumns).ToList();

            return new CellRange(firstRow, columns.Min(), lastRow, columns.Max());
        }

        private static SheetBlock Build(SheetGrid grid, CellRange band)
        {
            var block = new SheetBlock { SheetName = grid.Name, Range = band };
            var row = band.FirstRow;

            row = SkipTitles(grid, band, block, row);

            if (row > band.LastRow)
                return block;

            block.HeaderRow = row;
            block.Headers.AddRange(Disambiguate(Cells(grid, row, band), band));

            FillRows(grid, band, block, row + 1);

            return block;
        }

        private static List<string> Disambiguate(List<string> headers, CellRange band)
        {
            var repeated = headers
                .Where(header => header.Length > 0)
                .GroupBy(header => header, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return headers
                .Select((header, index) => repeated.Contains(header)
                    ? $"{header} ({CellRange.ColumnName(band.FirstColumn + index)})"
                    : header)
                .ToList();
        }

        private static int SkipTitles(SheetGrid grid, CellRange band, SheetBlock block, int row)
        {
            while (row < band.LastRow && IsTitle(grid, band, row))
            {
                var text = Cells(grid, row, band).FirstOrDefault(value => value.Length > 0) ?? string.Empty;

                if (text.Length > 0)
                    block.Title = block.Title.Length == 0 ? text : $"{block.Title} / {text}";

                row++;
            }

            return row;
        }

        private static bool IsTitle(SheetGrid grid, CellRange band, int row)
        {
            var filled = grid.FilledColumns(row);

            if (filled.Count == 0)
                return true;

            if (IsMergedBanner(grid, filled, row))
                return true;

            return filled.Count == 1 && grid.FilledColumns(row + 1).Count > 1;
        }

        private static bool IsMergedBanner(SheetGrid grid, List<int> filled, int row)
        {
            return filled.All(column => grid.Merges.Any(merge => Covers(merge, row, column)));
        }

        private static bool Covers(CellRange merge, int row, int column)
        {
            return merge.FirstRow == row
                && merge.SpansSingleRow
                && merge.Width > 1
                && column >= merge.FirstColumn
                && column <= merge.LastColumn;
        }

        private static void FillRows(SheetGrid grid, CellRange band, SheetBlock block, int firstRow)
        {
            for (var row = firstRow; row <= band.LastRow; row++)
            {
                var cells = Cells(grid, row, band);

                if (IsTotals(grid, band, block, row))
                {
                    block.TotalsRow.AddRange(cells);

                    continue;
                }

                block.Rows.Add(cells);
            }
        }

        private static bool IsTotals(SheetGrid grid, CellRange band, SheetBlock block, int row)
        {
            if (row != band.LastRow || block.Rows.Count == 0)
                return false;

            var filled = grid.FilledColumns(row);

            return filled.Count > 0 && filled.Count < block.Headers.Count && filled.Min() > band.FirstColumn;
        }

        private static List<string> Cells(SheetGrid grid, int row, CellRange band)
        {
            return Enumerable.Range(band.FirstColumn, band.Width).Select(column => grid[row, column]).ToList();
        }

        #endregion
    }
}
