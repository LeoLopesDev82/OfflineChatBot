using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using OfflineChatBot.Helpers;

namespace OfflineChatBot.Models
{
    public partial class ModelDownloadState : ObservableObject
    {
        private CancellationTokenSource? _cancellationSource;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private double _progress;

        [ObservableProperty]
        private string _speedFormatted = "0 MB/s";

        [ObservableProperty]
        private string _bytesFormatted = "0 MB";

        public bool IsCancelled => _cancellationSource?.IsCancellationRequested == true;

        public CancellationToken Token => _cancellationSource?.Token ?? CancellationToken.None;

        public void Begin()
        {
            _cancellationSource = new CancellationTokenSource();

            IsActive = true;
            Progress = 0;
            SpeedFormatted = "Starting...";
            BytesFormatted = "0 MB";
        }

        public void Report(DownloadProgress update)
        {
            Progress = update.Percentage;
            SpeedFormatted = string.Format(CultureInfo.InvariantCulture, "{0:F1} MB/s", update.SpeedMbPerSecond);
            BytesFormatted = $"{SizeFormatter.FromBytes(update.BytesReceived)} / {SizeFormatter.FromBytes(update.TotalBytes)}";
        }

        public void Complete()
        {
            Progress = 100;
            SpeedFormatted = "Completed!";
        }

        public void End()
        {
            IsActive = false;
        }

        public void Cancel()
        {
            _cancellationSource?.Cancel();
        }
    }
}