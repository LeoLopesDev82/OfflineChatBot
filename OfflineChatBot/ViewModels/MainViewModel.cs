using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const string NoModelMessage = "[No downloaded model available. Please open Model Manager to download Qwen 2.5.]";
        private const string VisionRequiredMessage = "Image attachments require a vision model. Please select LLaVA 1.5 7B (Vision & Chat) before sending your message.";
        private const string EmbeddingRequiredMessage = "Reading documents needs the EmbeddingGemma model. Open the Model Manager and download it, then attach the file again. A chat model is still needed to answer.";

        private readonly ILlmService _llmService;
        private readonly IChatStorageService _chatStorage;
        private readonly IDialogService _dialogService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDocumentIndexService _documents;
        private readonly IDocumentStore _documentStore;
        private readonly DocumentOptions _documentOptions;

        private CancellationTokenSource? _generationCts;
        private IndexedDocument? _activeDocument;

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

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingDocument))]
        private string? _pendingDocumentName;

        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(SendMessageCommand))]
        [NotifyCanExecuteChangedFor(nameof(AttachDocumentCommand))]
        private bool _isIndexingDocument;

        public MainViewModel(
            ILlmService llmService,
            IChatStorageService chatStorage,
            IDialogService dialogService,
            IUiDispatcher uiDispatcher,
            ILogger<MainViewModel> logger,
            IDocumentIndexService documents,
            IDocumentStore documentStore,
            IOptions<DocumentOptions> documentOptions,
            ModelManagerViewModel models,
            AppStatusViewModel status)
        {
            _llmService = llmService;
            _chatStorage = chatStorage;
            _dialogService = dialogService;
            _uiDispatcher = uiDispatcher;
            _logger = logger;
            _documents = documents;
            _documentStore = documentStore;
            _documentOptions = documentOptions.Value;

            Models = models;
            Status = status;
        }

        public ModelManagerViewModel Models { get; }
        public AppStatusViewModel Status { get; }

        public bool HasPendingImage => !string.IsNullOrEmpty(PendingImagePath);

        public bool HasPendingDocument => !string.IsNullOrEmpty(PendingDocumentName);

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
            if (!await Models.EnsureEmbeddingModelReadyAsync())
            {
                _dialogService.ShowInformation(EmbeddingRequiredMessage, "Document Search Model Required");

                return;
            }

            var filePath = _dialogService.PickDocumentFile();

            if (string.IsNullOrEmpty(filePath))
                return;

            await IndexDocumentAsync(CurrentSession ?? CreateAndSelectSession(), filePath);
        }

        [RelayCommand]
        public async Task RemoveAttachedDocumentAsync()
        {
            var session = CurrentSession;

            if (session?.DocumentName == null)
                return;

            await _documentStore.DeleteAsync(session.Id);

            session.DocumentName = null;
            _activeDocument = null;
            PendingDocumentName = null;

            SaveSessions();
        }

        [RelayCommand(CanExecute = nameof(IsIdle))]
        public async Task SendMessageAsync()
        {
            if (IsGenerating || string.IsNullOrWhiteSpace(UserInput))
                return;

            var prompt = UserInput.Trim();
            var imagePath = PendingImagePath;
            var documentName = PendingDocumentName;

            if (!IsImageSupportedByActiveModel(imagePath))
                return;

            ClearComposer();

            var session = CurrentSession ?? CreateAndSelectSession();
            var history = session.Messages.ToList();

            session.RenameFromPrompt(prompt);
            session.AddUserMessage(prompt, imagePath, documentName);

            var answer = session.AddStreamingAssistantMessage();
            var documentContext = await FindDocumentContextAsync(session, prompt);

            await GenerateAnswerAsync(answer, history, prompt, imagePath, documentContext);
        }

        [RelayCommand]
        public void StopGeneration()
        {
            _generationCts?.Cancel();
        }

        partial void OnCurrentSessionChanged(ChatSession? value)
        {
            _activeDocument = null;
            PendingDocumentName = null;
        }

        #region Private Methods

        private bool IsIdle() => !IsIndexingDocument;

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

        private async Task IndexDocumentAsync(ChatSession session, string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            PendingDocumentName = fileName;
            IsIndexingDocument = true;

            try
            {
                var progress = new Progress<double>(percentage => Status.Message = $"Reading {fileName}... {percentage:F0}%");

                _activeDocument = await Task.Run(() => _documents.IndexAsync(filePath, progress));

                await _documentStore.SaveAsync(session.Id, _activeDocument);

                session.DocumentName = _activeDocument.Name;
                PendingDocumentName = _activeDocument.Name;

                Status.Message = $"{fileName} is ready with {_activeDocument.Chunks.Count} passages indexed.";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not read {FileName}", fileName);
                _dialogService.ShowInformation(exception.Message, "Could not read the document");

                PendingDocumentName = null;
                Status.Message = "Ready";
            }
            finally
            {
                IsIndexingDocument = false;

                SaveSessions();
            }
        }

        private async Task<string> FindDocumentContextAsync(ChatSession session, string prompt)
        {
            if (session.DocumentName == null)
                return string.Empty;

            var document = await EnsureDocumentLoadedAsync(session);

            if (document == null)
                return string.Empty;

            var chunks = await _documents.FindRelevantAsync(document, prompt, _documentOptions.RetrievedChunks);

            _logger.LogInformation("Retrieved {ChunkCount} passages from {DocumentName}", chunks.Count, document.Name);

            return string.Join("\n\n", chunks.Select(chunk => chunk.Text));
        }

        private async Task<IndexedDocument?> EnsureDocumentLoadedAsync(ChatSession session)
        {
            if (_activeDocument != null)
                return _activeDocument;

            if (!await Models.EnsureEmbeddingModelReadyAsync())
                return null;

            _activeDocument = await _documentStore.LoadAsync(session.Id);

            return _activeDocument;
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
            PendingDocumentName = null;
            PendingImagePath = null;
        }

        private async Task GenerateAnswerAsync(ChatMessage answer, List<ChatMessage> history, string prompt, string? imagePath, string documentContext)
        {
            IsGenerating = true;

            _generationCts = new CancellationTokenSource();

            try
            {
                await RequestAnswerAsync(answer, history, prompt, imagePath, documentContext);
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

        private async Task RequestAnswerAsync(ChatMessage answer, List<ChatMessage> history, string prompt, string? imagePath, string documentContext)
        {
            var model = await Models.EnsureActiveModelReadyAsync();

            if (model == null)
            {
                answer.Content = NoModelMessage;

                _dialogService.ShowModelManager();

                return;
            }

            await StreamAnswerAsync(answer, history, prompt, imagePath, documentContext, _generationCts!.Token);
        }

        private Task StreamAnswerAsync(
            ChatMessage answer,
            List<ChatMessage> history,
            string prompt,
            string? imagePath,
            string documentContext,
            CancellationToken cancellationToken)
        {
            return Task.Run(async () =>
            {
                var stream = _llmService.GenerateResponseStreamAsync(history, prompt, imagePath, documentContext, cancellationToken);

                await foreach (var token in stream.WithCancellation(cancellationToken))
                    await _uiDispatcher.InvokeAsync(() => answer.Content += token, cancellationToken);
            }, cancellationToken);
        }

        private void SaveSessions()
        {
            var snapshot = Sessions.ToList();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _chatStorage.SaveSessionsAsync(snapshot);
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Could not save the chat history");
                }
            });
        }

        #endregion
    }
}