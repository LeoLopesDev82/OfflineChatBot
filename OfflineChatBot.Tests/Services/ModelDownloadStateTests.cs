using OfflineChatBot.Models;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class ModelDownloadStateTests
    {
        [Fact]
        public void AnOrdinaryUpdate_ShowsTheSpeed()
        {
            var state = new ModelDownloadState();

            state.Report(new DownloadProgress(42, 100, 200, 8.5));

            Assert.Equal("8.5 MB/s", state.SpeedFormatted);
            Assert.Equal(42, state.Progress);
        }

        [Fact]
        public void AStalledRetry_SaysItIsReconnectingInsteadOfShowingZero()
        {
            var state = new ModelDownloadState();

            state.Report(new DownloadProgress(42, 100, 200, 0, 3));

            Assert.Equal("Reconnecting, attempt 3...", state.SpeedFormatted);
        }

        [Fact]
        public void ARetryThatIsMovingAgain_ShowsTheSpeed()
        {
            var state = new ModelDownloadState();

            state.Report(new DownloadProgress(50, 120, 200, 6.2, 3));

            Assert.Equal("6.2 MB/s", state.SpeedFormatted);
        }

        [Fact]
        public void AStalledRetry_KeepsTheProgressItHadReached()
        {
            var state = new ModelDownloadState();

            state.Report(new DownloadProgress(42, 1000, 2000, 0, 2));

            Assert.Equal(42, state.Progress);
            Assert.Contains("/", state.BytesFormatted);
        }
    }
}
