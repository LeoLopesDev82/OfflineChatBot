using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Tests.Fakes;
using OfflineChatBot.ViewModels;
using Xunit;

namespace OfflineChatBot.Tests.ViewModels
{
    public class DocumentAttachmentTests
    {
        [Fact]
        public async Task AttachDocument_WhileIndexing_ShowsTheFileAndBlocksSending()
        {
            var context = await CreateAsync();

            context.Documents.Gate = new TaskCompletionSource();
            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            var attaching = context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.True(context.ViewModel.IsIndexingDocument);
            Assert.Equal("contract.pdf", context.ViewModel.PendingDocumentName);
            Assert.False(context.ViewModel.SendMessageCommand.CanExecute(null));

            context.Documents.Gate.SetResult();

            await attaching;

            Assert.False(context.ViewModel.IsIndexingDocument);
            Assert.True(context.ViewModel.SendMessageCommand.CanExecute(null));
        }

        [Fact]
        public async Task AttachDocument_WithoutTheEmbeddingModel_ExplainsWhatIsMissing()
        {
            var context = await CreateAsync(embeddingDownloaded: false);

            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.False(context.ViewModel.HasPendingDocument);
            Assert.Contains(context.Dialogs.Information, message => message.Contains("Embedding"));
        }

        [Fact]
        public async Task AttachDocument_IndexesTheFileAndRemembersIt()
        {
            var context = await CreateAsync();

            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.True(context.ViewModel.HasPendingDocument);
            Assert.Equal("contract.pdf", context.ViewModel.PendingDocumentName);
            Assert.Single(context.DocumentStore.Stored);
        }

        [Fact]
        public async Task AttachDocument_WhenTheUserPicksNothing_DoesNothing()
        {
            var context = await CreateAsync();

            context.Dialogs.PickedDocument = null;

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.False(context.ViewModel.HasPendingDocument);
            Assert.Empty(context.DocumentStore.Stored);
        }

        [Fact]
        public async Task AttachDocument_WhenReadingFails_TellsTheUserAndKeepsNoDocument()
        {
            var context = await CreateAsync();

            context.Documents.IndexFailure = new InvalidOperationException("No text could be read from contract.pdf");
            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.False(context.ViewModel.HasPendingDocument);
            Assert.Contains(context.Dialogs.Information, message => message.Contains("No text could be read"));
        }

        [Fact]
        public async Task SendMessage_WithAnAttachedDocument_SendsTheRetrievedPassages()
        {
            var context = await CreateAsync();

            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            context.ViewModel.UserInput = "How long is the delivery?";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal("How long is the delivery?", context.Documents.LastQuestion);
            Assert.Contains("delivery takes thirty days", context.Llm.LastDocumentContext);
        }

        [Fact]
        public async Task SendMessage_WithoutADocument_SendsNoContext()
        {
            var context = await CreateAsync();

            context.ViewModel.UserInput = "Hello";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal(string.Empty, context.Llm.LastDocumentContext);
        }

        [Fact]
        public async Task SendMessage_CarriesTheDocumentIntoTheSentMessageAndClearsTheComposer()
        {
            var context = await CreateAsync();

            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            context.ViewModel.UserInput = "How long is the delivery?";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            var sent = context.ViewModel.CurrentSession!.Messages.First(message => message.IsUser);

            Assert.Equal("contract.pdf", sent.AttachedDocumentName);
            Assert.False(context.ViewModel.HasPendingDocument);
        }

        [Fact]
        public async Task RemoveAttachedDocument_ForgetsAndErasesIt()
        {
            var context = await CreateAsync();

            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            var sessionId = context.ViewModel.CurrentSession!.Id;

            await context.ViewModel.RemoveAttachedDocumentCommand.ExecuteAsync(null);

            Assert.False(context.ViewModel.HasPendingDocument);
            Assert.Contains(sessionId, context.DocumentStore.Deleted);
        }

        [Fact]
        public async Task DeleteChat_AlsoErasesTheDocumentOfThatChat()
        {
            var context = await CreateAsync();

            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            var session = context.ViewModel.CurrentSession!;

            context.ViewModel.DeleteChatCommand.Execute(session);

            Assert.Contains(session.Id, context.DocumentStore.Deleted);
        }

        private static async Task<TestContext> CreateAsync(bool embeddingDownloaded = true)
        {
            var catalog = new FakeModelManagerService();

            catalog.Models.Single(model => model.IsEmbeddingModel).IsDownloaded = embeddingDownloaded;

            var llm = new FakeLlmService("Answer");
            var dialogs = new FakeDialogService();
            var documents = new FakeDocumentIndexService();
            var documentStore = new FakeDocumentStore();
            var status = new AppStatusViewModel(new FakeResourceMonitor(), llm);
            var models = new ModelManagerViewModel(catalog, llm, dialogs, status, new FakeLogger<ModelManagerViewModel>(), new FakeEmbeddingService());

            var viewModel = new MainViewModel(
                llm,
                new FakeChatStorageService(),
                dialogs,
                new ImmediateUiDispatcher(),
                new FakeLogger<MainViewModel>(),
                documents,
                documentStore,
                Options.Create(new DocumentOptions()),
                models,
                status);

            await viewModel.InitializeAsync();

            return new TestContext(viewModel, dialogs, documents, documentStore, llm);
        }

        private sealed record TestContext(
            MainViewModel ViewModel,
            FakeDialogService Dialogs,
            FakeDocumentIndexService Documents,
            FakeDocumentStore DocumentStore,
            FakeLlmService Llm);
    }
}
