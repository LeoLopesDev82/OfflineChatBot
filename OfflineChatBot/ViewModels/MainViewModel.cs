using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OfflineChatBot.Models;
using OfflineChatBot.Services;
using OfflineChatBot.Views;

namespace OfflineChatBot.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ILLMService _llmService;
        private readonly IModelManagerService _modelManager;
        private readonly IChatStorageService _chatStorage;

        private CancellationTokenSource? _generationCts;
        private Task? _warmupTask;

        [ObservableProperty]
        private ObservableCollection<ChatSession> _sessions = new ObservableCollection<ChatSession>();

        [ObservableProperty]
        private ChatSession? _currentSession;

        [ObservableProperty]
        private ObservableCollection<ModelInfo> _availableModels = new ObservableCollection<ModelInfo>();

        [ObservableProperty]
        private ObservableCollection<ModelInfo> _downloadedModels = new ObservableCollection<ModelInfo>();

        [ObservableProperty]
        private ModelInfo? _selectedModel;

        [ObservableProperty]
        private string _userInput = string.Empty;

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        private bool _isModelLoading;

        [ObservableProperty]
        private bool _isDownloadingModel;

        [ObservableProperty]
        private double _downloadProgress;

        [ObservableProperty]
        private string _statusMessage = "Ready";

        public MainViewModel(ILLMService llmService, IModelManagerService modelManager, IChatStorageService chatStorage)
        {
            _llmService = llmService;
            _modelManager = modelManager;
            _chatStorage = chatStorage;
        }

        public MainViewModel() : this(new LlamaSharpService(), new ModelManagerService(), new ChatStorageService())
        {

        }

        public async Task InitializeAsync()
        {
            StatusMessage = "Loading data...";

            var loadedSessions = await _chatStorage.LoadSessionsAsync();
        
            Sessions = new ObservableCollection<ChatSession>(loadedSessions);

            if (Sessions.Count == 0)
                CreateNewChat();
            else
                CurrentSession = Sessions.First();

            await RefreshModelsAsync();
            
            StatusMessage = "Ready";

            var modelToWarmUp = SelectedModel;
            
            if (modelToWarmUp != null && modelToWarmUp.IsDownloaded)
            {
                _warmupTask = Task.Run(async () =>
                {
                    try { await _llmService.LoadModelAsync(modelToWarmUp.FilePath); } catch { }
                });
            }
        }

        partial void OnSelectedModelChanged(ModelInfo? value)
        {

        }

        [RelayCommand]
        public void CreateNewChat()
        {
            var newSession = new ChatSession { Title = "New Chat" };
            
            Sessions.Insert(0, newSession);
            
            CurrentSession = newSession;
            
            SaveSessionsSilently();
        }

        [RelayCommand]
        public void DeleteChat(ChatSession? session)
        {
            if (session == null) return;

            var result = CustomMessageBoxWindow.Show($"Are you sure you want to delete the chat \"{session.Title}\"?", "Confirm Deletion", MessageBoxButton.YesNo);
            
            if (result != MessageBoxResult.Yes) return;

            Sessions.Remove(session);

            if (CurrentSession == session)
            {
                CurrentSession = Sessions.FirstOrDefault();
            
                if (CurrentSession == null)
                    CreateNewChat();
            }

            SaveSessionsSilently();
        }

        [RelayCommand]
        public void OpenModelManager()
        {
            var window = new ModelManagerWindow(this)
            {
                Owner = Application.Current.MainWindow
            };
            
            window.ShowDialog();
        }

        [RelayCommand]
        public async Task SelectModelAsync(ModelInfo? model)
        {
            if (model == null || !model.IsDownloaded) return;

            SelectedModel = model;
            IsModelLoading = true;
            StatusMessage = $"Loading model {model.Name}...";

            try
            {
                await _llmService.LoadModelAsync(model.FilePath);
            
                StatusMessage = $"Model {model.Name} ready!";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading model: {ex.Message}";
            }
            finally
            {
                IsModelLoading = false;
            }
        }

        public async Task DownloadModelWithCtsAsync(ModelInfo model, CancellationToken token)
        {
            if (model == null || model.IsDownloading) return;

            IsDownloadingModel = true;
            StatusMessage = $"Downloading {model.Name}...";

            var progress = new Progress<double>(p => { DownloadProgress = p; });

            try
            {
                await ExecuteModelDownloadAsync(model, progress, token);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Download cancelled by user.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"Download error: {ex.Message}";
            }
            finally
            {
                IsDownloadingModel = false;
            }
        }

        [RelayCommand]
        public async Task DownloadModelAsync(ModelInfo? model)
        {
            if (model == null) return;
            
            var cts = new CancellationTokenSource();
            
            await DownloadModelWithCtsAsync(model, cts.Token);
        }

        [RelayCommand]
        public async Task DownloadAllModelsAsync()
        {
            var pendingModels = AvailableModels.Where(m => !m.IsDownloaded && !string.IsNullOrEmpty(m.DownloadUrl)).ToList();
            
            if (pendingModels.Count == 0)
            {
                CustomMessageBoxWindow.Show("All available models are already downloaded!", "Model Manager", MessageBoxButton.OK);
            
                return;
            }

            foreach (var model in pendingModels)
            {
                model.DownloadCts = new CancellationTokenSource();
                
                await DownloadModelWithCtsAsync(model, model.DownloadCts.Token);
                
                if (model.DownloadCts.IsCancellationRequested)
                    break;
            }
        }

        public async Task DeleteModelAsync(ModelInfo model)
        {
            if (model == null) return;

            if (SelectedModel == model || (SelectedModel != null && SelectedModel.FileName.Equals(model.FileName, StringComparison.OrdinalIgnoreCase)))
            {
                await _llmService.UnloadModelAsync();
                
                SelectedModel = null;
            }

            await _modelManager.DeleteModelAsync(model);
            await RefreshModelsAsync();

            var nextModel = DownloadedModels.FirstOrDefault();
            
            if (nextModel != null)
            {
                SelectedModel = nextModel;
                StatusMessage = $"Model {model.Name} deleted. Selected {nextModel.Name}.";
            }
            else
            {
                SelectedModel = null;
                StatusMessage = $"Model {model.Name} deleted. No downloaded models left.";
            }
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(UserInput) || IsGenerating || CurrentSession == null)
                return;

            var prompt = UserInput.Trim();
            
            UserInput = string.Empty;

            UpdateSessionTitleIfNeeded(prompt);
            AddUserMessageToSession(prompt);

            var assistantMessage = CreateAndAddAssistantMessage();
            
            IsGenerating = true;
            
            _generationCts = new CancellationTokenSource();

            try
            {
                if (!await EnsureModelIsReadyAsync(assistantMessage))
                    return;

                await GenerateAIResponseAsync(assistantMessage, prompt);
            }
            catch (OperationCanceledException)
            {
                StatusMessage = "Generation cancelled.";
            }
            catch (Exception ex)
            {
                assistantMessage.Content += $"\n[Generation error: {ex.Message}]";
            }
            finally
            {
                assistantMessage.IsStreaming = false;
            
                IsGenerating = false;
                
                SaveSessionsSilently();
            }
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _generationCts?.Cancel();
        }

        [RelayCommand]
        public void CopyCode(string? code)
        {
            if (string.IsNullOrEmpty(code)) return;

            Clipboard.SetText(code);
            
            StatusMessage = "Code copied to clipboard!";
        }

        public async Task RefreshModelsAsync()
        {
            var models = await _modelManager.GetAvailableModelsAsync();
            var currentSelectedFileName = SelectedModel?.FileName;

            AvailableModels = new ObservableCollection<ModelInfo>(models);
            DownloadedModels = new ObservableCollection<ModelInfo>(models.Where(m => m.IsDownloaded));

            if (!string.IsNullOrEmpty(currentSelectedFileName))
            {
                var match = DownloadedModels.FirstOrDefault(m => m.FileName.Equals(currentSelectedFileName, StringComparison.OrdinalIgnoreCase));
            
                if (match != null)
                {
                    SelectedModel = match;
                
                    return;
                }
            }

            var defaultDownloaded = DownloadedModels.FirstOrDefault();
            
            SelectedModel = defaultDownloaded;
        }

        #region Private Methods

        private async Task ExecuteModelDownloadAsync(ModelInfo model, IProgress<double> progress, CancellationToken token)
        {
            await _modelManager.DownloadModelAsync(model, progress, token);
            await _llmService.UnloadModelAsync();
            
            await RefreshModelsAsync();

            var downloaded = DownloadedModels.FirstOrDefault(m => m.FileName.Equals(model.FileName, StringComparison.OrdinalIgnoreCase));
            
            if (downloaded != null)
            {
                SelectedModel = downloaded;
                StatusMessage = $"Model {downloaded.Name} downloaded and selected!";
            }
        }

        private void UpdateSessionTitleIfNeeded(string prompt)
        {
            if (CurrentSession != null && (CurrentSession.Messages.Count == 0 || CurrentSession.Title == "New Chat"))
            {
                CurrentSession.Title = prompt.Length > 30 ? prompt.Substring(0, 30) + "..." : prompt;
            }
        }

        private void AddUserMessageToSession(string prompt)
        {
            if (CurrentSession == null) return;

            var userMessage = new ChatMessage
            {
                Sender = MessageSender.User,
                Content = prompt
            };
            
            CurrentSession.Messages.Add(userMessage);
        }

        private ChatMessage CreateAndAddAssistantMessage()
        {
            var assistantMessage = new ChatMessage
            {
                Sender = MessageSender.Assistant,
                Content = string.Empty,
                IsStreaming = true
            };
            
            CurrentSession?.Messages.Add(assistantMessage);
            
            return assistantMessage;
        }

        private async Task<bool> EnsureModelIsReadyAsync(ChatMessage assistantMessage)
        {
            var targetModel = SelectedModel ?? DownloadedModels.FirstOrDefault();

            if (targetModel == null || !targetModel.IsDownloaded)
            {
                assistantMessage.Content = "[No downloaded model available. Please open Model Manager to download Qwen 2.5.]";
            
                OpenModelManager();
                
                return false;
            }

            if (_warmupTask != null && !_warmupTask.IsCompleted)
            {
                StatusMessage = "Warming up AI model...";
                
                await _warmupTask;
            }

            if (!_llmService.IsLoaded || _llmService.LoadedModelPath != targetModel.FilePath)
            {
                StatusMessage = "Loading model into memory...";
                
                await SelectModelAsync(targetModel);
            }

            return true;
        }

        private async Task GenerateAIResponseAsync(ChatMessage assistantMessage, string prompt)
        {
            if (CurrentSession == null || _generationCts == null) return;

            var history = CurrentSession.Messages.Take(CurrentSession.Messages.Count - 2).ToList();
            var stream = _llmService.GenerateResponseStreamAsync(history, prompt, _generationCts.Token);

            await foreach (var token in stream)
            {
                assistantMessage.Content += token;
            }
        }

        private void SaveSessionsSilently()
        {
            Task.Run(async () =>
            {
                try { await _chatStorage.SaveSessionsAsync(Sessions); } catch { }
            });
        }

        #endregion
    }
}