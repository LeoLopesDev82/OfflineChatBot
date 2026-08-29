namespace OfflineChatBot.Services.Llm
{
    public sealed class PromptFormat
    {
        public static readonly PromptFormat ChatMl = new PromptFormat(
            systemPrompt:
                "You are a helpful, intelligent AI assistant. Respond naturally, articulate, and accurately. " +
                "Only format code snippets in markdown code blocks when answering coding questions or when code is explicitly requested.",
            systemTurn: "<|im_start|>system\n{0}\n<|im_end|>\n",
            userTurn: "<|im_start|>user\n{0}\n<|im_end|>\n",
            assistantTurn: "<|im_start|>assistant\n{0}\n<|im_end|>\n",
            assistantOpening: "<|im_start|>assistant\n",
            stopTokens: new[] { "<|im_end|>", "<|im_start|>", "<|endoftext|>" });

        public static readonly PromptFormat Vicuna = new PromptFormat(
            systemPrompt:
                "A chat between a curious human and an artificial intelligence assistant. " +
                "The assistant gives helpful, detailed, and polite answers to the human's questions.",
            systemTurn: "{0}\n\n",
            userTurn: "USER: {0}\n",
            assistantTurn: "ASSISTANT: {0}</s>\n",
            assistantOpening: "ASSISTANT:",
            stopTokens: new[] { "USER:", "</s>", "<|endoftext|>" });

        private readonly string _systemTurn;
        private readonly string _userTurn;
        private readonly string _assistantTurn;

        private PromptFormat(
            string systemPrompt,
            string systemTurn,
            string userTurn,
            string assistantTurn,
            string assistantOpening,
            IReadOnlyList<string> stopTokens)
        {
            SystemPrompt = systemPrompt;
            AssistantOpening = assistantOpening;
            StopTokens = stopTokens;

            _systemTurn = systemTurn;
            _userTurn = userTurn;
            _assistantTurn = assistantTurn;
        }

        public string SystemPrompt { get; }
        public string AssistantOpening { get; }
        public IReadOnlyList<string> StopTokens { get; }

        public string SystemTurn(string content) => string.Format(_systemTurn, content);
        public string UserTurn(string content) => string.Format(_userTurn, content);
        public string AssistantTurn(string content) => string.Format(_assistantTurn, content);

        public string RemoveStopTokens(string text)
        {
            foreach (var token in StopTokens)
                text = text.Replace(token, string.Empty);

            return text;
        }
    }
}
