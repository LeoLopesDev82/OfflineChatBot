namespace OfflineChatBot.Models
{
    public readonly record struct BackendStatus(string Device, int OffloadedLayers, int TotalLayers, double VideoMemoryInMB)
    {
        public static readonly BackendStatus Cpu = new BackendStatus("CPU", 0, 0, 0);

        public bool UsesGpu => OffloadedLayers > 0;
    }
}