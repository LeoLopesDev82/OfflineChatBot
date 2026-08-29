using System.Globalization;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.ViewModels
{
    public partial class DocumentAttachmentViewModel : ObservableObject
    {
        private const string PartedReadingMessage = "{0} holds {1} tokens, more than this model can read at once. It will be read in {2} parts, and every question will pay that cost. Attach it anyway?";
        private const string ReplaceDocumentMessage = "This chat already has {0} attached, and a chat holds one document at a time. Replace it with {1}? Earlier messages keep showing {0}, but from now on questions are answered from {1}.";

        private readonly IDialogService _dialogService;
        private readonly ILogger<DocumentAttachmentViewModel> _logger;
        private readonly IDocumentReader _reader;
        private readonly IDocumentScanner _scanner;
        private readonly ISpreadsheetQueryService _spreadsheets;
        private readonly IQuestionRouter _router;
        private readonly IDocumentStore _documentStore;
        private readonly AppStatusViewModel _status;

        private ChatSession? _session;
        private ReadDocument? _activeDocument;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPendingDocument))]
        private string? _pendingDocumentName;

        [ObservableProperty]
        private bool _isReadingDocument;

        public DocumentAttachmentViewModel(
            IDialogService dialogService,
            ILogger<DocumentAttachmentViewModel> logger,
            IDocumentReader reader,
            IDocumentScanner scanner,
            ISpreadsheetQueryService spreadsheets,
            IQuestionRouter router,
            IDocumentStore documentStore,
            AppStatusViewModel status)
        {
            _dialogService = dialogService;
            _logger = logger;
            _reader = reader;
            _scanner = scanner;
            _spreadsheets = spreadsheets;
            _router = router;
            _documentStore = documentStore;
            _status = status;
        }

        public bool HasPendingDocument => !string.IsNullOrEmpty(PendingDocumentName);

        public bool HasActiveDocument => _session?.DocumentName != null;

        public string ActiveDocumentSummary => Describe();

        public void ClearPending()
        {
            PendingDocumentName = null;
        }

        public void UseSession(ChatSession? session)
        {
            _session = session;
            _activeDocument = null;
            PendingDocumentName = null;

            NotifyActiveDocument();
        }

        public async Task AttachAsync()
        {
            var session = _session;

            if (session == null)
                return;

            var filePath = _dialogService.PickDocumentFile();

            if (string.IsNullOrEmpty(filePath))
                return;

            if (!await ConfirmReplacementAsync(session, filePath))
                return;

            await ReadAsync(session, filePath);
        }

        public async Task RemoveAsync()
        {
            var session = _session;

            if (session?.DocumentName == null)
                return;

            await _documentStore.DeleteAsync(session.Id);

            session.DocumentName = null;
            session.DocumentPath = null;
            session.DocumentTokens = 0;
            session.DocumentParts = 0;

            _activeDocument = null;
            PendingDocumentName = null;

            NotifyActiveDocument();
        }

        public async Task<string> FindContextAsync(ChatSession session, string prompt, CancellationToken cancellationToken)
        {
            if (session.DocumentName == null)
                return string.Empty;

            var document = await EnsureLoadedAsync(session);

            if (document == null)
                return string.Empty;

            if (CostsAPass(session, document) && !_router.NeedsDocument(prompt))
            {
                _logger.LogInformation("Left {DocumentName} alone, the message is not about it", document.Name);

                return document.FitsInOnePass ? document.Text : string.Empty;
            }

            var queried = await QuerySpreadsheetAsync(session, prompt, cancellationToken);

            if (document.FitsInOnePass)
            {
                _logger.LogInformation("Sending {DocumentName} in full with {TokenCount} tokens", document.Name, document.Tokens);

                return Join(document.Text, queried.Text);
            }

            return Join(await ScanInPartsAsync(document, prompt, cancellationToken), queried.Text);
        }

        #region Private Methods

        private void NotifyActiveDocument()
        {
            OnPropertyChanged(nameof(HasActiveDocument));
            OnPropertyChanged(nameof(ActiveDocumentSummary));
        }

        private string Describe()
        {
            var session = _session;

            if (session?.DocumentName == null)
                return string.Empty;

            if (session.DocumentTokens == 0)
                return session.DocumentName;

            var size = $"{session.DocumentName} · {session.DocumentTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens";

            return session.DocumentParts > 1
                ? $"{size} · read in {session.DocumentParts} parts on every question"
                : $"{size} · read in one pass";
        }

        private async Task ReadAsync(ChatSession session, string filePath)
        {
            var fileName = Path.GetFileName(filePath);

            PendingDocumentName = fileName;
            IsReadingDocument = true;
            _status.Message = $"Reading {fileName}...";

            try
            {
                var document = await Task.Run(() => _reader.ReadAsync(filePath));

                if (!await ConfirmPartedReadingAsync(document))
                {
                    PendingDocumentName = null;
                    _status.Message = "Ready";

                    return;
                }

                await _documentStore.SaveAsync(session.Id, document.Text);

                _activeDocument = document;

                session.DocumentName = document.Name;
                session.DocumentPath = filePath;
                session.DocumentTokens = document.Tokens;
                session.DocumentParts = document.Parts;
                PendingDocumentName = document.Name;

                NotifyActiveDocument();

                _status.Message = document.FitsInOnePass
                    ? $"{document.Name} is attached with {document.Tokens} tokens, read in one pass."
                    : $"{document.Name} is attached with {document.Tokens} tokens, read in {document.Parts} parts per question.";
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not read {FileName}", fileName);
                await _dialogService.ShowInformationAsync(exception.Message, "Could not read the document");

                PendingDocumentName = null;
                _status.Message = "Ready";
            }
            finally
            {
                IsReadingDocument = false;
            }
        }

        private async Task<bool> ConfirmReplacementAsync(ChatSession session, string filePath)
        {
            var attached = session.DocumentName;
            var picked = Path.GetFileName(filePath);

            if (attached == null || attached.Equals(picked, StringComparison.OrdinalIgnoreCase))
                return true;

            return await _dialogService.ConfirmAsync(string.Format(ReplaceDocumentMessage, attached, picked), "Replace the attached document");
        }

        private async Task<bool> ConfirmPartedReadingAsync(ReadDocument document)
        {
            if (document.FitsInOnePass)
                return true;

            _logger.LogInformation("{DocumentName} needs {PartCount} parts per question", document.Name, document.Parts);

            return await _dialogService.ConfirmAsync(string.Format(PartedReadingMessage, document.Name, document.Tokens, document.Parts), "This document will be read in parts");
        }

        private bool CostsAPass(ChatSession session, ReadDocument document)
        {
            return !document.FitsInOnePass || _spreadsheets.CanQuery(session.DocumentPath);
        }

        private async Task<QueryOutcome> QuerySpreadsheetAsync(ChatSession session, string prompt, CancellationToken cancellationToken)
        {
            if (!_spreadsheets.CanQuery(session.DocumentPath))
                return new QueryOutcome(false, string.Empty);

            _status.Message = "Querying the spreadsheet...";

            return await Task.Run(() => _spreadsheets.AskAsync(session.DocumentPath!, prompt, cancellationToken), cancellationToken);
        }

        private static string Join(string document, string queried)
        {
            return queried.Length == 0 ? document : $"{document}\n\n{queried}";
        }

        private async Task<string> ScanInPartsAsync(ReadDocument document, string prompt, CancellationToken cancellationToken)
        {
            var progress = new Progress<ScanProgress>(ReportScanProgress);
            var notes = await Task.Run(() => _scanner.ScanAsync(document, prompt, progress, cancellationToken), cancellationToken);

            _status.Message = "Writing the answer...";

            return notes;
        }

        private void ReportScanProgress(ScanProgress progress)
        {
            _status.Message = $"Reading part {progress.Part} of {progress.TotalParts}{RemainingSuffix(progress.Remaining)}...";
        }

        private static string RemainingSuffix(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero)
                return string.Empty;

            if (remaining.TotalMinutes < 1)
                return $", about {remaining.TotalSeconds:F0}s left";

            return $", about {remaining.TotalMinutes:F0} min left";
        }

        private async Task<ReadDocument?> EnsureLoadedAsync(ChatSession session)
        {
            if (_activeDocument != null)
                return _activeDocument;

            var text = await _documentStore.LoadAsync(session.Id);

            if (text == null)
                return null;

            _activeDocument = _reader.Measure(session.DocumentName!, text);

            return _activeDocument;
        }

        #endregion
    }
}
