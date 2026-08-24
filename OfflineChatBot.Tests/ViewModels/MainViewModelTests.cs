using Microsoft.Extensions.Logging;
using OfflineChatBot.Models;
using OfflineChatBot.Tests.Fakes;
using OfflineChatBot.ViewModels;
using Xunit;

namespace OfflineChatBot.Tests.ViewModels
{
    public class MainViewModelTests
    {
        [Fact]
        public async Task SendMessageAsync_StreamsTheAnswerIntoTheAssistantMessage()
        {
            var context = await TestContext.CreateAsync("Hello", " there", "!");

            context.ViewModel.UserInput = "Hi";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            var messages = context.ViewModel.CurrentSession!.Messages;

            Assert.Equal(2, messages.Count);
            Assert.Equal("Hi", messages[0].Content);
            Assert.True(messages[0].IsUser);
            Assert.Equal("Hello there!", messages[1].Content);
            Assert.True(messages[1].IsAssistant);
            Assert.False(messages[1].IsStreaming);
        }

        [Fact]
        public async Task SendMessageAsync_SendsTheHistoryWithoutTheCurrentExchange()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.UserInput = "First";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            context.ViewModel.UserInput = "Second";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal("Second", context.Llm.LastPrompt);
            Assert.Equal(2, context.Llm.LastHistory.Count);
            Assert.Equal("First", context.Llm.LastHistory[0].Content);
        }

        [Fact]
        public async Task SendMessageAsync_ClearsTheComposer()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.UserInput = "Hi";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal(string.Empty, context.ViewModel.UserInput);
            Assert.Null(context.ViewModel.PendingImagePath);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SendMessageAsync_BlankInput_DoesNothing(string input)
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.UserInput = input;

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Empty(context.ViewModel.CurrentSession!.Messages);
        }

        [Fact]
        public async Task SendMessageAsync_FirstMessage_RenamesTheSession()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.UserInput = "Explain dependency injection";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Equal("Explain dependency injection", context.ViewModel.CurrentSession!.Title);
        }

        [Fact]
        public async Task SendMessageAsync_ImageWithoutVisionModel_WarnsAndSendsNothing()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.UserInput = "What is this?";
            context.ViewModel.PendingImagePath = @"C:\pictures\photo.png";

            await context.ViewModel.SendMessageCommand.ExecuteAsync(null);

            Assert.Empty(context.ViewModel.CurrentSession!.Messages);
            Assert.Single(context.Dialogs.Information);
        }

        [Fact]
        public async Task CreateNewChat_InsertsAtTheTopAndSelectsIt()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.CreateNewChatCommand.Execute(null);

            Assert.Equal(2, context.ViewModel.Sessions.Count);
            Assert.Same(context.ViewModel.Sessions[0], context.ViewModel.CurrentSession);
        }

        [Fact]
        public async Task DeleteChat_WhenDeclined_KeepsTheSession()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.Dialogs.ConfirmResult = false;

            context.ViewModel.DeleteChatCommand.Execute(context.ViewModel.CurrentSession);

            Assert.Single(context.ViewModel.Sessions);
            Assert.Equal(1, context.Dialogs.ConfirmCount);
        }

        [Fact]
        public async Task DeleteChat_WhenConfirmed_RemovesItAndKeepsOneSessionOpen()
        {
            var context = await TestContext.CreateAsync("Answer");

            var removed = context.ViewModel.CurrentSession;

            context.ViewModel.DeleteChatCommand.Execute(removed);

            Assert.NotNull(context.ViewModel.CurrentSession);
            Assert.DoesNotContain(removed, context.ViewModel.Sessions);
        }

        [Fact]
        public async Task BeginRenameChat_ClosesAnyOtherOpenEditor()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.ViewModel.CreateNewChatCommand.Execute(null);

            var first = context.ViewModel.Sessions[0];
            var second = context.ViewModel.Sessions[1];

            context.ViewModel.BeginRenameChatCommand.Execute(first);
            context.ViewModel.BeginRenameChatCommand.Execute(second);

            Assert.False(first.IsEditing);
            Assert.True(second.IsEditing);
        }

        [Fact]
        public async Task InitializeAsync_WithStoredSessions_SelectsTheFirstOne()
        {
            var stored = new ChatSession { Title = "Yesterday" };

            stored.AddUserMessage("Old question", null);

            var context = await TestContext.CreateAsync(["Answer"], [stored]);

            Assert.Single(context.ViewModel.Sessions);
            Assert.Equal("Yesterday", context.ViewModel.CurrentSession!.Title);
        }

        [Fact]
        public async Task SelectModel_WhenLoadingFails_ReportsItOnScreenAndInTheLog()
        {
            var context = await TestContext.CreateAsync("Answer");

            context.Llm.LoadFailure = new InvalidOperationException("model file is corrupt");
            context.Llm.IsLoaded = false;

            await context.Models.SelectModelCommand.ExecuteAsync(context.Models.DownloadedModels[0]);

            Assert.Contains("model file is corrupt", context.ViewModel.Status.Message);

            var logged = Assert.Single(context.ModelsLog.Problems);

            Assert.Equal(LogLevel.Error, logged.Level);
            Assert.Equal("model file is corrupt", logged.Exception!.Message);
        }

        private sealed class TestContext
        {
            public required MainViewModel ViewModel { get; init; }
            public required ModelManagerViewModel Models { get; init; }
            public required FakeLlmService Llm { get; init; }
            public required FakeDialogService Dialogs { get; init; }
            public required FakeChatStorageService Storage { get; init; }
            public required FakeLogger<ModelManagerViewModel> ModelsLog { get; init; }

            public static Task<TestContext> CreateAsync(params string[] tokens)
            {
                return CreateAsync(tokens, []);
            }

            public static async Task<TestContext> CreateAsync(string[] tokens, ChatSession[] storedSessions)
            {
                var llm = new FakeLlmService(tokens);
                var dialogs = new FakeDialogService();
                var storage = new FakeChatStorageService { Stored = storedSessions.ToList() };
                var status = new AppStatusViewModel(new FakeResourceMonitor());
                var modelsLog = new FakeLogger<ModelManagerViewModel>();
                var models = new ModelManagerViewModel(new FakeModelManagerService(), llm, dialogs, status, modelsLog);
                var viewModel = new MainViewModel(llm, storage, dialogs, new ImmediateUiDispatcher(), new FakeLogger<MainViewModel>(), models, status);

                await viewModel.InitializeAsync();
                await models.EnsureActiveModelReadyAsync();

                return new TestContext
                {
                    ViewModel = viewModel,
                    Models = models,
                    Llm = llm,
                    Dialogs = dialogs,
                    Storage = storage,
                    ModelsLog = modelsLog
                };
            }
        }
    }
}