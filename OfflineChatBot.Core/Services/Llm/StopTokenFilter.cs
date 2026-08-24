using System.Text;

namespace OfflineChatBot.Services.Llm
{
    public sealed class StopTokenFilter
    {
        private static readonly int LongestStopToken = ChatMlPromptBuilder.StopTokens.Max(token => token.Length);

        private readonly StringBuilder _buffer = new StringBuilder();

        public string Take(string chunk)
        {
            _buffer.Append(chunk);

            var text = ChatMlPromptBuilder.RemoveStopTokens(_buffer.ToString());
            var safeLength = SafeLength(text);

            _buffer.Clear();
            _buffer.Append(text.Substring(safeLength));

            return text.Substring(0, safeLength);
        }

        public string Flush()
        {
            var text = ChatMlPromptBuilder.RemoveStopTokens(_buffer.ToString());

            _buffer.Clear();

            return text.Substring(0, LengthWithoutDanglingToken(text));
        }

        #region Private Methods

        private static int SafeLength(string text)
        {
            var maxHeldBack = Math.Min(LongestStopToken - 1, text.Length);

            for (var heldBack = maxHeldBack; heldBack > 0; heldBack--)
            {
                if (StartsAStopToken(text.Substring(text.Length - heldBack)))
                    return text.Length - heldBack;
            }

            return text.Length;
        }

        private static int LengthWithoutDanglingToken(string text)
        {
            var safeLength = SafeLength(text);

            return text.Length - safeLength > 1 ? safeLength : text.Length;
        }

        private static bool StartsAStopToken(string suffix)
        {
            return ChatMlPromptBuilder.StopTokens.Any(token => token.StartsWith(suffix) && token != suffix);
        }

        #endregion
    }
}