using OfflineChatBot.Models;

namespace OfflineChatBot.Services
{
    public interface IModelManagerService
    {
        Task<List<ModelInfo>> GetAvailableModelsAsync();
        Task DownloadModelAsync(ModelInfo model, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
        Task<ModelInfo> AddLocalModelFileAsync(string filePath);
        Task DeleteModelAsync(ModelInfo model);
    }
}