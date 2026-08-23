using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface IModelManagerService
    {
        Task<List<ModelInfo>> GetAvailableModelsAsync();
        Task DownloadModelAsync(ModelInfo model, IProgress<DownloadProgress>? progress = null, CancellationToken cancellationToken = default);
        Task DeleteModelAsync(ModelInfo model);
    }
}