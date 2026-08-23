using OfflineChatBot.Models;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class ModelDownloadStateTests
    {
        [Fact]
        public void Begin_ResetsProgressAndProvidesAToken()
        {
            var state = new ModelDownloadState { Progress = 42 };

            state.Begin();

            Assert.True(state.IsActive);
            Assert.Equal(0, state.Progress);
            Assert.False(state.IsCancelled);
            Assert.True(state.Token.CanBeCanceled);
        }

        [Fact]
        public void Report_FormatsSpeedAndTransferredBytes()
        {
            var state = new ModelDownloadState();

            state.Report(new DownloadProgress(50, 512L * 1024 * 1024, 1024L * 1024 * 1024, 12.34));

            Assert.Equal(50, state.Progress);
            Assert.Equal("12.3 MB/s", state.SpeedFormatted);
            Assert.Equal("512 MB / 1.00 GB", state.BytesFormatted);
        }

        [Fact]
        public void Cancel_MarksTheDownloadAsCancelled()
        {
            var state = new ModelDownloadState();

            state.Begin();
            state.Cancel();

            Assert.True(state.IsCancelled);
        }

        [Fact]
        public void Complete_FillsTheProgressBar()
        {
            var state = new ModelDownloadState();

            state.Begin();
            state.Complete();

            Assert.Equal(100, state.Progress);
        }
    }
}