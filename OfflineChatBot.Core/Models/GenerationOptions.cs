namespace OfflineChatBot.Models
{
    public class GenerationOptions
    {
        public const string SectionName = "Generation";

        public uint ContextSize { get; set; } = 8192;
        public int MaxTokens { get; set; } = 2048;
        public int MaxNoteTokens { get; set; } = 256;
        public int MaxHistoryTokens { get; set; } = 2048;
        public bool UseGpu { get; set; } = true;
        public int GpuLayerCount { get; set; } = 99;
        public float Temperature { get; set; } = 0.7f;
        public float RepeatPenalty { get; set; } = 1.18f;
        public int TopK { get; set; } = 40;
        public float TopP { get; set; } = 0.95f;
    }
}