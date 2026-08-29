using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDocumentStore _documentStore;
        private readonly SemaphoreSlim _saveLock = new SemaphoreSlim(1, 1);

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
            ILogger<MainViewModel> logger,
            IDocumentStore documentStore,
            DocumentAttachmentViewModel documents,
            ModelManagerViewModel models,
            AppStatusViewModel status)
        {
            _llmService = llmService;
            _chatStorage = chatStorage;
            _dialogService = dialogService;
            _uiDispatcher = uiDispatcher;
            _logger = logger;
            _documentStore = documentStore;

            Documents = documents;
            Models = models;
            Status = status;

            Documents.PropertyChanged += OnDocumentsPropertyChanged;
        }

        public DocumentAttachmentViewModel Documents { get; }
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
        public async Task DeleteChatAsync(ChatSession? session)
        {
            if (session == null)
                return;

            if (!await _dialogService.ConfirmAsync($"Are you sure you want to delete the chat \"{session.Title}\"?", "Confirm Deletion"))
                return;

            Sessions.Remove(session);

            _ = _documentStore.DeleteAsync(session.Id);

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

        [RelayCommand(CanExecute = nameof(IsIdle))]
        public async Task AttachDocumentAsync()
        {
            EnsureSession();

            await Documents.AttachAsync();

            SaveSessions();
        }

        [RelayCommand]
        public async Task RemoveAttachedDocumentAsync()
        {
            await Documents.RemoveAsync();

            SaveSessions();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        public async Task SendMessageAsync()
        {
            if (IsGenerating || string.IsNullOrWhiteSpace(UserInput))
                return;

            var prompt = UserInput.Trim();
            var imagePath = PendingImagePath;
            var documentName = Documents.PendingDocumentName;

            if (!await IsImageSupportedByActiveModelAsync(imagePath))
                return;

            ClearComposer();

            var session = EnsureSession();
            var history = session.Messages.ToList();

            session.RenameFromPrompt(prompt);
            session.AddUserMessage(prompt, imagePath, documentName);

            var answer = session.AddStreamingAssistantMessage();

            await GenerateAnswerAsync(answer, session, history, prompt, imagePath);
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _generationCts?.Cancel();
        }

        partial void OnCurrentSessionChanged(ChatSession? value)
        {
            Documents.UseSession(value);
        }

        #region Private Methods

        private bool IsIdle() => !Documents.IsReadingDocument;

        private void OnDocumentsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(DocumentAttachmentViewModel.IsReadingDocument))
                return;

            SendMessageCommand.NotifyCanExecuteChanged();
            AttachDocumentCommand.NotifyCanExecuteChanged();
        }

        private ChatSession EnsureSession()
        {
            if (CurrentSession == null)
                CreateNewChat();

            return CurrentSession!;
        }

        private async Task LoadSessionsAsync()
        {
            var storedSessions = await _chatStorage.LoadSessionsAsync();

            Sessions = new ObservableCollection<ChatSession>(storedSessions);
            CurrentSession = Sessions.FirstOrDefault();

            if (CurrentSession == null)
                CreateNewChat();
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

        private async Task<bool> IsImageSupportedByActiveModelAsync(string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || Models.SelectedModel?.IsVisionModel == true)
                return true;

            await _dialogService.ShowInformationAsync(VisionRequiredMessage, "Vision Model Required");

            return false;
        }

        private void ClearComposer()
        {
            UserInput = string.Empty;
            PendingImagePath = null;

            Documents.ClearPending();
        }

        private async Task GenerateAnswerAsync(ChatMessage answer, ChatSession session, List<ChatMessage> history, string prompt, string? imagePath)
        {
            IsGenerating = true;

            _generationCts = new CancellationTokenSource();

            try
            {
                var documentContext = await Documents.FindContextAsync(session, prompt, _generationCts.Token);

                await RequestAnswerAsync(answer, session.Id, history, prompt, imagePath, documentContext);
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

        private async Task RequestAnswerAsync(ChatMessage answer, string conversationId, List<ChatMessage> history, string prompt, string? imagePath, string documentContext)
        {
            var model = await Models.EnsureActiveModelReadyAsync();

            if (model == null)
            {
                answer.Content = NoModelMessage;

                _dialogService.ShowModelManager();

                return;
            }

            await StreamAnswerAsync(answer, conversationId, history, prompt, imagePath, documentContext, _generationCts!.Token);
        }

        private Task StreamAnswerAsync(
            ChatMessage answer,
            string conversationId,
            List<ChatMessage> history,
            string prompt,
            string? imagePath,
            string documentContext,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var stream = _llmService.GenerateResponseStreamAsync(conversationId, history, prompt, imagePath, documentContext, cancellationToken);

                await foreach (var token in stream.WithCancellation(cancellationToken))
                    await _uiDispatcher.InvokeAsync(() => answer.Content += token, cancellationToken);
            }, cancellationToken);
        }

        private void SaveSessions()
        {
            var snapshot = Sessions.Select(session => session.Snapshot()).ToList();

            _ = SaveSnapshotAsync(snapshot);
        }

        private async Task SaveSnapshotAsync(List<ChatSession> snapshot)
        {
            await _saveLock.WaitAsync();

            try
            {
                await _chatStorage.SaveSessionsAsync(snapshot);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not save the chat history");
            }
            finally
            {
                _saveLock.Release();
            }
        }

        #endregion
    }
}
