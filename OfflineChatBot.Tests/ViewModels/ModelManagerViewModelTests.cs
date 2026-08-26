using OfflineChatBot.Tests.Fakes;
using OfflineChatBot.ViewModels;
using Xunit;

namespace OfflineChatBot.Tests.ViewModels
{
    public class ModelManagerViewModelTests
    {
        [Fact]
        public async Task RefreshAsync_OffersOnlyTheModelsAlreadyOnDisk()
        {
            var models = await CreateRefreshedAsync();

            Assert.All(models.DownloadedModels, model => Assert.True(model.IsDownloaded));
            Assert.DoesNotContain(models.DownloadedModels, model => model.FileName == "fake-other.gguf");
        }

        [Fact]
        public async Task RefreshAsync_SelectsADownloadedModel()
        {
            var models = await CreateRefreshedAsync();

            Assert.NotNull(models.SelectedModel);
            Assert.True(models.SelectedModel!.IsDownloaded);
        }

        [Fact]
        public async Task SelectModelAsync_IgnoresAModelThatIsNotDownloaded()
        {
            var catalog = new FakeModelManagerService();
            var models = await CreateRefreshedAsync(catalog);
            var missing = catalog.Models.Single(model => model.FileName == "fake-other.gguf");

            await models.SelectModelCommand.ExecuteAsync(missing);

            Assert.NotEqual(missing, models.SelectedModel);
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
                new FakeLogger<ModelManagerViewModel>());

            await models.RefreshAsync();

            return models;
        }
    }
}
