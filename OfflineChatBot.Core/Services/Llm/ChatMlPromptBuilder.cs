using System.Text;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Llm
{
    public sealed class ChatMlPromptBuilder
    {
        public const string SystemPrompt =
            "You are a helpful, intelligent AI assistant. Respond naturally, articulate, and accurately. " +
            "Only format code snippets in markdown code blocks when answering coding questions or when code is explicitly requested.";

        public static readonly IReadOnlyList<string> StopTokens = new[] { "<|im_end|>", "<|im_start|>", "<|endoftext|>" };

        private const string AssistantOpening = "<|im_start|>assistant\n";

        private readonly ITokenCounter _tokenCounter;
        private readonly GenerationOptions _options;

        public ChatMlPromptBuilder(ITokenCounter tokenCounter, GenerationOptions options)
        {
            _tokenCounter = tokenCounter;
            _options = options;
        }

        public static string RemoveStopTokens(string text)
        {
            foreach (var token in StopTokens)
                text = text.Replace(token, string.Empty);

            return text;
        }

        public PromptResult Build(IEnumerable<ChatMessage> history, string userPrompt)
        {
            var opening = FormatTurn("system", SystemPrompt);
            var closing = FormatTurn("user", userPrompt) + AssistantOpening;
            var candidates = history.Where(message => message.IsUser || message.IsAssistant).ToList();

            var included = SelectWithinBudget(candidates, opening, closing);
            var text = opening + string.Concat(included) + closing;

            return new PromptResult(text, _tokenCounter.Count(text), included.Count, candidates.Count - included.Count);
        }

        #region Private Methods

        private List<string> SelectWithinBudget(List<ChatMessage> candidates, string opening, string closing)
        {
            var available = AvailableForHistory(opening, closing);
            var selected = new List<string>();

            foreach (var message in Enumerable.Reverse(candidates))
            {
                var turn = FormatTurn(RoleOf(message), message.Content);
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

        private static string RoleOf(ChatMessage message)
        {
            return message.IsUser ? "user" : "assistant";
        }

        private static string FormatTurn(string role, string content)
        {
            var builder = new StringBuilder();

            builder.AppendLine($"<|im_start|>{role}");
            builder.AppendLine(content);
            builder.AppendLine("<|im_end|>");

            return builder.ToString();
        }

        #endregion
    }
}