using System.Text.RegularExpressions;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class TextChunker
    {
        private static readonly Regex ParagraphRegex = new Regex(@"\r?\n\s*\r?\n", RegexOptions.Compiled);
        private static readonly Regex SentenceRegex = new Regex(@"(?<=[.!?])\s+", RegexOptions.Compiled);
        private static readonly Regex WhitespaceRegex = new Regex(@"[ \t]+", RegexOptions.Compiled);

        private const string Separator = "\n\n";

        private readonly ITokenCounter _tokenCounter;

        public TextChunker(ITokenCounter tokenCounter)
        {
            _tokenCounter = tokenCounter;
        }

        public IReadOnlyList<DocumentChunk> Split(string text, int chunkTokens, int overlapTokens)
        {
            var pieces = ToPieces(text, chunkTokens);
            var chunks = new List<DocumentChunk>();
            var current = new List<Piece>();

            foreach (var piece in pieces)
            {
                if (Exceeds(current, piece, chunkTokens))
                    current = StartNextChunk(chunks, current, overlapTokens);

                current.Add(piece);
            }

            AddChunk(chunks, current);

            return chunks;
        }

        #region Private Methods

        private List<Piece> ToPieces(string text, int chunkTokens)
        {
            return ParagraphRegex.Split(text)
                .Select(Normalize)
                .Where(paragraph => paragraph.Length > 0)
                .SelectMany(paragraph => Fit(paragraph, chunkTokens))
                .ToList();
        }

        private IEnumerable<Piece> Fit(string paragraph, int chunkTokens)
        {
            var tokens = _tokenCounter.Count(paragraph);

            if (tokens <= chunkTokens)
                return [new Piece(paragraph, tokens)];

            return SplitLongParagraph(paragraph, chunkTokens);
        }

        private IEnumerable<Piece> SplitLongParagraph(string paragraph, int chunkTokens)
        {
            foreach (var sentence in SentenceRegex.Split(paragraph).Where(sentence => sentence.Length > 0))
            {
                var tokens = _tokenCounter.Count(sentence);

                if (tokens <= chunkTokens)
                {
                    yield return new Piece(sentence, tokens);

                    continue;
                }

                foreach (var slice in SplitByWords(sentence, chunkTokens))
                    yield return slice;
            }
        }

        private IEnumerable<Piece> SplitByWords(string sentence, int chunkTokens)
        {
            var taken = new List<string>();

            foreach (var word in sentence.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (taken.Count > 0 && _tokenCounter.Count(string.Join(' ', taken.Append(word))) > chunkTokens)
                {
                    yield return CreatePiece(taken);

                    taken = [];
                }

                taken.Add(word);
            }

            if (taken.Count > 0)
                yield return CreatePiece(taken);
        }

        private Piece CreatePiece(List<string> words)
        {
            var text = string.Join(' ', words);

            return new Piece(text, _tokenCounter.Count(text));
        }

        private bool Exceeds(List<Piece> current, Piece piece, int chunkTokens)
        {
            return current.Count > 0 && _tokenCounter.Count(TextOf(current.Append(piece))) > chunkTokens;
        }

        private static List<Piece> StartNextChunk(List<DocumentChunk> chunks, List<Piece> current, int overlapTokens)
        {
            AddChunk(chunks, current);

            return TakeOverlap(current, overlapTokens);
        }

        private static List<Piece> TakeOverlap(List<Piece> current, int overlapTokens)
        {
            var overlap = new List<Piece>();
            var tokens = 0;

            foreach (var piece in Enumerable.Reverse(current))
            {
                if (tokens + piece.Tokens > overlapTokens)
                    break;

                tokens += piece.Tokens;

                overlap.Insert(0, piece);
            }

            return overlap;
        }

        private static void AddChunk(List<DocumentChunk> chunks, List<Piece> pieces)
        {
            if (pieces.Count == 0)
                return;

            chunks.Add(new DocumentChunk(chunks.Count, TextOf(pieces)));
        }

        private static string TextOf(IEnumerable<Piece> pieces)
        {
            return string.Join(Separator, pieces.Select(piece => piece.Text));
        }

        private static string Normalize(string paragraph)
        {
            return WhitespaceRegex.Replace(paragraph.Replace('\r', ' ').Replace('\n', ' '), " ").Trim();
        }

        private sealed record Piece(string Text, int Tokens);

        #endregion
    }
}
