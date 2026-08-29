using System.Text;

namespace OfflineChatBot.Services.Llm
{
    public sealed class StopTokenFilter
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly PromptFormat _format;
        private readonly int _longestStopToken;

        public StopTokenFilter(PromptFormat format)
        {
            _format = format;
            _longestStopToken = format.StopTokens.Max(token => token.Length);
        }

        public string Take(string chunk)
        {
            _buffer.Append(chunk);

            var text = _format.RemoveStopTokens(_buffer.ToString());
            var safeLength = SafeLength(text);

            _buffer.Clear();
            _buffer.Append(text.Substring(safeLength));

            return text.Substring(0, safeLength);
        }

        public string Flush()
        {
            var text = _format.RemoveStopTokens(_buffer.ToString());

            _buffer.Clear();

            return text.Substring(0, LengthWithoutDanglingToken(text));
        }

        #region Private Methods

        private int SafeLength(string text)
        {
            var maxHeldBack = Math.Min(_longestStopToken - 1, text.Length);

            for (var heldBack = maxHeldBack; heldBack > 0; heldBack--)
            {
                if (StartsAStopToken(text.Substring(text.Length - heldBack)))
                    return text.Length - heldBack;
            }

            return text.Length;
        }

        private int LengthWithoutDanglingToken(string text)
        {
            var safeLength = SafeLength(text);

            return text.Length - safeLength > 1 ? safeLength : text.Length;
        }

        private bool StartsAStopToken(string suffix)
        {
            return _format.StopTokens.Any(token => token.StartsWith(suffix) && token != suffix);
        }

        #endregion
    }
}
