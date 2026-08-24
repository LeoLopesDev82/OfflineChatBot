using System.Text;
using OfflineChatBot.Services.Llm;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class StopTokenFilterTests
    {
        [Theory]
        [InlineData(new[] { "Hello there.", "\n<|im", "_end|>" }, "Hello there.\n")]
        [InlineData(new[] { "Hello.", "<|im_end|>" }, "Hello.")]
        [InlineData(new[] { "Bye.", "<", "|", "i", "m", "_", "e", "n", "d", "|", ">" }, "Bye.")]
        [InlineData(new[] { "Done.", "<|endof", "text|>" }, "Done.")]
        [InlineData(new[] { "Answer.", "<|im_st", "art|>user" }, "Answer.user")]
        [InlineData(new[] { "Plain ", "text ", "only." }, "Plain text only.")]
        [InlineData(new[] { "a < b ", "and c |> d" }, "a < b and c |> d")]
        [InlineData(new[] { "one<|im_end|>two" }, "onetwo")]
        public void Filter_RemovesStopTokensNoMatterHowTheyAreSplit(string[] chunks, string expected)
        {
            Assert.Equal(expected, RunThrough(chunks));
        }

        [Fact]
        public void Take_HoldsBackTextThatCouldStillBecomeAStopToken()
        {
            var filter = new StopTokenFilter();

            Assert.Equal("Hi ", filter.Take("Hi <|im"));
            Assert.Equal("there", filter.Take("_end|>there"));
        }

        [Fact]
        public void Flush_DropsATokenThatWasCutInHalfByTheEndOfTheStream()
        {
            Assert.Equal("Answer ", RunThrough(["Answer <|im"]));
        }

        [Fact]
        public void Flush_KeepsASingleCharacterThatOnlyLooksLikeTheStartOfAToken()
        {
            Assert.Equal("a < b <", RunThrough(["a < b <"]));
        }

        private static string RunThrough(string[] chunks)
        {
            var filter = new StopTokenFilter();
            var output = new StringBuilder();

            foreach (var chunk in chunks)
                output.Append(filter.Take(chunk));

            output.Append(filter.Flush());

            return output.ToString();
        }
    }
}