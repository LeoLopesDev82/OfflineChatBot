using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeTokenCounter : ITokenCounter
    {
        private readonly int _charactersPerToken;

        public FakeTokenCounter(int charactersPerToken = 4)
        {
            _charactersPerToken = charactersPerToken;
        }

        public int Count(string text)
        {
            return text.Length / _charactersPerToken;
        }
    }
}