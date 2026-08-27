using OfflineChatBot.Services.Abstractions;
using OfflineChatBot.Services.Documents;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class TextSplitterTests
    {
        [Fact]
        public void AShortText_StaysInOnePart()
        {
            var parts = Split("One two three.", 100);

            Assert.Single(parts);
            Assert.Equal("One two three.", parts[0]);
        }

        [Fact]
        public void NoPartGoesOverTheBudget()
        {
            var text = string.Join("\n\n", Enumerable.Range(1, 40).Select(number => $"Paragraph number {number} of the document."));
            var counter = new WordTokenCounter();
            var parts = new TextSplitter(counter).Split(text, 20);

            Assert.All(parts, part => Assert.True(counter.Count(part) <= 20, $"part had {counter.Count(part)} tokens"));
        }

        [Fact]
        public void EveryWordOfTheOriginalSurvives()
        {
            var text = string.Join("\n\n", Enumerable.Range(1, 40).Select(number => $"Paragraph number {number} of the document."));
            var parts = Split(text, 20);

            foreach (var number in Enumerable.Range(1, 40))
                Assert.Contains(parts, part => part.Contains($"number {number} of"));
        }

        [Fact]
        public void AParagraphLargerThanAPart_IsBrokenIntoSentences()
        {
            var text = string.Join(" ", Enumerable.Range(1, 12).Select(number => $"Sentence {number} is here."));
            var parts = Split(text, 10);

            Assert.True(parts.Count > 1);
            Assert.All(parts, part => Assert.True(new WordTokenCounter().Count(part) <= 10));
        }

        [Fact]
        public void ASentenceLargerThanAPart_IsBrokenIntoWords()
        {
            var text = string.Join(" ", Enumerable.Repeat("word", 50));
            var counter = new WordTokenCounter();
            var parts = new TextSplitter(counter).Split(text, 8);

            Assert.True(parts.Count >= 6);
            Assert.All(parts, part => Assert.True(counter.Count(part) <= 8));
        }

        [Fact]
        public void BlankParagraphs_AreDropped()
        {
            var parts = Split("First.\n\n\n\nSecond.", 100);

            Assert.Single(parts);
            Assert.Equal("First.\n\nSecond.", parts[0]);
        }

        [Fact]
        public void WindowsLineEndings_SplitParagraphsToo()
        {
            var parts = Split("First paragraph.\r\n\r\nSecond paragraph.", 2);

            Assert.Equal(["First paragraph.", "Second paragraph."], parts);
        }

        private static IReadOnlyList<string> Split(string text, int partTokens)
        {
            return new TextSplitter(new WordTokenCounter()).Split(text, partTokens);
        }

        private sealed class WordTokenCounter : ITokenCounter
        {
            public int Count(string text)
            {
                return text.Split([' ', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries).Length;
            }
        }
    }
}
