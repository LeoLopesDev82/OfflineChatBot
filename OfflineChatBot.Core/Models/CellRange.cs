namespace OfflineChatBot.Models
{
    public readonly record struct CellRange(int FirstRow, int FirstColumn, int LastRow, int LastColumn)
    {
        public int Width => LastColumn - FirstColumn + 1;
        public int Height => LastRow - FirstRow + 1;

        public bool SpansSingleRow => FirstRow == LastRow;

        public override string ToString()
        {
            return $"{ColumnName(FirstColumn)}{FirstRow}:{ColumnName(LastColumn)}{LastRow}";
        }

        public static string ColumnName(int column)
        {
            var name = string.Empty;

            while (column > 0)
            {
                var remainder = (column - 1) % 26;

                name = (char)('A' + remainder) + name;
                column = (column - 1) / 26;
            }

            return name;
        }
    }
}
