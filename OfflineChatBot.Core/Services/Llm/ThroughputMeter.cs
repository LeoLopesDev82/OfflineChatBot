using System.Diagnostics;

namespace OfflineChatBot.Services.Llm
{
    public sealed class ThroughputMeter
    {
        private readonly Stopwatch _stopwatch = new Stopwatch();

        public int TokenCount { get; private set; }

        public double TokensPerSecond => Elapsed > 0 ? TokenCount / Elapsed : 0;

        public void Count()
        {
            TokenCount++;

            if (_stopwatch.IsRunning)
                return;

            _stopwatch.Restart();
        }

        #region Private Methods

        private double Elapsed => _stopwatch.Elapsed.TotalSeconds;

        #endregion
    }
}