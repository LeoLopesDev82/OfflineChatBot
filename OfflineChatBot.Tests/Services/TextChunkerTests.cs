using OfflineChatBot.Services.Documents;
using OfflineChatBot.Tests.Fakes;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class TextChunkerTests
    {
        [Fact]
        public void Split_ShortText_ProducesASingleChunk()
        {
            var chunks = Create().Split("A short paragraph that fits comfortably.", chunkTokens: 100, overlapTokens: 10);

            Assert.Single(chunks);
            Assert.Equal(0, chunks[0].Index);
        }

        [Fact]
        public void Split_NumbersTheChunksInReadingOrder()
        {
            var chunks = Create().Split(Paragraphs(12), chunkTokens: 40, overlapTokens: 0);

            Assert.True(chunks.Count > 1);
            Assert.Equal(Enumerable.Range(0, chunks.Count), chunks.Select(chunk => chunk.Index));
        }

        [Fact]
        public void Split_KeepsEveryChunkWithinTheRequestedSize()
        {
            var counter = new FakeTokenCounter();

            var chunks = Create(counter).Split(Paragraphs(30), chunkTokens: 60, overlapTokens: 10);

            Assert.All(chunks, chunk => Assert.True(counter.Count(chunk.Text) <= 60, $"chunk of {counter.Count(chunk.Text)} tokens"));
        }

        [Fact]
        public void Split_LosesNoContent()
        {
            var chunks = Create().Split(Paragraphs(10), chunkTokens: 40, overlapTokens: 0);
            var joined = string.Join(" ", chunks.Select(chunk => chunk.Text));

            for (var index = 1; index <= 10; index++)
                Assert.Contains($"Paragraph {index} ", joined);
        }

        [Fact]
        public void Split_WithOverlap_RepeatsTheTailOfThePreviousChunk()
        {
            var withoutOverlap = Create().Split(Paragraphs(20), chunkTokens: 60, overlapTokens: 0);
            var withOverlap = Create().Split(Paragraphs(20), chunkTokens: 60, overlapTokens: 20);

            Assert.True(withOverlap.Count > withoutOverlap.Count);
        }

        [Fact]
        public void Split_ParagraphLongerThanAChunk_IsBrokenDown()
        {
            var counter = new FakeTokenCounter();
            var giant = string.Join(" ", Enumerable.Repeat("word", 400));

            var chunks = Create(counter).Split(giant, chunkTokens: 50, overlapTokens: 0);

            Assert.True(chunks.Count > 1);
            Assert.All(chunks, chunk => Assert.True(counter.Count(chunk.Text) <= 50));
        }

        [Fact]
        public void Split_CollapsesLineBreaksInsideAParagraph()
        {
            var chunks = Create().Split("One line\nsecond line\nthird line", chunkTokens: 100, overlapTokens: 0);

            Assert.Equal("One line second line third line", Assert.Single(chunks).Text);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("\n\n\n")]
        public void Split_EmptyText_ProducesNoChunks(string text)
        {
            Assert.Empty(Create().Split(text, chunkTokens: 100, overlapTokens: 10));
        }

        private static TextChunker Create(FakeTokenCounter? counter = null)
        {
            return new TextChunker(counter ?? new FakeTokenCounter());
        }

        private static string Paragraphs(int count)
        {
            var paragraphs = Enumerable.Range(1, count)
                .Select(index => $"Paragraph {index} carries a sentence with a handful of words in it.");

            return string.Join("\n\n", paragraphs);
        }
    }
}
