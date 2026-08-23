using CommunityToolkit.Mvvm.ComponentModel;
using OfflineChatBot.Helpers;

namespace OfflineChatBot.Models
{
    public partial class ModelInfo : ObservableObject
    {
        [ObservableProperty]
        private bool _isDownloaded;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(SizeFormatted))]
        private double _sizeInMB;

        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public string MmprojFileName { get; set; } = string.Empty;
        public string MmprojFilePath { get; set; } = string.Empty;
        public string MmprojDownloadUrl { get; set; } = string.Empty;

        public ModelDownloadState Download { get; } = new ModelDownloadState();

        public bool IsVisionModel => !string.IsNullOrEmpty(MmprojDownloadUrl);
        public bool IsDownloadable => !string.IsNullOrWhiteSpace(DownloadUrl);

        public string SizeFormatted => SizeFormatter.FromMegabytes(SizeInMB);

        public string? VisionProjectionPath => IsVisionModel ? MmprojFilePath : null;

        public bool IsSameFileAs(ModelInfo? other)
        {
            return other != null && FileName.Equals(other.FileName, StringComparison.OrdinalIgnoreCase);
        }
    }
}