using CommunityToolkit.Mvvm.ComponentModel;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class AppStatusViewModel : ObservableObject
    {
        private readonly IResourceMonitor _resourceMonitor;

        [ObservableProperty]
        private string _message = "Ready";

        [ObservableProperty]
        private string _cpu = "CPU: --";

        [ObservableProperty]
        private string _memory = "RAM: --";

        public AppStatusViewModel(IResourceMonitor resourceMonitor)
        {
            _resourceMonitor = resourceMonitor;
        }

        public string Backend => "GPU: CPU backend";

        public void StartMonitoring()
        {
            _resourceMonitor.Start(ApplyUsage);
        }

        #region Private Methods

        private void ApplyUsage(ResourceUsage usage)
        {
            Cpu = usage.Cpu;
            Memory = usage.Memory;
        }

        #endregion
    }
}