using CommunityToolkit.Mvvm.ComponentModel;

namespace OfflineChatBot.Models
{
    public partial class ModelInfo : ObservableObject
    {
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public double SizeInMB { get; set; }
        public string DownloadUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        [ObservableProperty]
        private bool _isDownloaded;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _speedFormatted = "0 MB/s";

        [ObservableProperty]
        private string _downloadedBytesFormatted = "0 MB";

        public CancellationTokenSource? DownloadCts { get; set; }

        public string SizeFormatted
        {
            get
            {
                if (SizeInMB >= 1024)
                    return $"{SizeInMB / 1024.0:F2} GB";

                return $"{SizeInMB:F0} MB";
            }
        }
    }
}