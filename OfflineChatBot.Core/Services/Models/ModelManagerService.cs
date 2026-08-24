using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Models
{
    public sealed class ModelManagerService : IModelManagerService
    {
        private const double VisionWeightsProgressShare = 0.87;

        private readonly ModelFileDownloader _downloader;
        private readonly ILogger<ModelManagerService> _logger;
        private readonly List<ModelInfo> _models = ModelCatalog.CreatePresets();

        public ModelManagerService(ModelFileDownloader downloader, ILogger<ModelManagerService> logger)
        {
            _downloader = downloader;
            _logger = logger;
        }

        public Task<List<ModelInfo>> GetAvailableModelsAsync()
        {
            foreach (var model in _models)
                ApplyLocalState(model);

            RefreshRemoteSizesInBackground();

            return Task.FromResult(_models.ToList());
        }

        public async Task DownloadModelAsync(ModelInfo model, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!model.IsDownloadable)
                throw new InvalidOperationException("This model does not have a valid download URL.");

            var weightsShare = model.IsVisionModel ? VisionWeightsProgressShare : 1.0;

            model.FilePath = await DownloadPartAsync(model.DownloadUrl, model.FileName, progress, 0, weightsShare, cancellationToken);

            await DownloadVisionProjectionAsync(model, progress, weightsShare, cancellationToken);

            model.IsDownloaded = true;
        }

        public async Task DeleteModelAsync(ModelInfo model)
        {
            model.IsDownloaded = false;

            await DeleteFileAsync(model.FilePath);
            await DeleteFileAsync(model.MmprojFilePath);

            model.FilePath = string.Empty;
            model.MmprojFilePath = string.Empty;

            _logger.LogInformation("Deleted model {ModelName}", model.Name);
        }

        #region Private Methods

        private async Task DeleteFileAsync(string filePath)
        {
            if (await ModelFileStore.DeleteAsync(filePath))
                return;

            _logger.LogWarning("Could not delete {FilePath}, the file is probably still in use", filePath);
        }

        private async Task DownloadVisionProjectionAsync(
            ModelInfo model,
            IProgress<DownloadProgress>? progress,
            double weightsShare,
            CancellationToken cancellationToken)
        {
            if (!model.IsVisionModel)
                return;

            model.MmprojFilePath = await DownloadPartAsync(
                model.MmprojDownloadUrl,
                model.MmprojFileName,
                progress,
                weightsShare,
                1.0 - weightsShare,
                cancellationToken);
        }

        private async Task<string> DownloadPartAsync(
            string url,
            string fileName,
            IProgress<DownloadProgress>? progress,
            double progressStart,
            double progressShare,
            CancellationToken cancellationToken)
        {
            var destinationPath = Path.Combine(PathHelper.ModelsFolder, fileName);
            var scopedProgress = ScaleProgress(progress, progressStart, progressShare);

            await _downloader.DownloadAsync(url, destinationPath, scopedProgress, cancellationToken);

            return destinationPath;
        }

        private static IProgress<DownloadProgress>? ScaleProgress(IProgress<DownloadProgress>? progress, double start, double share)
        {
            if (progress == null)
                return null;

            return new Progress<DownloadProgress>(part => progress.Report(part with
            {
                Percentage = (start + part.Percentage / 100.0 * share) * 100.0
            }));
        }

        private static void ApplyLocalState(ModelInfo model)
        {
            model.FilePath = Path.Combine(PathHelper.ModelsFolder, model.FileName);
            model.MmprojFilePath = model.IsVisionModel ? Path.Combine(PathHelper.ModelsFolder, model.MmprojFileName) : string.Empty;

            var weightsBytes = ModelFileStore.GetSizeInBytes(model.FilePath);
            var projectionBytes = ModelFileStore.GetSizeInBytes(model.MmprojFilePath);

            model.IsDownloaded = weightsBytes > 0 && (!model.IsVisionModel || projectionBytes > 0);

            if (!model.IsDownloaded)
                return;

            model.SizeInMB = SizeFormatter.ToMegabytes(weightsBytes + projectionBytes);
        }

        private void RefreshRemoteSizesInBackground()
        {
            var pendingModels = _models.Where(model => !model.IsDownloaded && model.IsDownloadable).ToList();

            _ = Task.Run(async () =>
            {
                foreach (var model in pendingModels)
                    await ApplyRemoteSizeAsync(model);
            });
        }

        private async Task ApplyRemoteSizeAsync(ModelInfo model)
        {
            var sizeInBytes = await _downloader.GetRemoteSizeAsync(model.DownloadUrl);

            if (sizeInBytes == null)
                return;

            model.SizeInMB = SizeFormatter.ToMegabytes(sizeInBytes.Value);
        }

        #endregion
    }
}