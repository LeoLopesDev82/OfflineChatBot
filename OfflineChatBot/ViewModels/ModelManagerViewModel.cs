using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class ModelManagerViewModel : ObservableObject
    {
        private readonly IModelManagerService _modelManager;
        private readonly ILlmService _llmService;
        private readonly IDialogService _dialogService;
        private readonly AppStatusViewModel _status;
        private readonly ILogger<ModelManagerViewModel> _logger;

        private Task? _warmupTask;

        [ObservableProperty]
        private ObservableCollection<ModelInfo> _availableModels = new ObservableCollection<ModelInfo>();

        [ObservableProperty]
        private ObservableCollection<ModelInfo> _downloadedModels = new ObservableCollection<ModelInfo>();

        [ObservableProperty]
        private ModelInfo? _selectedModel;

        [ObservableProperty]
        private bool _isDownloading;

        [ObservableProperty]
        private double _downloadProgress;

        public ModelManagerViewModel(
            IModelManagerService modelManager,
            ILlmService llmService,
            IDialogService dialogService,
            AppStatusViewModel status,
            ILogger<ModelManagerViewModel> logger)
        {
            _modelManager = modelManager;
            _llmService = llmService;
            _dialogService = dialogService;
            _status = status;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await RefreshAsync();

            StartWarmup();
        }

        public async Task RefreshAsync()
        {
            var models = await _modelManager.GetAvailableModelsAsync();

            AvailableModels = new ObservableCollection<ModelInfo>(models);
            DownloadedModels = new ObservableCollection<ModelInfo>(models.Where(model => model.IsDownloaded));

            SelectedModel = FindEquivalentDownloadedModel(SelectedModel) ?? DownloadedModels.FirstOrDefault();
        }

        public async Task<ModelInfo?> EnsureActiveModelReadyAsync()
        {
            var model = SelectedModel ?? DownloadedModels.FirstOrDefault();

            if (model == null || !model.IsDownloaded)
                return null;

            await WaitForWarmupAsync();

            if (IsAlreadyInMemory(model))
                return model;

            _status.Message = "Loading model into memory...";

            await LoadModelAsync(model);

            return model;
        }

        [RelayCommand]
        public async Task SelectModelAsync(ModelInfo? model)
        {
            if (model == null || !model.IsDownloaded)
                return;

            SelectedModel = model;

            await LoadModelAsync(model);
        }

        [RelayCommand]
        public async Task DownloadModelAsync(ModelInfo? model)
        {
            if (model == null || model.Download.IsActive)
                return;

            StartDownloadState(model);

            try
            {
                await _modelManager.DownloadModelAsync(model, CreateProgressReporter(model), model.Download.Token);

                model.Download.Complete();

                await OnModelDownloadedAsync(model);
            }
            catch (OperationCanceledException)
            {
                _status.Message = "Download cancelled by user.";
            }
            catch (Exception exception)
            {
                _status.Message = $"Download error: {exception.Message}";

                _logger.LogError(exception, "Download of {ModelName} failed", model.Name);
            }
            finally
            {
                model.Download.End();

                IsDownloading = false;
            }
        }

        [RelayCommand]
        public async Task DownloadAllModelsAsync()
        {
            var pendingModels = AvailableModels.Where(model => !model.IsDownloaded && model.IsDownloadable).ToList();

            if (pendingModels.Count == 0)
            {
                _dialogService.ShowInformation("All available models are already downloaded!", "Model Manager");

                return;
            }

            foreach (var model in pendingModels)
            {
                await DownloadModelAsync(model);

                if (model.Download.IsCancelled)
                    break;
            }
        }

        [RelayCommand]
        public void CancelDownload(ModelInfo? model)
        {
            model?.Download.Cancel();
        }

        [RelayCommand]
        public async Task DeleteModelAsync(ModelInfo? model)
        {
            if (model == null)
                return;

            if (!_dialogService.Confirm($"Are you sure you want to delete the model file for {model.Name}?", "Confirm Deletion"))
                return;

            await ReleaseModelIfActiveAsync(model);

            await _modelManager.DeleteModelAsync(model);

            await RefreshAsync();

            _status.Message = SelectedModel == null
                ? $"Model {model.Name} deleted. No downloaded models left."
                : $"Model {model.Name} deleted. Selected {SelectedModel.Name}.";
        }

        #region Private Methods

        private async Task LoadModelAsync(ModelInfo model)
        {
            _status.Message = $"Loading model {model.Name}...";

            try
            {
                await _llmService.LoadModelAsync(model.FilePath, model.VisionProjectionPath);

                _status.Message = $"Model {model.Name} ready!";
            }
            catch (Exception exception)
            {
                _status.Message = $"Error loading model: {exception.Message}";

                _logger.LogError(exception, "Could not load model {ModelName}", model.Name);
            }
        }

        private void StartWarmup()
        {
            var model = SelectedModel;

            if (model == null || !model.IsDownloaded)
                return;

            _warmupTask = Task.Run(async () =>
            {
                try
                {
                    await _llmService.LoadModelAsync(model.FilePath, model.VisionProjectionPath);
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(exception, "Warm up of {ModelName} failed, it will be loaded on the first message", model.Name);
                }
            });
        }

        private async Task WaitForWarmupAsync()
        {
            if (_warmupTask == null || _warmupTask.IsCompleted)
                return;

            _status.Message = "Warming up AI model...";

            await _warmupTask;
        }

        private bool IsAlreadyInMemory(ModelInfo model)
        {
            return _llmService.IsLoaded && _llmService.LoadedModelPath == model.FilePath;
        }

        private ModelInfo? FindEquivalentDownloadedModel(ModelInfo? model)
        {
            return DownloadedModels.FirstOrDefault(candidate => candidate.IsSameFileAs(model));
        }

        private void StartDownloadState(ModelInfo model)
        {
            model.Download.Begin();

            IsDownloading = true;
            DownloadProgress = 0;

            _status.Message = $"Downloading {model.Name}...";
        }

        private IProgress<DownloadProgress> CreateProgressReporter(ModelInfo model)
        {
            return new Progress<DownloadProgress>(update =>
            {
                model.Download.Report(update);

                DownloadProgress = update.Percentage;
            });
        }

        private async Task OnModelDownloadedAsync(ModelInfo model)
        {
            await _llmService.UnloadModelAsync();
            await RefreshAsync();

            SelectedModel = FindEquivalentDownloadedModel(model) ?? SelectedModel;

            _status.Message = $"Model {model.Name} downloaded and selected!";
        }

        private async Task ReleaseModelIfActiveAsync(ModelInfo model)
        {
            if (!model.IsSameFileAs(SelectedModel))
                return;

            await _llmService.UnloadModelAsync();

            SelectedModel = null;
        }

        #endregion
    }
}