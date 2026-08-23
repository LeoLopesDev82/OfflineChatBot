using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface IResourceMonitor
    {
        void Start(Action<ResourceUsage> onSample);
        void Stop();
    }
}