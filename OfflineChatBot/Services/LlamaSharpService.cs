using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Sampling;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services
{
    public class LlamaSharpService : ILLMService, IDisposable
    {
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);
        private LLamaWeights? _weights;
        private ModelParams? _parameters;
        private StatelessExecutor? _executor;

        public bool IsLoaded => _weights != null && _executor != null;
        public string LoadedModelPath { get; private set; } = string.Empty;

        public async Task LoadModelAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException(".gguf model file not found.", modelPath);

            if (LoadedModelPath == modelPath && IsLoaded)
                return;

            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                if (LoadedModelPath == modelPath && IsLoaded)
                    return;

                UnloadModelInternal();

                var localPath = modelPath;

                await Task.Run(() =>
                {
                    _parameters = new ModelParams(localPath)
                    {
                        ContextSize = 8192,
                        GpuLayerCount = 0
                    };

                    _weights = LLamaWeights.LoadFromFile(_parameters);
                    _executor = new StatelessExecutor(_weights, _parameters);
                }, cancellationToken);

                LoadedModelPath = modelPath;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task UnloadModelAsync()
        {
            await _loadLock.WaitAsync();

            try
            {
                UnloadModelInternal();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(IEnumerable<ChatMessage> history, string userPrompt, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var localExecutor = await GetValidExecutorAsync(cancellationToken);
            var prompt = BuildChatMLPrompt(history, userPrompt);
            var inferenceParams = CreateInferenceParams();

            bool isFirstChunk = true;

            await foreach (var text in localExecutor.InferAsync(prompt, inferenceParams, cancellationToken))
            {
                var cleanedText = CleanFirstChunk(text, ref isFirstChunk);
            
                cleanedText = RemoveSpecialTokens(cleanedText);

                if (!string.IsNullOrEmpty(cleanedText))
                {
                    yield return cleanedText;
                }
            }
        }

        public void Dispose()
        {
            UnloadModelInternal();
            
            _loadLock.Dispose();
        }

        #region Private Methods

        private async Task<StatelessExecutor> GetValidExecutorAsync(CancellationToken cancellationToken)
        {
            await _loadLock.WaitAsync(cancellationToken);
            
            try
            {
                if (_executor == null)
                    throw new InvalidOperationException("No model is loaded into memory.");

                return _executor;
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private string BuildChatMLPrompt(IEnumerable<ChatMessage> history, string userPrompt)
        {
            var promptBuilder = new StringBuilder();
            
            AppendSystemPrompt(promptBuilder);
            AppendHistory(promptBuilder, history);
            AppendUserPrompt(promptBuilder, userPrompt);

            return promptBuilder.ToString();
        }

        private void AppendSystemPrompt(StringBuilder promptBuilder)
        {
            promptBuilder.AppendLine("<|im_start|>system");
            promptBuilder.AppendLine("You are a helpful, intelligent AI assistant. Respond naturally, articulate, and accurately. Only format code snippets in markdown code blocks when answering coding questions or when code is explicitly requested.");
            promptBuilder.AppendLine("<|im_end|>");
        }

        private void AppendHistory(StringBuilder promptBuilder, IEnumerable<ChatMessage> history)
        {
            var recentHistory = history.TakeLast(10).ToList();
            
            foreach (var msg in recentHistory)
            {
                if (msg.IsUser)
                {
                    promptBuilder.AppendLine("<|im_start|>user");
                    promptBuilder.AppendLine(msg.Content);
                    promptBuilder.AppendLine("<|im_end|>");
                }
                else if (msg.IsAssistant)
                {
                    promptBuilder.AppendLine("<|im_start|>assistant");
                    promptBuilder.AppendLine(msg.Content);
                    promptBuilder.AppendLine("<|im_end|>");
                }
            }
        }

        private void AppendUserPrompt(StringBuilder promptBuilder, string userPrompt)
        {
            promptBuilder.AppendLine("<|im_start|>user");
            promptBuilder.AppendLine(userPrompt);
            promptBuilder.AppendLine("<|im_end|>");
            promptBuilder.Append("<|im_start|>assistant\n");
        }

        private InferenceParams CreateInferenceParams()
        {
            return new InferenceParams
            {
                MaxTokens = 2048,
                AntiPrompts = new List<string>
                {
                    "<|im_end|>",
                    "<|im_start|>",
                    "<|endoftext|>"
                },
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = 0.7f,
                    RepeatPenalty = 1.18f,
                    TopK = 40,
                    TopP = 0.95f
                }
            };
        }

        private string CleanFirstChunk(string text, ref bool isFirstChunk)
        {
            if (!isFirstChunk) return text;
            
            isFirstChunk = false;
            
            if (text.StartsWith("Bot:"))
                return text.Substring(4).TrimStart();
            if (text.StartsWith("Help:"))
                return text.Substring(5).TrimStart();
            if (text.StartsWith("Assistant:"))
                return text.Substring(10).TrimStart();

            return text;
        }

        private string RemoveSpecialTokens(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            return text
                .Replace("<|im_end|>", "")
                .Replace("<|im_start|>", "")
                .Replace("<|endoftext|>", "");
        }

        private void UnloadModelInternal()
        {
            _executor = null;
            _parameters = null;

            if (_weights != null)
            {
                _weights.Dispose();
            
                _weights = null;
            }

            LoadedModelPath = string.Empty;
        }

        #endregion
    }
}