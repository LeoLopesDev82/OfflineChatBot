using System.Diagnostics;
using System.Windows.Threading;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Platform
{
    public sealed class ProcessResourceMonitor : IResourceMonitor
    {
        private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(1);

        private readonly GpuCounters _gpuCounters = new GpuCounters();

        private DispatcherTimer? _timer;
        private TimeSpan _previousCpuTime;
        private DateTime _previousSampleAt;

        public void Start(Action<ResourceUsage> onSample)
        {
            if (_timer != null)
                return;

            _timer = new DispatcherTimer { Interval = SampleInterval };
            _timer.Tick += (_, _) => onSample(Sample());
            _timer.Start();

            onSample(Sample());
        }

        public void Stop()
        {
            _timer?.Stop();

            _timer = null;

            _gpuCounters.Dispose();
        }

        #region Private Methods

        private ResourceUsage Sample()
        {
            using var process = Process.GetCurrentProcess();

            var cpu = FormatCpu(process);
            var memory = $"RAM: {SizeFormatter.FromBytes(process.WorkingSet64)}";

            return new ResourceUsage(cpu, memory, FormatGpu());
        }

        private string FormatGpu()
        {
            if (!_gpuCounters.IsAvailable)
                return "GPU: --";

            var utilization = _gpuCounters.UtilizationPercentage;
            var memory = _gpuCounters.DedicatedMemoryInMB;

            return $"GPU: {utilization:F0}% · {SizeFormatter.FromMegabytes(memory)}";
        }

        private string FormatCpu(Process process)
        {
            var now = DateTime.UtcNow;
            var isFirstSample = _previousSampleAt == default;
            var percentage = isFirstSample ? 0 : CalculateCpuPercentage(process, now);

            _previousCpuTime = process.TotalProcessorTime;
            _previousSampleAt = now;

            return isFirstSample ? "CPU: --" : $"CPU: {percentage:F0}%";
        }

        private double CalculateCpuPercentage(Process process, DateTime now)
        {
            var elapsedMilliseconds = (now - _previousSampleAt).TotalMilliseconds;

            if (elapsedMilliseconds <= 0)
                return 0;

            var cpuMilliseconds = (process.TotalProcessorTime - _previousCpuTime).TotalMilliseconds;

            return Math.Clamp(cpuMilliseconds / (elapsedMilliseconds * Environment.ProcessorCount) * 100, 0, 100);
        }

        #endregion
    }
}