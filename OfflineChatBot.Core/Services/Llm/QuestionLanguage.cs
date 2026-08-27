namespace OfflineChatBot.Services.Llm
{
    public static class QuestionLanguage
    {
        private const int LeastMatchesToDecide = 1;

        private static readonly Dictionary<string, string[]> Markers = new Dictionary<string, string[]>
        {
            ["Portuguese"] = ["que", "não", "nao", "para", "com", "uma", "você", "voce", "isso", "como", "qual", "quais", "está", "esta", "tem", "existe", "meu", "minha", "por", "dos", "das", "da", "do", "na", "mais", "sobre", "onde", "quanto", "quantos", "quantas", "essa", "esse", "então", "entao", "pode", "fazer"],
            ["Spanish"] = ["qué", "que", "no", "para", "con", "una", "usted", "esto", "cómo", "cual", "cuáles", "está", "tiene", "mi", "por", "los", "las", "más", "sobre", "puedes"],
            ["English"] = ["the", "what", "how", "which", "you", "your", "this", "that", "with", "for", "have", "does", "can", "please", "about", "there", "and", "from"],
            ["French"] = ["le", "la", "les", "que", "quoi", "comment", "vous", "votre", "avec", "pour", "est", "dans", "une", "des", "sur", "peux"],
            ["Italian"] = ["che", "cosa", "come", "quale", "tu", "tuo", "con", "per", "una", "delle", "sono", "questo", "puoi", "sopra"],
            ["German"] = ["der", "die", "das", "was", "wie", "welche", "sie", "ihr", "mit", "für", "ist", "eine", "und", "kannst", "über"]
        };

        public static string? Of(string text)
        {
            var words = Words(text);

            if (words.Count == 0)
                return null;

            var scores = Markers
                .Select(marker => (Language: marker.Key, Score: marker.Value.Count(words.Contains)))
                .OrderByDescending(entry => entry.Score)
                .ToList();

            if (scores[0].Score < LeastMatchesToDecide || scores[0].Score == scores[1].Score)
                return null;

            return scores[0].Language;
        }

        #region Private Methods

        private static HashSet<string> Words(string text)
        {
            return text
                .ToLowerInvariant()
                .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '?', '!', '"', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();
        }

        #endregion
    }
}
