namespace OfflineChatBot.Models
{
    public sealed class BlockProfile
    {
        public string SheetName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public CellRange Range { get; set; }
        public int HeaderRow { get; set; }
        public int RowCount { get; set; }

        public List<ColumnProfile> Columns { get; } = new List<ColumnProfile>();
        public List<string> Totals { get; } = new List<string>();
    }
}
