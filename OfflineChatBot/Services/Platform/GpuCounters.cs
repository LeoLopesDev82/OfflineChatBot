using System.Diagnostics;

namespace OfflineChatBot.Services.Platform
{
    public sealed class GpuCounters : IDisposable
    {
        private const string EngineCategory = "GPU Engine";
        private const string MemoryCategory = "GPU Process Memory";
        private const string UtilizationCounter = "Utilization Percentage";
        private const string DedicatedMemoryCounter = "Dedicated Usage";

        private static readonly TimeSpan InstanceRefreshInterval = TimeSpan.FromSeconds(5);

        private readonly string _processPrefix = $"pid_{Environment.ProcessId}_";
        private readonly List<PerformanceCounter> _counters = new List<PerformanceCounter>();

        private DateTime _refreshedAt = DateTime.MinValue;

        public bool IsAvailable { get; private set; } = true;

        public double UtilizationPercentage => Sum(EngineCategory);

        public double DedicatedMemoryInMB => Sum(MemoryCategory) / (1024.0 * 1024.0);

        public void Dispose()
        {
            DisposeCounters();
        }

        #region Private Methods

        private double Sum(string category)
        {
            RefreshCounters();

            var total = 0.0;

            foreach (var counter in _counters.Where(counter => counter.CategoryName == category))
                total += Read(counter);

            return total;
        }

        private static double Read(PerformanceCounter counter)
        {
            try
            {
                return counter.NextValue();
            }
            catch
            {
                return 0;
            }
        }

        private void RefreshCounters()
        {
            if (!IsAvailable || DateTime.UtcNow - _refreshedAt < InstanceRefreshInterval)
                return;

            _refreshedAt = DateTime.UtcNow;

            DisposeCounters();

            try
            {
                AddCounters(EngineCategory, UtilizationCounter);
                AddCounters(MemoryCategory, DedicatedMemoryCounter);
            }
            catch
            {
                IsAvailable = false;
            }
        }

        private void AddCounters(string category, string counterName)
        {
            var instances = new PerformanceCounterCategory(category)
                .GetInstanceNames()
                .Where(instance => instance.StartsWith(_processPrefix, StringComparison.OrdinalIgnoreCase));

            foreach (var instance in instances)
                _counters.Add(new PerformanceCounter(category, counterName, instance, readOnly: true));
        }

        private void DisposeCounters()
        {
            foreach (var counter in _counters)
                counter.Dispose();

            _counters.Clear();
        }

        #endregion
    }
}