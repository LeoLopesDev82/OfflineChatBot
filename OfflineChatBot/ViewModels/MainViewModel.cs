using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const string NoModelMessage = "[No downloaded model available. Please open Model Manager to download Qwen 2.5.]";
        private const string VisionRequiredMessage = "Image attachments require a vision model. Please select LLaVA 1.5 7B (Vision & Chat) before sending your message.";

        private readonly ILlmService _llmService;
        private readonly IChatStorageService _chatStorage;
        private readonly IDialogService _dialogService;
        private readonly IUiDispatcher _uiDispatcher;

        private CancellationTokenSource? _generationCts;

        [ObservableProperty]
        private ObservableCollection<ChatSession> _sessions = new ObservableCollection<ChatSession>();

        [ObservableProperty]
        private ChatSession? _currentSession;

        [ObservableProperty]
        private string _userInput = string.Empty;

        [ObservableProperty]
        private bool _isGenerating;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingImage))]
        private string? _pendingImagePath;

        public MainViewModel(
            ILlmService llmService,
            IChatStorageService chatStorage,
            IDialogService dialogService,
            IUiDispatcher uiDispatcher,
            ModelManagerViewModel models,
            AppStatusViewModel status)
        {
            _llmService = llmService;
            _chatStorage = chatStorage;
            _dialogService = dialogService;
            _uiDispatcher = uiDispatcher;

            Models = models;
            Status = status;
        }

        public ModelManagerViewModel Models { get; }
        public AppStatusViewModel Status { get; }

        public bool HasPendingImage => !string.IsNullOrEmpty(PendingImagePath);

        public bool HasOpenRename => Sessions.Any(session => session.IsEditing);

        public async Task InitializeAsync()
        {
            Status.Message = "Loading data...";

            await LoadSessionsAsync();
            await Models.InitializeAsync();

            Status.Message = "Ready";
        
            Status.StartMonitoring();
        }

        [RelayCommand]
        public void CreateNewChat()
        {
            var session = new ChatSession();

            Sessions.Insert(0, session);

            CurrentSession = session;

            SaveSessions();
        }

        [RelayCommand]
        public void DeleteChat(ChatSession? session)
        {
            if (session == null)
                return;

            if (!_dialogService.Confirm($"Are you sure you want to delete the chat \"{session.Title}\"?", "Confirm Deletion"))
                return;

            Sessions.Remove(session);

            EnsureCurrentSession(session);

            SaveSessions();
        }

        [RelayCommand]
        public void BeginRenameChat(ChatSession? session)
        {
            CloseOpenRenames();

            if (session == null)
                return;

            session.IsEditing = true;
        }

        [RelayCommand]
        public void CommitRenameChat(ChatSession? session)
        {
            if (session?.IsEditing != true)
                return;

            session.IsEditing = false;

            SaveSessions();
        }

        [RelayCommand]
        public void CommitAllRenames()
        {
            if (!CloseOpenRenames())
                return;

            SaveSessions();
        }

        [RelayCommand]
        public void AttachImage()
        {
            PendingImagePath = _dialogService.PickImageFile() ?? PendingImagePath;
        }

        [RelayCommand]
        public void RemoveAttachedImage()
        {
            PendingImagePath = null;
        }

        [RelayCommand]
        public void OpenModelManager()
        {
            _dialogService.ShowModelManager();
        }

        [RelayCommand]
        public async Task SendMessageAsync()
        {
            if (IsGenerating || string.IsNullOrWhiteSpace(UserInput))
                return;

            var prompt = UserInput.Trim();
            var imagePath = PendingImagePath;

            if (!IsImageSupportedByActiveModel(imagePath))
                return;

            ClearComposer();

            var session = CurrentSession ?? CreateAndSelectSession();
            var history = session.Messages.ToList();

            session.RenameFromPrompt(prompt);
            session.AddUserMessage(prompt, imagePath);

            await GenerateAnswerAsync(session.AddStreamingAssistantMessage(), history, prompt, imagePath);
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _generationCts?.Cancel();
        }

        #region Private Methods

        private async Task LoadSessionsAsync()
        {
            var storedSessions = await _chatStorage.LoadSessionsAsync();

            Sessions = new ObservableCollection<ChatSession>(storedSessions);
            CurrentSession = Sessions.FirstOrDefault();

            if (CurrentSession == null)
                CreateNewChat();
        }

        private ChatSession CreateAndSelectSession()
        {
            CreateNewChat();

            return CurrentSession!;
        }

        private void EnsureCurrentSession(ChatSession removedSession)
        {
            if (CurrentSession != removedSession)
                return;

            CurrentSession = Sessions.FirstOrDefault();

            if (CurrentSession == null)
                CreateNewChat();
        }

        private bool CloseOpenRenames()
        {
            var editingSessions = Sessions.Where(session => session.IsEditing).ToList();

            foreach (var session in editingSessions)
                session.IsEditing = false;

            return editingSessions.Count > 0;
        }

        private bool IsImageSupportedByActiveModel(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || Models.SelectedModel?.IsVisionModel == true)
                return true;

            _dialogService.ShowInformation(VisionRequiredMessage, "Vision Model Required");

            return false;
        }

        private void ClearComposer()
        {
            UserInput = string.Empty;
            PendingImagePath = null;
        }

        private async Task GenerateAnswerAsync(ChatMessage answer, List<ChatMessage> history, string prompt, string? imagePath)
        {
            IsGenerating = true;

            _generationCts = new CancellationTokenSource();

            try
            {
                await RequestAnswerAsync(answer, history, prompt, imagePath);
            }
            catch (OperationCanceledException)
            {
                Status.Message = "Generation cancelled.";
            }
            catch (Exception exception)
            {
                answer.Content += $"\n[Generation error: {exception.Message}]";
            }
            finally
            {
                answer.IsStreaming = false;

                IsGenerating = false;

                SaveSessions();
            }
        }

        private async Task RequestAnswerAsync(ChatMessage answer, List<ChatMessage> history, string prompt, string? imagePath)
        {
            var model = await Models.EnsureActiveModelReadyAsync();

            if (model == null)
            {
                answer.Content = NoModelMessage;

                _dialogService.ShowModelManager();

                return;
            }

            await StreamAnswerAsync(answer, history, prompt, imagePath, _generationCts!.Token);
        }

        private Task StreamAnswerAsync(
            ChatMessage answer,
            List<ChatMessage> history,
            string prompt,
            string? imagePath,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var stream = _llmService.GenerateResponseStreamAsync(history, prompt, imagePath, cancellationToken);

                await foreach (var token in stream.WithCancellation(cancellationToken))
                    await _uiDispatcher.InvokeAsync(() => answer.Content += token, cancellationToken);
            }, cancellationToken);
        }

        private void SaveSessions()
        {
            var snapshot = Sessions.ToList();

            _ = Task.Run(async () =>
            {
                try { await _chatStorage.SaveSessionsAsync(snapshot); } catch { }
            });
        }

        #endregion
    }
}