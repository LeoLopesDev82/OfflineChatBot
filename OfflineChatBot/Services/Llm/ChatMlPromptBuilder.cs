using System.Text;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Llm
{
    public static class ChatMlPromptBuilder
    {
        public const string SystemPrompt =
            "You are a helpful, intelligent AI assistant. Respond naturally, articulate, and accurately. " +
            "Only format code snippets in markdown code blocks when answering coding questions or when code is explicitly requested.";

        public static readonly IReadOnlyList<string> StopTokens = new[] { "<|im_end|>", "<|im_start|>", "<|endoftext|>" };

        private const int HistoryMessageLimit = 10;

        public static string Build(IEnumerable<ChatMessage> history, string userPrompt)
        {
            var builder = new StringBuilder();

            AppendTurn(builder, "system", SystemPrompt);

            foreach (var message in RelevantHistory(history))
                AppendTurn(builder, RoleOf(message), message.Content);

            AppendTurn(builder, "user", userPrompt);

            builder.Append("<|im_start|>assistant\n");

            return builder.ToString();
        }

        public static string RemoveStopTokens(string text)
        {
            foreach (var token in StopTokens)
                text = text.Replace(token, string.Empty);

            return text;
        }

        #region Private Methods

        private static IEnumerable<ChatMessage> RelevantHistory(IEnumerable<ChatMessage> history)
        {
            return history.Where(message => message.IsUser || message.IsAssistant).TakeLast(HistoryMessageLimit);
        }

        private static string RoleOf(ChatMessage message)
        {
            return message.IsUser ? "user" : "assistant";
        }

        private static void AppendTurn(StringBuilder builder, string role, string content)
        {
            builder.AppendLine($"<|im_start|>{role}");
            builder.AppendLine(content);
            builder.AppendLine("<|im_end|>");
        }

        #endregion
    }
}