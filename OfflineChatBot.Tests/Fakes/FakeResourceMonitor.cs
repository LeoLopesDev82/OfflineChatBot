using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeResourceMonitor : IResourceMonitor
    {
        public bool Started { get; private set; }

        public void Start(Action<ResourceUsage> onSample)
        {
            Started = true;

            onSample(new ResourceUsage("CPU: 10%", "RAM: 100 MB"));
        }

        public void Stop()
        {
            Started = false;
        }
    }
}
