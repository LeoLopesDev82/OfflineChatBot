using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Llm
{
    public sealed class PromptBuilder
    {
        private const string UnknownLanguageReminder =
            "\n\n(Write the whole answer in the same language as the question above, whatever language the instructions and the attached material are in.)";

        private readonly ITokenCounter _tokenCounter;
        private readonly GenerationOptions _options;
        private readonly PromptFormat _format;

        public PromptBuilder(ITokenCounter tokenCounter, GenerationOptions options, PromptFormat format)
        {
            _tokenCounter = tokenCounter;
            _options = options;
            _format = format;
        }

        public string BuildTurn(string userPrompt)
        {
            return _format.UserTurn(userPrompt + ReminderFor(userPrompt)) + _format.AssistantOpening;
        }

        public PromptResult Build(IEnumerable<ChatMessage> history, string userPrompt, string documentContext = "")
        {
            var opening = _format.SystemTurn(_format.SystemPrompt + DocumentBlock(documentContext));
            var closing = BuildTurn(userPrompt);
            var candidates = history.Where(message => message.IsUser || message.IsAssistant).ToList();

            var included = SelectWithinBudget(candidates, opening, closing);
            var text = opening + string.Concat(included) + closing;

            return new PromptResult(text, _tokenCounter.Count(text), included.Count, candidates.Count - included.Count);
        }

        #region Private Methods

        private static string ReminderFor(string userPrompt)
        {
            var language = QuestionLanguage.Of(userPrompt);

            if (language == null)
                return UnknownLanguageReminder;

            return $"\n\n(Write the whole answer in {language}, whatever language the instructions and the attached material are in.)";
        }

        private List<string> SelectWithinBudget(List<ChatMessage> candidates, string opening, string closing)
        {
            var available = AvailableForHistory(opening, closing);
            var selected = new List<string>();

            foreach (var message in Enumerable.Reverse(candidates))
            {
                var turn = TurnFor(message);
                var cost = _tokenCounter.Count(turn);

                if (cost > available)
                    break;

                available -= cost;

                selected.Insert(0, turn);
            }

            return selected;
        }

        private int AvailableForHistory(string opening, string closing)
        {
            var reserved = _options.MaxTokens + _tokenCounter.Count(opening) + _tokenCounter.Count(closing);
            var remaining = (int)_options.ContextSize - reserved;

            return Math.Min(remaining, _options.MaxHistoryTokens);
        }

        private static string DocumentBlock(string documentContext)
        {
            if (string.IsNullOrWhiteSpace(documentContext))
                return string.Empty;

            return $"\n\nThe user attached a document. This is what you have to answer from:\n---\n{documentContext}\n---\nAnswer from this material, and say so when the answer is not in it.";
        }

        private string TurnFor(ChatMessage message)
        {
            return message.IsUser ? _format.UserTurn(message.Content) : _format.AssistantTurn(message.Content);
        }

        #endregion
    }
}
