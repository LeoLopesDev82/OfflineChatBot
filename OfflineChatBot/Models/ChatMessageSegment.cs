namespace OfflineChatBot.Models
{
    public class ChatMessageSegment
    {
        public bool IsCode { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; } = "code";
        public string Code { get; set; } = string.Empty;
    }
}