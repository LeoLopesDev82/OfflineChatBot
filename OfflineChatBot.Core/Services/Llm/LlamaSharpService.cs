using System.Runtime.CompilerServices;
using System.Text;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Llm
{
    public sealed class LlamaSharpService : ILlmService, ITokenCounter, IDisposable
    {
        private const string AlreadyGeneratingMessage = "The model is already answering. One model, one context, one answer at a time.";
        private const string NoModelMessage = "No model is loaded into memory.";

        private static readonly string[] AssistantPrefixes = { "Bot:", "Help:", "Assistant:" };

        private readonly GenerationOptions _options;
        private readonly ILogger<LlamaSharpService> _logger;
        private readonly ConversationTracker _tracker;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

        private LoadedModel? _model;
        private PromptBuilder _promptBuilder;
        private int _generating;

        public LlamaSharpService(IOptions<GenerationOptions> options, ILogger<LlamaSharpService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _promptBuilder = new PromptBuilder(this, _options, PromptFormat.ChatMl);
            _tracker = new ConversationTracker(_options);

            UseGpu = _options.UseGpu;
        }

        public bool IsLoaded => _model != null;
        public string LoadedModelPath => _model?.Path ?? string.Empty;

        public bool UseGpu { get; set; }
        public BackendStatus Backend { get; private set; } = BackendStatus.Cpu;
        public double LastTokensPerSecond { get; private set; }

        public int Count(string text)
        {
            return _model?.Tokenize(text) ?? EstimateTokens(text);
        }

        public async Task LoadModelAsync(string modelPath, string? visionProjectionPath = null, CancellationToken cancellationToken = default)
        {
            EnsureFileExists(modelPath, ".gguf model file not found.");
            EnsureVisionFileExists(visionProjectionPath);

            if (IsModelReady(modelPath))
                return;

            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                if (IsModelReady(modelPath))
                    return;

                Release();

                NativeBackend.BeginLoad();

                _model = await Task.Run(
                    () => LoadedModel.Load(modelPath, visionProjectionPath, RequestedGpuLayers, _options.ContextSize, _logger),
                    cancellationToken);

                _promptBuilder = new PromptBuilder(this, _options, _model.Format);
                Backend = NativeBackend.Current;

                _logger.LogInformation(
                    "Loaded model {ModelPath} on {Device} with {OffloadedLayers}/{TotalLayers} layers offloaded using {VideoMemory:F0} MiB{VisionSuffix}",
                    modelPath,
                    Backend.Device,
                    Backend.OffloadedLayers,
                    Backend.TotalLayers,
                    Backend.VideoMemoryInMB,
                    visionProjectionPath == null ? string.Empty : " with vision support");
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
                Release();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<string> CompleteAsync(string question, string content, CancellationToken cancellationToken = default)
        {
            EnterGeneration();

            try
            {
                return await CompleteInternalAsync(question, content, cancellationToken);
            }
            finally
            {
                LeaveGeneration();
            }
        }

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
            string conversationId,
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            string documentContext = "",
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            EnterGeneration();

            try
            {
                await foreach (var chunk in StreamAsync(conversationId, history, userPrompt, imagePath, documentContext, cancellationToken))
                    yield return chunk;
            }
            finally
            {
                LeaveGeneration();
            }
        }

        public void Dispose()
        {
            Release();

            _loadLock.Dispose();
        }

        #region Private Methods

        private async Task<string> CompleteInternalAsync(string question, string content, CancellationToken cancellationToken)
        {
            var model = await PrepareAsync(cancellationToken);

            model.RestartContext();
            _tracker.Invalidate();

            var prompt = _promptBuilder.Build([], question, content);
            var filter = new StopTokenFilter(model.Format);
            var answer = new StringBuilder();

            try
            {
                await foreach (var chunk in model.InferAsync(prompt.Text, CreateInferenceParams(model, _options.MaxNoteTokens), cancellationToken))
                    answer.Append(filter.Take(chunk));

                answer.Append(filter.Flush());
            }
            finally
            {
                _tracker.Invalidate();
            }

            _logger.LogInformation("Completed a standalone pass over {TokenCount} tokens of content", prompt.TokenCount);

            return answer.ToString().Trim();
        }

        private async IAsyncEnumerable<string> StreamAsync(
            string conversationId,
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath,
            string documentContext,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var model = await PrepareAsync(cancellationToken);
            var messages = history.Where(message => message.IsUser || message.IsAssistant).ToList();
            var input = PrepareInput(model, conversationId, messages, userPrompt, imagePath, documentContext);
            var filter = new StopTokenFilter(model.Format);
            var throughput = new ThroughputMeter();
            var isFirstChunk = true;
            var completed = false;

            try
            {
                await foreach (var chunk in model.InferAsync(input.Text, CreateInferenceParams(model, _options.MaxTokens), cancellationToken))
                {
                    throughput.Count();

                    var text = filter.Take(isFirstChunk ? TrimAssistantPrefix(chunk) : chunk);

                    isFirstChunk = false;
                    LastTokensPerSecond = throughput.TokensPerSecond;

                    if (string.IsNullOrEmpty(text))
                        continue;

                    yield return text;
                }

                var remainingText = filter.Flush();

                if (!string.IsNullOrEmpty(remainingText))
                    yield return remainingText;

                completed = true;
            }
            finally
            {
                LastTokensPerSecond = throughput.TokensPerSecond;

                _logger.LogInformation("Generated {TokenCount} tokens at {TokensPerSecond:F1} tokens per second", throughput.TokenCount, LastTokensPerSecond);

                Remember(model, completed, conversationId, messages.Count, documentContext);
            }
        }

        private void EnterGeneration()
        {
            if (Interlocked.CompareExchange(ref _generating, 1, 0) != 0)
                throw new InvalidOperationException(AlreadyGeneratingMessage);
        }

        private void LeaveGeneration()
        {
            Interlocked.Exchange(ref _generating, 0);
        }

        private bool IsModelReady(string modelPath)
        {
            return _model != null && _model.Path == modelPath;
        }

        private async Task<LoadedModel> PrepareAsync(CancellationToken cancellationToken)
        {
            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                return _model ?? throw new InvalidOperationException(NoModelMessage);
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private PromptResult PrepareInput(LoadedModel model, string conversationId, List<ChatMessage> history, string userPrompt, string? imagePath, string documentContext)
        {
            var turn = _promptBuilder.BuildTurn(userPrompt);
            var turnTokens = Count(turn);

            if (_tracker.CanContinue(conversationId, history.Count, documentContext, imagePath != null, turnTokens))
            {
                _logger.LogInformation("Continued the loaded context, sending {TokenCount} new tokens instead of the whole conversation", turnTokens);

                return new PromptResult(turn, turnTokens, history.Count, 0);
            }

            model.RestartContext();
            _tracker.Invalidate();

            var result = _promptBuilder.Build(history, model.AttachImage(userPrompt, imagePath), documentContext);

            _logger.LogInformation(
                "Rebuilt the context with {TokenCount} of {ContextSize} tokens, keeping {IncludedMessages} history messages and dropping {DroppedMessages}",
                result.TokenCount,
                _options.ContextSize,
                result.IncludedMessages,
                result.DroppedMessages);

            return result;
        }

        private void Remember(LoadedModel model, bool completed, string conversationId, int historyCount, string documentContext)
        {
            if (!completed)
            {
                _tracker.Invalidate();

                return;
            }

            _tracker.Advance(conversationId, historyCount, documentContext, model.ConsumedTokens());
        }

        private void Release()
        {
            _model?.Dispose();
            _model = null;

            _tracker.Invalidate();
        }

        private int RequestedGpuLayers => UseGpu ? _options.GpuLayerCount : 0;

        private InferenceParams CreateInferenceParams(LoadedModel model, int maxTokens)
        {
            return new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = model.Format.StopTokens.ToList(),
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = _options.Temperature,
                    RepeatPenalty = _options.RepeatPenalty,
                    TopK = _options.TopK,
                    TopP = _options.TopP
                }
            };
        }

        private static int EstimateTokens(string text)
        {
            return text.Length / 4;
        }

        private static string TrimAssistantPrefix(string text)
        {
            var prefix = AssistantPrefixes.FirstOrDefault(text.StartsWith);

            return prefix == null ? text : text.Substring(prefix.Length).TrimStart();
        }

        private static void EnsureVisionFileExists(string? visionProjectionPath)
        {
            if (string.IsNullOrEmpty(visionProjectionPath))
                return;

            EnsureFileExists(visionProjectionPath, "Vision projection file not found.");
        }

        private static void EnsureFileExists(string filePath, string errorMessage)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(errorMessage, filePath);
        }

        #endregion
    }
}
