namespace OfflineChatBot.Models
{
    public class DocumentOptions
    {
        public const string SectionName = "Documents";

        public int ChunkTokens { get; set; } = 350;
        public int OverlapTokens { get; set; } = 50;
        public int RetrievedChunks { get; set; } = 4;
    }
}
