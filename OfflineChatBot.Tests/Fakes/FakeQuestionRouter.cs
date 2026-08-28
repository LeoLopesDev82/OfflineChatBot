using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeQuestionRouter : IQuestionRouter
    {
        public bool Needed { get; set; } = true;
        public string? LastQuestion { get; private set; }
        public int AskCount { get; private set; }

        public bool NeedsDocument(string message)
        {
            LastQuestion = message;
            AskCount++;

            return Needed;
        }
    }
}
