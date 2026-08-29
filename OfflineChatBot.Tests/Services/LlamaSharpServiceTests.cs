using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Llm;
using OfflineChatBot.Tests.Fakes;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class LlamaSharpServiceTests
    {
        [Fact]
        public async Task CompleteAsync_WithoutAModel_SaysSoInsteadOfFailingObscurely()
        {
            using var service = CreateService();

            var failure = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAsync("A question", "Some content"));

            Assert.Contains("No model is loaded", failure.Message);
        }

        [Fact]
        public async Task CompleteAsync_WhenItFails_LeavesTheModelFreeForTheNextCall()
        {
            using var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAsync("A question", "Some content"));

            var second = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CompleteAsync("A question", "Some content"));

            Assert.Contains("No model is loaded", second.Message);
        }

        [Fact]
        public async Task GenerateResponseStream_WhenItFails_LeavesTheModelFreeForTheNextCall()
        {
            using var service = CreateService();

            await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(service));

            var second = await Assert.ThrowsAsync<InvalidOperationException>(() => Drain(service));

            Assert.Contains("No model is loaded", second.Message);
        }

        private static async Task Drain(LlamaSharpService service)
        {
            await foreach (var _ in service.GenerateResponseStreamAsync("chat-1", [], "A question"))
            {
            }
        }

        private static LlamaSharpService CreateService()
        {
            var options = Options.Create(new GenerationOptions
            {
                ContextSize = 8192,
                MaxTokens = 256,
                UseGpu = false
            });

            return new LlamaSharpService(options, new FakeLogger<LlamaSharpService>());
        }
    }
}
