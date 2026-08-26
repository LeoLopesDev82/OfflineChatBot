namespace OfflineChatBot.Models
{
    public sealed record IndexedChunk(int Index, string Text, float[] Embedding);
}
