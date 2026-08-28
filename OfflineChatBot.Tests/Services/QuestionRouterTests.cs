using OfflineChatBot.Services.Llm;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class QuestionRouterTests
    {
        [Theory]
        [InlineData("obrigado!")]
        [InlineData("de nada")]
        [InlineData("Ola, tudo bem?")]
        [InlineData("Yo!")]
        [InlineData("bom dia")]
        [InlineData("ok, valeu")]
        [InlineData("beleza")]
        [InlineData("thanks!")]
        [InlineData("hello, how are you?")]
        [InlineData("perfeito, obrigado")]
        public void SmallTalk_DoesNotNeedTheDocument(string message)
        {
            Assert.False(new QuestionRouter().NeedsDocument(message));
        }

        [Theory]
        [InlineData("quem e o protagonista dessa historia?")]
        [InlineData("resuma o documento para mim")]
        [InlineData("qual o valor da casa verde?")]
        [InlineData("o arquivo fala sobre piscina?")]
        [InlineData("quais casas tem escritorio")]
        [InlineData("me fala mais sobre isso")]
        [InlineData("obrigado, e qual o prazo de entrega?")]
        public void AnythingWithContent_NeedsTheDocument(string message)
        {
            Assert.True(new QuestionRouter().NeedsDocument(message));
        }

        [Fact]
        public void AnEmptyMessage_ErrsTowardsReading()
        {
            Assert.True(new QuestionRouter().NeedsDocument("   "));
        }

        [Fact]
        public void ALongMessageOfNothingButPleasantries_StillReads()
        {
            Assert.True(new QuestionRouter().NeedsDocument("oi tudo bem obrigado valeu beleza ok certo"));
        }
    }
}
