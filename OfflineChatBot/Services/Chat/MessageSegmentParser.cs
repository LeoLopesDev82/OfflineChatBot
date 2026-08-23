using System.Text.RegularExpressions;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Chat
{
    public static class MessageSegmentParser
    {
        private static readonly Regex CodeBlockRegex = new Regex(@"```(?:language:)?([a-zA-Z0-9_#+\-]*)\r?\n([\s\S]*?)```", RegexOptions.Compiled);

        private const string DefaultLanguage = "code";

        public static List<ChatMessageSegment> Parse(string? content)
        {
            var segments = new List<ChatMessageSegment>();

            if (string.IsNullOrWhiteSpace(content))
                return segments;

            var lastIndex = 0;

            foreach (Match match in CodeBlockRegex.Matches(content))
            {
                AddTextSegment(segments, content.Substring(lastIndex, match.Index - lastIndex));
                AddCodeSegment(segments, match);

                lastIndex = match.Index + match.Length;
            }

            AddTextSegment(segments, content.Substring(lastIndex));

            return segments;
        }

        #region Private Methods

        private static void AddTextSegment(List<ChatMessageSegment> segments, string rawText)
        {
            var text = rawText.Trim();

            if (string.IsNullOrEmpty(text))
                return;

            segments.Add(new ChatMessageSegment
            {
                IsCode = false,
                Text = text
            });
        }

        private static void AddCodeSegment(List<ChatMessageSegment> segments, Match match)
        {
            segments.Add(new ChatMessageSegment
            {
                IsCode = true,
                Language = NormalizeLanguage(match.Groups[1].Value),
                Code = match.Groups[2].Value.TrimEnd()
            });
        }

        private static string NormalizeLanguage(string rawLanguage)
        {
            var language = rawLanguage.Replace("language:", string.Empty).Replace("lang-", string.Empty).Trim();

            return string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language;
        }

        #endregion
    }
}