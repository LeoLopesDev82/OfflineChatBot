namespace OfflineChatBot.Models
{
    public readonly record struct ScanProgress(int Part, int TotalParts, TimeSpan Remaining);
}
