using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Models
{
    public sealed class ModelFileDownloader
    {
        private const int BufferSize = 16 * 1024;
        private const int SpeedSampleIntervalMs = 500;
        private const int ReportIntervalMs = 100;

        private readonly HttpClient _httpClient;
        private readonly ILogger<ModelFileDownloader> _logger;

        public ModelFileDownloader(HttpClient httpClient, ILogger<ModelFileDownloader> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task DownloadAsync(
            string url,
            string destinationPath,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            var temporaryPath = destinationPath + ".tmp";

            try
            {
                await DownloadToTemporaryFileAsync(url, temporaryPath, progress, cancellationToken);

                File.Move(temporaryPath, destinationPath, true);
            }
            finally
            {
                ModelFileStore.TryDelete(temporaryPath);
            }
        }

        public async Task<long?> GetRemoteSizeAsync(string url)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, url);
                using var response = await _httpClient.SendAsync(request);

                return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Could not read the published size of {Url}", url);

                return null;
            }
        }

        #region Private Methods

        private async Task DownloadToTemporaryFileAsync(
            string url,
            string temporaryPath,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(temporaryPath, FileMode.Create, FileAccess.Write, FileShare.None, BufferSize, useAsync: true);

            await CopyWithProgressAsync(contentStream, fileStream, totalBytes, progress, cancellationToken);

            await fileStream.FlushAsync(cancellationToken);
        }

        private static async Task CopyWithProgressAsync(
            Stream source,
            Stream destination,
            long totalBytes,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];
            var speedStopwatch = Stopwatch.StartNew();
            var reportStopwatch = Stopwatch.StartNew();

            long totalRead = 0;
            long bytesSinceLastSample = 0;
            double speedMbPerSecond = 0;
            int bytesRead;

            while ((bytesRead = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);

                totalRead += bytesRead;
                bytesSinceLastSample += bytesRead;

                if (speedStopwatch.ElapsedMilliseconds >= SpeedSampleIntervalMs)
                {
                    speedMbPerSecond = CalculateSpeed(bytesSinceLastSample, speedStopwatch.ElapsedMilliseconds);

                    bytesSinceLastSample = 0;

                    speedStopwatch.Restart();
                }

                if (reportStopwatch.ElapsedMilliseconds < ReportIntervalMs)
                    continue;

                reportStopwatch.Restart();

                Report(progress, totalRead, totalBytes, speedMbPerSecond);
            }

            Report(progress, totalRead, totalBytes, speedMbPerSecond);
        }

        private static void Report(IProgress<DownloadProgress>? progress, long totalRead, long totalBytes, double speedMbPerSecond)
        {
            progress?.Report(new DownloadProgress(Percentage(totalRead, totalBytes), totalRead, totalBytes, speedMbPerSecond));
        }

        private static double Percentage(long bytesReceived, long totalBytes)
        {
            return totalBytes > 0 ? (double)bytesReceived / totalBytes * 100.0 : 0;
        }

        private static double CalculateSpeed(long bytes, long elapsedMilliseconds)
        {
            var seconds = elapsedMilliseconds / 1000.0;

            return SizeFormatter.ToMegabytes(bytes) / seconds;
        }

        #endregion
    }
}