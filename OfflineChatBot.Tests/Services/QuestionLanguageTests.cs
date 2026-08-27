using OfflineChatBot.Services.Llm;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class QuestionLanguageTests
    {
        [Theory]
        [InlineData("qual o valor da casa Sobrado?")]
        [InlineData("existe informacao se tem piscina?")]
        [InlineData("pode me dizer que tipo de dados tem essa planilha?")]
        [InlineData("me explica o que e uma lista encadeada")]
        public void PortugueseQuestions_AreRecognised(string question)
        {
            Assert.Equal("Portuguese", QuestionLanguage.Of(question));
        }

        [Theory]
        [InlineData("what is a linked list?")]
        [InlineData("Which houses have an office?")]
        [InlineData("can you show me an example please")]
        public void EnglishQuestions_AreRecognised(string question)
        {
            Assert.Equal("English", QuestionLanguage.Of(question));
        }

        [Fact]
        public void AGermanQuestion_IsRecognised()
        {
            Assert.Equal("German", QuestionLanguage.Of("was ist eine verkettete Liste?"));
        }

        [Fact]
        public void TextWithNoMarkers_StaysUndecided()
        {
            Assert.Null(QuestionLanguage.Of("Sobrado 1680"));
        }

        [Fact]
        public void EmptyText_StaysUndecided()
        {
            Assert.Null(QuestionLanguage.Of("   "));
        }

        [Fact]
        public void ADrawBetweenLanguages_StaysUndecided()
        {
            Assert.Null(QuestionLanguage.Of("sobre"));
        }
    }
}
