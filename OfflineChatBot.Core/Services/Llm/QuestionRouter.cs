using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Llm
{
    public sealed class QuestionRouter : IQuestionRouter
    {
        private const int LongestSmallTalk = 6;

        private static readonly HashSet<string> SmallTalk = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "oi", "ola", "olá", "eai", "eaí", "hello", "hi", "hey", "yo",
            "bom", "boa", "dia", "tarde", "noite", "manha", "manhã",
            "obrigado", "obrigada", "obg", "valeu", "vlw", "thanks", "thank", "you", "thx",
            "de", "nada", "denada", "welcome",
            "tchau", "adeus", "ate", "até", "mais", "logo", "bye", "goodbye",
            "ok", "okay", "beleza", "blz", "certo", "entendi", "entendido", "perfeito",
            "otimo", "ótimo", "legal", "show", "massa", "bacana", "top", "great", "nice", "cool",
            "tudo", "bem", "como", "vai", "vc", "voce", "você", "esta", "está", "tá", "ta",
            "e", "é", "ai", "aí", "cara", "sim", "nao", "não", "yes", "no", "please", "por", "favor",
            "how", "are", "is", "was", "it", "going", "doing", "there", "good", "morning", "afternoon",
            "evening", "night", "alright", "fine", "well", "hola", "salut", "here"
        };

        public bool NeedsDocument(string message)
        {
            var words = Words(message);

            if (words.Count == 0 || words.Count > LongestSmallTalk)
                return true;

            return !words.All(SmallTalk.Contains);
        }

        #region Private Methods

        private static List<string> Words(string message)
        {
            return message
                .Split([' ', '\n', '\r', '\t', '.', ',', ';', ':', '?', '!', '"', '\'', '(', ')', '-'], StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        #endregion
    }
}
