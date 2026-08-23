namespace OfflineChatBot.Models
{
    public readonly record struct DownloadProgress(double Percentage, long BytesReceived, long TotalBytes, double SpeedMbPerSecond);
}