namespace OfflineChatBot.Models
{
    public sealed class SheetGrid
    {
        private readonly Dictionary<(int Row, int Column), string> _cells = new Dictionary<(int, int), string>();

        public string Name { get; set; } = string.Empty;
        public int LastRow { get; private set; }
        public int LastColumn { get; private set; }

        public List<CellRange> Merges { get; } = new List<CellRange>();

        public string this[int row, int column] => _cells.GetValueOrDefault((row, column), string.Empty);

        public void Set(int row, int column, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            _cells[(row, column)] = value.Trim();

            LastRow = Math.Max(LastRow, row);
            LastColumn = Math.Max(LastColumn, column);
        }

        public bool IsEmptyRow(int row)
        {
            return FilledColumns(row).Count == 0;
        }

        public List<int> FilledColumns(int row)
        {
            var filled = new List<int>();

            for (var column = 1; column <= LastColumn; column++)
            {
                if (_cells.ContainsKey((row, column)))
                    filled.Add(column);
            }

            return filled;
        }

        public CellRange? MergeStartingAt(int row, int column)
        {
            return Merges.FirstOrDefault(merge => merge.FirstRow == row && merge.FirstColumn == column);
        }
    }
}
