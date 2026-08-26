namespace OfflineChatBot.Models
{
    public sealed class IndexedDocument
    {
        public string Name { get; set; } = string.Empty;
        public IReadOnlyList<IndexedChunk> Chunks { get; set; } = [];
    }
}
