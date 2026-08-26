using OfflineChatBot.Tests.Fakes;
using OfflineChatBot.ViewModels;
using Xunit;

namespace OfflineChatBot.Tests.ViewModels
{
    public class DocumentAttachmentTests
    {
        [Fact]
        public async Task AttachDocument_KeepsTheWholeTextAndRemembersTheFile()
        {
            var context = await CreateAsync();

            context.Reader.Text = "The delivery takes thirty days.";
            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.True(context.ViewModel.HasPendingDocument);
            Assert.Equal("contract.pdf", context.ViewModel.PendingDocumentName);
            Assert.Equal("The delivery takes thirty days.", context.DocumentStore.Stored.Values.Single());
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

            context.Reader.ReadFailure = new InvalidOperationException("No text could be read from contract.pdf");
            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.False(context.ViewModel.HasPendingDocument);
            Assert.Contains(context.Dialogs.Information, message => message.Contains("No text could be read"));
        }

        [Fact]
        public async Task AttachDocument_WhenTheFileNeedsMoreThanOnePass_RefusesAndSaysSo()
        {
            var context = await CreateAsync();

            context.Reader.Parts = 7;
            context.Reader.Tokens = 86000;
            context.Dialogs.PickedDocument = @"C:\docs\book.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.False(context.ViewModel.HasPendingDocument);
            Assert.Empty(context.DocumentStore.Stored);
            Assert.Contains(context.Dialogs.Information, message => message.Contains("86000"));
        }

        [Fact]
        public async Task AttachDocument_WhileReading_ShowsTheFileAndBlocksSending()
        {
            var context = await CreateAsync();

            context.Reader.Gate = new TaskCompletionSource();
            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            var attaching = context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            Assert.True(context.ViewModel.IsReadingDocument);
            Assert.Equal("contract.pdf", context.ViewModel.PendingDocumentName);
            Assert.False(context.ViewModel.SendMessageCommand.CanExecute(null));

            context.Reader.Gate.SetResult();

            await attaching;

            Assert.False(context.ViewModel.IsReadingDocument);
            Assert.True(context.ViewModel.SendMessageCommand.CanExecute(null));
        }

        [Fact]
        public async Task SendMessage_WithAnAttachedDocument_SendsTheWholeText()
        {
            var context = await CreateAsync();

            context.Reader.Text = "Clause 7. The delivery takes thirty days. Clause 8. The warranty lasts twelve months.";
            context.Dialogs.PickedDocument = @"C:\docs\contract.pdf";

            await context.ViewModel.AttachDocumentCommand.ExecuteAsync(null);

            context.ViewModel.UserInput = "How long is the delivery?";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal(context.Reader.Text, context.Llm.LastDocumentContext);
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
        public async Task SendMessage_SendsTheConversationIdentity()
        {
            var context = await CreateAsync();

            context.ViewModel.UserInput = "Hello";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal(context.ViewModel.CurrentSession!.Id, context.Llm.LastConversationId);
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

        private static async Task<TestContext> CreateAsync()
        {
            var llm = new FakeLlmService("Answer");
            var dialogs = new FakeDialogService();
            var reader = new FakeDocumentReader();
            var documentStore = new FakeDocumentStore();
            var status = new AppStatusViewModel(new FakeResourceMonitor(), llm);
            var models = new ModelManagerViewModel(new FakeModelManagerService(), llm, dialogs, status, new FakeLogger<ModelManagerViewModel>());

            var viewModel = new MainViewModel(
                llm,
                new FakeChatStorageService(),
                dialogs,
                new ImmediateUiDispatcher(),
                new FakeLogger<MainViewModel>(),
                reader,
                documentStore,
                models,
                status);

            await viewModel.InitializeAsync();

            return new TestContext(viewModel, dialogs, reader, documentStore, llm);
        }

        private sealed record TestContext(
            MainViewModel ViewModel,
            FakeDialogService Dialogs,
            FakeDocumentReader Reader,
            FakeDocumentStore DocumentStore,
            FakeLlmService Llm);
    }
}
