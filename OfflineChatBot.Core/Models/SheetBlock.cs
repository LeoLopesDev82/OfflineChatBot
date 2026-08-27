namespace OfflineChatBot.Models
{
    public sealed class SheetBlock
    {
        public string SheetName { get; set; } = string.Empty;
        public CellRange Range { get; set; }
        public string Title { get; set; } = string.Empty;
        public int HeaderRow { get; set; }
        public List<string> Headers { get; } = new List<string>();
        public List<List<string>> Rows { get; } = new List<List<string>>();
        public List<string> TotalsRow { get; } = new List<string>();

        public bool HasHeaders => Headers.Count > 0;
        public bool HasTotals => TotalsRow.Count > 0;
    }
}
