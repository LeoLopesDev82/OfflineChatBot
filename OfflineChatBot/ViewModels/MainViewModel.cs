using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private const string NoModelMessage = "[No downloaded model available. Please open Model Manager to download Qwen 2.5.]";
        private const string VisionRequiredMessage = "Image attachments require a vision model. Please select LLaVA 1.5 7B (Vision & Chat) before sending your message.";
        private const string PartedReadingMessage = "{0} holds {1} tokens, more than this model can read at once. It will be read in {2} parts, and every question will pay that cost. Attach it anyway?";

        private readonly ILlmService _llmService;
        private readonly IChatStorageService _chatStorage;
        private readonly IDialogService _dialogService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly ILogger<MainViewModel> _logger;
        private readonly IDocumentReader _reader;
        private readonly IDocumentScanner _scanner;
        private readonly IDocumentStore _documentStore;

        private CancellationTokenSource? _generationCts;
        private ReadDocument? _activeDocument;

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
        private bool _isReadingDocument;

        public MainViewModel(
            ILlmService llmService,
            IChatStorageService chatStorage,
            IDialogService dialogService,
            IUiDispatcher uiDispatcher,
            ILogger<MainViewModel> logger,
            IDocumentReader reader,
            IDocumentScanner scanner,
            IDocumentStore documentStore,
            ModelManagerViewModel models,
            AppStatusViewModel status)
        {
            _llmService = llmService;
            _chatStorage = chatStorage;
            _dialogService = dialogService;
            _uiDispatcher = uiDispatcher;
            _logger = logger;
            _reader = reader;
            _scanner = scanner;
            _documentStore = documentStore;

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
            var filePath = _dialogService.PickDocumentFile();

            if (string.IsNullOrEmpty(filePath))
                return;

            await ReadDocumentAsync(CurrentSession ?? CreateAndSelectSession(), filePath);
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

            await GenerateAnswerAsync(answer, session, history, prompt, imagePath);
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

        private bool IsIdle() => !IsReadingDocument;

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

        private async Task ReadDocumentAsync(ChatSession session, string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            PendingDocumentName = fileName;
            IsReadingDocument = true;
            Status.Message = $"Reading {fileName}...";

            try
            {
                var document = await Task.Run(() => _reader.ReadAsync(filePath));

                if (!ConfirmPartedReading(document))
                {
                    PendingDocumentName = null;
                    Status.Message = "Ready";

                    return;
                }

                await _documentStore.SaveAsync(session.Id, document.Text);

                _activeDocument = document;

                session.DocumentName = document.Name;
                PendingDocumentName = document.Name;

                Status.Message = document.FitsInOnePass
                    ? $"{document.Name} is attached with {document.Tokens} tokens, read in one pass."
                    : $"{document.Name} is attached with {document.Tokens} tokens, read in {document.Parts} parts per question.";
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
                IsReadingDocument = false;

                SaveSessions();
            }
        }

        private bool ConfirmPartedReading(ReadDocument document)
        {
            if (document.FitsInOnePass)
                return true;

            _logger.LogInformation("{DocumentName} needs {PartCount} parts per question", document.Name, document.Parts);

            return _dialogService.Confirm(string.Format(PartedReadingMessage, document.Name, document.Tokens, document.Parts), "This document will be read in parts");
        }

        private async Task<string> FindDocumentContextAsync(ChatSession session, string prompt)
        {
            if (session.DocumentName == null)
                return string.Empty;

            var document = await EnsureDocumentLoadedAsync(session);

            if (document == null)
                return string.Empty;

            if (document.FitsInOnePass)
            {
                _logger.LogInformation("Sending {DocumentName} in full with {TokenCount} tokens", document.Name, document.Tokens);

                return document.Text;
            }

            return await ScanInPartsAsync(document, prompt);
        }

        private async Task<string> ScanInPartsAsync(ReadDocument document, string prompt)
        {
            var progress = new Progress<ScanProgress>(ReportScanProgress);
            var token = _generationCts!.Token;
            var notes = await Task.Run(() => _scanner.ScanAsync(document, prompt, progress, token), token);

            Status.Message = "Writing the answer...";

            return notes;
        }

        private void ReportScanProgress(ScanProgress progress)
        {
            Status.Message = $"Reading part {progress.Part} of {progress.TotalParts}{RemainingSuffix(progress.Remaining)}...";
        }

        private static string RemainingSuffix(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
                return string.Empty;

            if (remaining.TotalMinutes < 1)
                return $", about {remaining.TotalSeconds:F0}s left";

            return $", about {remaining.TotalMinutes:F0} min left";
        }

        private async Task<ReadDocument?> EnsureDocumentLoadedAsync(ChatSession session)
        {
            if (_activeDocument != null)
                return _activeDocument;

            var text = await _documentStore.LoadAsync(session.Id);

            if (text == null)
                return null;

            _activeDocument = _reader.Measure(session.DocumentName!, text);

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

        private async Task GenerateAnswerAsync(ChatMessage answer, ChatSession session, List<ChatMessage> history, string prompt, string? imagePath)
        {
            IsGenerating = true;

            _generationCts = new CancellationTokenSource();

            try
            {
                var documentContext = await FindDocumentContextAsync(session, prompt);

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