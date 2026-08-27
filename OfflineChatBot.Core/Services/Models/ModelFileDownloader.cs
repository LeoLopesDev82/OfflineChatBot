using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Models
{
    public sealed class ModelFileDownloader
    {
        private const int BufferSize = 128 * 1024;
        private const int SpeedSampleIntervalMs = 500;
        private const int ReportIntervalMs = 100;
        private const int Attempts = 10;
        private const int PauseBetweenAttemptsMs = 2000;
        private const int StallSeconds = 30;

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

            await DownloadToTemporaryFileAsync(url, temporaryPath, progress, cancellationToken);

            File.Move(temporaryPath, destinationPath, true);
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
            var knownTotal = 0L;

            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    await FetchAsync(url, temporaryPath, BytesAlreadyHere(temporaryPath), attempt, total => knownTotal = total, progress, cancellationToken);

                    return;
                }
                catch (Exception exception) when (attempt < Attempts && IsWorthRetrying(exception, cancellationToken))
                {
                    var received = BytesAlreadyHere(temporaryPath);

                    _logger.LogWarning(
                        exception,
                        "Download of {Url} broke after {Bytes} bytes, resuming (attempt {Attempt} of {Attempts})",
                        url,
                        received,
                        attempt + 1,
                        Attempts);

                    Report(progress, received, knownTotal, 0, attempt + 1);

                    await Task.Delay(PauseBetweenAttemptsMs, cancellationToken);
                }
            }
        }

        private async Task FetchAsync(
            string url,
            string temporaryPath,
            long alreadyHere,
            int attempt,
            Action<long> onTotalKnown,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (alreadyHere > 0)
                request.Headers.Range = new RangeHeaderValue(alreadyHere, null);

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            response.EnsureSuccessStatusCode();

            var resuming = response.StatusCode == HttpStatusCode.PartialContent;
            var startAt = resuming ? alreadyHere : 0;
            var totalBytes = (response.Content.Headers.ContentLength ?? 0) + startAt;

            onTotalKnown(totalBytes);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(
                temporaryPath,
                resuming ? FileMode.Append : FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                useAsync: true);

            await CopyWithProgressAsync(contentStream, fileStream, startAt, totalBytes, attempt, progress, cancellationToken);

            await fileStream.FlushAsync(cancellationToken);
        }

        private static long BytesAlreadyHere(string temporaryPath)
        {
            return File.Exists(temporaryPath) ? new FileInfo(temporaryPath).Length : 0;
        }

        private static bool IsWorthRetrying(Exception exception, CancellationToken cancellationToken)
        {
            return !cancellationToken.IsCancellationRequested && exception is IOException or HttpRequestException;
        }

        private static async Task CopyWithProgressAsync(
            Stream source,
            Stream destination,
            long startAt,
            long totalBytes,
            int attempt,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancellationToken)
        {
            var buffer = new byte[BufferSize];
            var speedStopwatch = Stopwatch.StartNew();
            var reportStopwatch = Stopwatch.StartNew();

            using var stall = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var totalRead = startAt;
            long bytesSinceLastSample = 0;
            double speedMbPerSecond = 0;
            int bytesRead;

            while ((bytesRead = await ReadAsync(source, buffer, stall, cancellationToken)) > 0)
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

                Report(progress, totalRead, totalBytes, speedMbPerSecond, attempt);
            }

            Report(progress, totalRead, totalBytes, speedMbPerSecond, attempt);
        }

        private static async Task<int> ReadAsync(Stream source, byte[] buffer, CancellationTokenSource stall, CancellationToken cancellationToken)
        {
            stall.CancelAfter(TimeSpan.FromSeconds(StallSeconds));

            try
            {
                return await source.ReadAsync(buffer, stall.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new IOException($"The download stopped receiving data for {StallSeconds} seconds.");
            }
        }

        private static void Report(IProgress<DownloadProgress>? progress, long totalRead, long totalBytes, double speedMbPerSecond, int attempt)
        {
            progress?.Report(new DownloadProgress(Percentage(totalRead, totalBytes), totalRead, totalBytes, speedMbPerSecond, attempt));
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
