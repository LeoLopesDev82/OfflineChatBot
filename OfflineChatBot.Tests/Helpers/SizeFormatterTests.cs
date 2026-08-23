using OfflineChatBot.Helpers;
using Xunit;

namespace OfflineChatBot.Tests.Helpers
{
    public class SizeFormatterTests
    {
        [Theory]
        [InlineData(0, "0 MB")]
        [InlineData(397, "397 MB")]
        [InlineData(1023, "1023 MB")]
        [InlineData(1024, "1.00 GB")]
        [InlineData(4700, "4.59 GB")]
        public void FromMegabytes_SwitchesToGigabytesAtOneThousandTwentyFour(double megabytes, string expected)
        {
            Assert.Equal(expected, SizeFormatter.FromMegabytes(megabytes));
        }

        [Fact]
        public void ToMegabytes_ConvertsFromBytes()
        {
            Assert.Equal(1, SizeFormatter.ToMegabytes(1024 * 1024));
        }

        [Fact]
        public void FromBytes_FormatsUsingTheSameThresholds()
        {
            Assert.Equal("2.00 GB", SizeFormatter.FromBytes(2L * 1024 * 1024 * 1024));
        }
    }
}
