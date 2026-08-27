namespace OfflineChatBot.Models
{
    public enum ValueKind
    {
        Empty,
        Text,
        Number,
        Date
    }

    public sealed class ColumnProfile
    {
        public string Name { get; set; } = string.Empty;
        public ValueKind Kind { get; set; }
        public int FilledRows { get; set; }
        public int DistinctCount { get; set; }
        public int NumericRows { get; set; }

        public List<string> DistinctValues { get; } = new List<string>();

        public double? Sum { get; set; }
        public double? Mean { get; set; }
        public double? Minimum { get; set; }
        public double? Maximum { get; set; }

        public string Earliest { get; set; } = string.Empty;
        public string Latest { get; set; } = string.Empty;
    }
}
