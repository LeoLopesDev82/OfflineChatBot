using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class TextSplitter
    {
        private const string Separator = "\n\n";

        private static readonly string[] ParagraphBreaks = { "\r\n\r\n", "\n\n", "\r\r" };
        private static readonly char[] SentenceEndings = { '.', '!', '?' };

        private readonly ITokenCounter _tokenCounter;

        public TextSplitter(ITokenCounter tokenCounter)
        {
            _tokenCounter = tokenCounter;
        }

        public IReadOnlyList<string> Split(string text, int partTokens)
        {
            var parts = new List<string>();
            var current = new List<string>();

            foreach (var piece in Pieces(text, partTokens))
            {
                if (Exceeds(current, piece, partTokens))
                {
                    parts.Add(TextOf(current));
                    current.Clear();
                }

                current.Add(piece);
            }

            if (current.Count > 0)
                parts.Add(TextOf(current));

            return parts;
        }

        #region Private Methods

        private IEnumerable<string> Pieces(string text, int partTokens)
        {
            foreach (var paragraph in Paragraphs(text))
            {
                if (_tokenCounter.Count(paragraph) <= partTokens)
                {
                    yield return paragraph;

                    continue;
                }

                foreach (var sentence in Break(paragraph, partTokens))
                    yield return sentence;
            }
        }

        private IEnumerable<string> Break(string paragraph, int partTokens)
        {
            foreach (var sentence in Sentences(paragraph))
            {
                if (_tokenCounter.Count(sentence) <= partTokens)
                {
                    yield return sentence;

                    continue;
                }

                foreach (var run in Words(sentence, partTokens))
                    yield return run;
            }
        }

        private IEnumerable<string> Words(string sentence, int partTokens)
        {
            var current = new List<string>();

            foreach (var word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Count > 0 && _tokenCounter.Count(string.Join(' ', current.Append(word))) > partTokens)
                {
                    yield return string.Join(' ', current);

                    current.Clear();
                }

                current.Add(word);
            }

            if (current.Count > 0)
                yield return string.Join(' ', current);
        }

        private static IEnumerable<string> Paragraphs(string text)
        {
            return text
                .Split(ParagraphBreaks, StringSplitOptions.RemoveEmptyEntries)
                .Select(paragraph => paragraph.Trim())
                .Where(paragraph => paragraph.Length > 0);
        }

        private static IEnumerable<string> Sentences(string paragraph)
        {
            var start = 0;

            for (var position = 0; position < paragraph.Length; position++)
            {
                if (Array.IndexOf(SentenceEndings, paragraph[position]) < 0)
                    continue;

                yield return paragraph.Substring(start, position - start + 1).Trim();

                start = position + 1;
            }

            if (start < paragraph.Length)
                yield return paragraph.Substring(start).Trim();
        }

        private bool Exceeds(List<string> current, string piece, int partTokens)
        {
            return current.Count > 0 && _tokenCounter.Count(TextOf(current.Append(piece))) > partTokens;
        }

        private static string TextOf(IEnumerable<string> pieces)
        {
            return string.Join(Separator, pieces);
        }

        #endregion
    }
}
