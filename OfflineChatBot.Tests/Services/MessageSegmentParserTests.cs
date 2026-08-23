using OfflineChatBot.Services.Chat;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class MessageSegmentParserTests
    {
        [Fact]
        public void Parse_PlainText_ReturnsSingleTextSegment()
        {
            var segments = MessageSegmentParser.Parse("Just a plain answer.");

            var segment = Assert.Single(segments);

            Assert.False(segment.IsCode);
            Assert.Equal("Just a plain answer.", segment.Text);
        }

        [Fact]
        public void Parse_TextAroundCodeBlock_ReturnsThreeSegmentsInOrder()
        {
            var content = "Before\n```csharp\nvar x = 1;\n```\nAfter";

            var segments = MessageSegmentParser.Parse(content);

            Assert.Equal(3, segments.Count);
            Assert.Equal("Before", segments[0].Text);
            Assert.True(segments[1].IsCode);
            Assert.Equal("csharp", segments[1].Language);
            Assert.Equal("var x = 1;", segments[1].Code);
            Assert.Equal("After", segments[2].Text);
        }

        [Fact]
        public void Parse_CodeBlockWithoutLanguage_FallsBackToCode()
        {
            var segments = MessageSegmentParser.Parse("```\nSELECT 1\n```");

            var segment = Assert.Single(segments);

            Assert.True(segment.IsCode);
            Assert.Equal("code", segment.Language);
        }

        [Fact]
        public void Parse_LanguagePrefixedBlock_NormalizesLanguage()
        {
            var segments = MessageSegmentParser.Parse("```language:python\nprint(1)\n```");

            Assert.Equal("python", Assert.Single(segments).Language);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Parse_EmptyContent_ReturnsNoSegments(string? content)
        {
            Assert.Empty(MessageSegmentParser.Parse(content));
        }
    }
}