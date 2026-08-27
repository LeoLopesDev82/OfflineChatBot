namespace OfflineChatBot.Models
{
    public sealed class ReadDocument
    {
        public string Name { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public int Tokens { get; set; }
        public int Parts { get; set; }
        public bool FitsInOnePass { get; set; }
    }
}
