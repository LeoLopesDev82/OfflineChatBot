using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class AppStatusViewModel : ObservableObject
    {
        private readonly IResourceMonitor _resourceMonitor;
        private readonly ILlmService _llmService;

        [ObservableProperty]
        private string _message = "Ready";

        [ObservableProperty]
        private string _cpu = "CPU: --";

        [ObservableProperty]
        private string _memory = "RAM: --";

        [ObservableProperty]
        private string _gpu = "GPU: --";

        [ObservableProperty]
        private string _backend = "Device: --";

        [ObservableProperty]
        private string _throughput = "Speed: --";

        public AppStatusViewModel(IResourceMonitor resourceMonitor, ILlmService llmService)
        {
            _resourceMonitor = resourceMonitor;
            _llmService = llmService;
        }

        public void StartMonitoring()
        {
            _resourceMonitor.Start(ApplyUsage);
        }

        public void RefreshHardware()
        {
            Backend = DescribeBackend(_llmService.Backend);
            Throughput = DescribeThroughput(_llmService.LastTokensPerSecond);
        }

        #region Private Methods

        private void ApplyUsage(ResourceUsage usage)
        {
            Cpu = usage.Cpu;
            Memory = usage.Memory;
            Gpu = usage.Gpu;

            RefreshHardware();
        }

        private static string DescribeBackend(BackendStatus backend)
        {
            if (!backend.UsesGpu)
                return "Device: CPU";

            return string.Format(
                CultureInfo.InvariantCulture,
                "GPU: {0} ({1}/{2} layers, {3:F0} MiB)",
                backend.Device,
                backend.OffloadedLayers,
                backend.TotalLayers,
                backend.VideoMemoryInMB);
        }

        private static string DescribeThroughput(double tokensPerSecond)
        {
            if (tokensPerSecond <= 0)
                return "Speed: --";

            return string.Format(CultureInfo.InvariantCulture, "Speed: {0:F1} tok/s", tokensPerSecond);
        }

        #endregion
    }
}