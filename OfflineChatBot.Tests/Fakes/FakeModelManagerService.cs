using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeModelManagerService : IModelManagerService
    {
        public const string ModelPath = @"C:\models\fake-model.gguf";

        public List<ModelInfo> Models { get; set; } = new List<ModelInfo>
        {
            new ModelInfo
            {
                Name = "Fake Model",
                FileName = "fake-model.gguf",
                FilePath = ModelPath,
                IsDownloaded = true
            }
        };

        public int DeleteCount { get; private set; }

        public Task<List<ModelInfo>> GetAvailableModelsAsync()
        {
            return Task.FromResult(Models.ToList());
        }

        public Task DownloadModelAsync(ModelInfo model, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            progress?.Report(new DownloadProgress(100, 10, 10, 1));

            model.IsDownloaded = true;

            return Task.CompletedTask;
        }

        public Task DeleteModelAsync(ModelInfo model)
        {
            DeleteCount++;
            model.IsDownloaded = false;

            return Task.CompletedTask;
        }
    }
}
