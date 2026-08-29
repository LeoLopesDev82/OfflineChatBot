using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Llm
{
    public sealed class ConversationTracker
    {
        private readonly GenerationOptions _options;

        private string _conversationId = string.Empty;
        private string _documentContext = string.Empty;
        private int _consumedMessages = -1;
        private int _consumedTokens;

        public ConversationTracker(GenerationOptions options)
        {
            _options = options;
        }

        public bool CanContinue(string conversationId, int historyCount, string documentContext, bool hasImage, int incomingTokens)
        {
            if (hasImage)
                return false;

            if (conversationId != _conversationId || documentContext != _documentContext)
                return false;

            if (historyCount != _consumedMessages)
                return false;

            return _consumedTokens + incomingTokens + _options.MaxTokens <= _options.ContextSize;
        }

        public void Advance(string conversationId, int historyCount, string documentContext, int consumedTokens)
        {
            _conversationId = conversationId;
            _documentContext = documentContext;
            _consumedMessages = historyCount + 2;
            _consumedTokens = consumedTokens;
        }

        public void Invalidate()
        {
            _conversationId = string.Empty;
            _documentContext = string.Empty;
            _consumedMessages = -1;
            _consumedTokens = 0;
        }
    }
}
