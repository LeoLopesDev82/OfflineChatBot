using OfflineChatBot.Models;
using OfflineChatBot.Tests.Fakes;
using OfflineChatBot.ViewModels;
using Xunit;

namespace OfflineChatBot.Tests.ViewModels
{
    public class ModelManagerViewModelTests
    {
        [Fact]
        public async Task RefreshAsync_KeepsEmbeddingModelsOutOfTheConversationList()
        {
            var models = await CreateRefreshedAsync();

            Assert.DoesNotContain(models.DownloadedModels, model => model.IsEmbeddingModel);
            Assert.All(models.DownloadedModels, model => Assert.True(model.IsConversational));
        }

        [Fact]
        public async Task RefreshAsync_FindsTheEmbeddingModelInTheCatalog()
        {
            var models = await CreateRefreshedAsync();

            Assert.NotNull(models.EmbeddingModel);
            Assert.True(models.EmbeddingModel!.IsEmbeddingModel);
        }

        [Fact]
        public async Task CanReadDocuments_IsFalseWhileTheEmbeddingModelIsNotDownloaded()
        {
            var models = await CreateRefreshedAsync();

            Assert.False(models.CanReadDocuments);
        }

        [Fact]
        public async Task CanReadDocuments_IsTrueOnceTheEmbeddingModelIsDownloaded()
        {
            var catalog = new FakeModelManagerService();

            catalog.Models.Single(model => model.IsEmbeddingModel).IsDownloaded = true;

            var models = await CreateRefreshedAsync(catalog);

            Assert.True(models.CanReadDocuments);
        }

        [Fact]
        public async Task SelectModelAsync_IgnoresAnEmbeddingModel()
        {
            var catalog = new FakeModelManagerService();
            var embedding = catalog.Models.Single(model => model.IsEmbeddingModel);

            embedding.IsDownloaded = true;

            var models = await CreateRefreshedAsync(catalog);

            await models.SelectModelCommand.ExecuteAsync(embedding);

            Assert.NotEqual(embedding, models.SelectedModel);
        }

        private static async Task<ModelManagerViewModel> CreateRefreshedAsync(FakeModelManagerService? catalog = null)
        {
            var llm = new FakeLlmService("Answer");
            var status = new AppStatusViewModel(new FakeResourceMonitor(), llm);
            var models = new ModelManagerViewModel(
                catalog ?? new FakeModelManagerService(),
                llm,
                new FakeDialogService(),
                status,
                new FakeLogger<ModelManagerViewModel>(),
                new FakeEmbeddingService());

            await models.RefreshAsync();

            return models;
        }
    }
}
