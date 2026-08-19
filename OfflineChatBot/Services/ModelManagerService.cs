using System.Diagnostics;
using System.IO;
using System.Net.Http;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services
{
    public class ModelManagerService : IModelManagerService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly List<ModelInfo> _presetModels = new List<ModelInfo>
        {
            new ModelInfo
            {
                Name = "Qwen 2.5 0.5B Instruct (Ultra Light)",
                FileName = "qwen2.5-0.5b-instruct-q4_k_m.gguf",
                DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct-GGUF/resolve/main/qwen2.5-0.5b-instruct-q4_k_m.gguf",
                SizeInMB = 397,
                Description = "Ultra-lightweight 500M parameter model. Ideal for quick tests and low-memory machines. Fast but limited reasoning."
            },
            new ModelInfo
            {
                Name = "Qwen 2.5 Coder 1.5B (Recommended)",
                FileName = "qwen2.5-coder-1.5b-instruct-q4_k_m.gguf",
                DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-Coder-1.5B-Instruct-GGUF/resolve/main/qwen2.5-coder-1.5b-instruct-q4_k_m.gguf",
                SizeInMB = 1060,
                Description = "Best balance of speed and intelligence. Excellent for coding assistance, general conversation, and creative writing."
            },
            new ModelInfo
            {
                Name = "Qwen 2.5 3B Instruct (High Intelligence)",
                FileName = "qwen2.5-3b-instruct-q4_k_m.gguf",
                DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-3B-Instruct-GGUF/resolve/main/qwen2.5-3b-instruct-q4_k_m.gguf",
                SizeInMB = 1930,
                Description = "Highly articulated 3B parameter model. Natural conversational flow with advanced reasoning and multilingual support."
            },
            new ModelInfo
            {
                Name = "Qwen 2.5 Coder 3B Instruct (Advanced Coding)",
                FileName = "qwen2.5-coder-3b-instruct-q4_k_m.gguf",
                DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-Coder-3B-Instruct-GGUF/resolve/main/qwen2.5-coder-3b-instruct-q4_k_m.gguf",
                SizeInMB = 1930,
                Description = "Dedicated coding model with deep understanding of programming languages, algorithms, and software architecture."
            },
            new ModelInfo
            {
                Name = "Qwen 2.5 7B Instruct (Maximum Intelligence)",
                FileName = "qwen2.5-7b-instruct-q4_k_m.gguf",
                DownloadUrl = "https://huggingface.co/Qwen/Qwen2.5-7B-Instruct-GGUF/resolve/main/qwen2.5-7b-instruct-q4_k_m.gguf",
                SizeInMB = 4680,
                Description = "Most powerful model in the lineup. Near GPT-level intelligence for complex analysis, long-form writing, and expert-level coding."
            }
        };

        public Task<List<ModelInfo>> GetAvailableModelsAsync()
        {
            var result = new List<ModelInfo>();
            var modelsDir = PathHelper.ModelsFolder;

            foreach (var preset in _presetModels)
            {
                UpdateLocalPresetInfo(preset, modelsDir);

                result.Add(preset);
            }

            FetchServerFileSizesAsync(result);

            return Task.FromResult(result);
        }

        public async Task DownloadModelAsync(ModelInfo model, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(model.DownloadUrl))
                throw new InvalidOperationException("This model does not have a valid download URL.");

            var destinationPath = Path.Combine(PathHelper.ModelsFolder, model.FileName);
            var tempPath = destinationPath + ".tmp";

            InitializeDownloadState(model);

            try
            {
                using var response = await _httpClient.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                
                response.EnsureSuccessStatusCode();

                var totalBytes = CalculateTotalBytes(response, model);
                
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                await ProcessDownloadStreamAsync(contentStream, fileStream, totalBytes, model, progress, cancellationToken);

                FinalizeDownload(model, tempPath, destinationPath);
            }
            finally
            {
                CleanupFailedDownload(model, tempPath);
            }
        }

        public Task<ModelInfo> AddLocalModelFileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("File not found.", filePath);

            var fileName = Path.GetFileName(filePath);
            var destPath = Path.Combine(PathHelper.ModelsFolder, fileName);

            CopyFileIfNotExists(filePath, destPath);

            var fileInfo = new FileInfo(destPath);
            
            var info = new ModelInfo
            {
                Name = Path.GetFileNameWithoutExtension(fileName),
                FileName = fileName,
                FilePath = destPath,
                SizeInMB = fileInfo.Length / (1024.0 * 1024.0),
                IsDownloaded = true,
                Description = "Locally imported .gguf model"
            };

            return Task.FromResult(info);
        }

        public async Task DeleteModelAsync(ModelInfo model)
        {
            model.IsDownloaded = false;

            if (File.Exists(model.FilePath))
            {
                ForceGarbageCollection();
            
                await Task.Delay(150);

                if (!TryDeleteFile(model.FilePath))
                {
                    ForceGarbageCollection();
                
                    await Task.Delay(300);
                    
                    TryDeleteFile(model.FilePath);
                }
            }

            model.FilePath = string.Empty;
        }

        #region Private Methods

        private void UpdateLocalPresetInfo(ModelInfo preset, string modelsDir)
        {
            var localPath = Path.Combine(modelsDir, preset.FileName);
            
            preset.FilePath = localPath;
            preset.IsDownloaded = File.Exists(localPath);

            if (preset.IsDownloaded)
            {
                var fileInfo = new FileInfo(localPath);
            
                preset.SizeInMB = fileInfo.Length / (1024.0 * 1024.0);
            }
        }

        private void FetchServerFileSizesAsync(List<ModelInfo> models)
        {
            _ = Task.Run(async () =>
            {
                foreach (var model in models)
                {
                    if (!model.IsDownloaded && !string.IsNullOrEmpty(model.DownloadUrl))
                    {
                        await UpdateModelSizeFromServerAsync(model);
                    }
                }
            });
        }

        private async Task UpdateModelSizeFromServerAsync(ModelInfo model)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Head, model.DownloadUrl);
                using var res = await _httpClient.SendAsync(req);
                
                if (res.IsSuccessStatusCode && res.Content.Headers.ContentLength.HasValue)
                {
                    model.SizeInMB = res.Content.Headers.ContentLength.Value / (1024.0 * 1024.0);
                }
            }
            catch { }
        }

        private void InitializeDownloadState(ModelInfo model)
        {
            model.IsDownloading = true;
            model.DownloadProgress = 0;
            model.SpeedFormatted = "Starting...";
        }

        private long CalculateTotalBytes(HttpResponseMessage response, ModelInfo model)
        {
            return response.Content.Headers.ContentLength ?? (long)(model.SizeInMB * 1024 * 1024);
        }

        private async Task ProcessDownloadStreamAsync(
            Stream sourceStream, 
            Stream destinationStream, 
            long totalBytes, 
            ModelInfo model, 
            IProgress<double>? progress, 
            CancellationToken cancellationToken)
        {
            var buffer = new byte[16384];
            long totalRead = 0;
            long bytesReadSinceLastCheck = 0;
            int bytesRead;
            
            var stopwatch = Stopwatch.StartNew();

            while ((bytesRead = await sourceStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await destinationStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                
                totalRead += bytesRead;
                bytesReadSinceLastCheck += bytesRead;

                if (stopwatch.ElapsedMilliseconds >= 500)
                {
                    UpdateSpeedMetric(model, bytesReadSinceLastCheck, stopwatch);
                
                    bytesReadSinceLastCheck = 0;
                    
                    stopwatch.Restart();
                }

                UpdateProgressMetrics(model, totalRead, totalBytes, progress);
            }
        }

        private void UpdateSpeedMetric(ModelInfo model, long bytesReadSinceLastCheck, Stopwatch stopwatch)
        {
            double seconds = stopwatch.ElapsedMilliseconds / 1000.0;
            double speedMbPerSec = (bytesReadSinceLastCheck / (1024.0 * 1024.0)) / seconds;
            
            model.SpeedFormatted = $"{speedMbPerSec:F1} MB/s";
        }

        private void UpdateProgressMetrics(ModelInfo model, long totalRead, long totalBytes, IProgress<double>? progress)
        {
            double currentMb = totalRead / (1024.0 * 1024.0);
            double totalMb = totalBytes / (1024.0 * 1024.0);
            
            model.DownloadedBytesFormatted = $"{currentMb:F0} MB / {totalMb:F0} MB";

            if (totalBytes > 0)
            {
                var pct = (double)totalRead / totalBytes * 100.0;
            
                model.DownloadProgress = pct;
                
                progress?.Report(pct);
            }
        }

        private void FinalizeDownload(ModelInfo model, string tempPath, string destinationPath)
        {
            if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);
            }

            File.Move(tempPath, destinationPath);

            model.FilePath = destinationPath;
            model.IsDownloaded = true;
            model.DownloadProgress = 100;
            model.SpeedFormatted = "Completed!";
        }

        private void CleanupFailedDownload(ModelInfo model, string tempPath)
        {
            model.IsDownloading = false;
            
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { }
            }
        }

        private void CopyFileIfNotExists(string sourcePath, string destPath)
        {
            if (!sourcePath.Equals(destPath, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(sourcePath, destPath, true);
            }
        }

        private void ForceGarbageCollection()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private bool TryDeleteFile(string filePath)
        {
            try
            {
                File.Delete(filePath);
         
                return true;
            }
            catch
            {
                return false;
            }
        }

        #endregion
    }
}