using System.Runtime.CompilerServices;
using System.Text;
using LLama;
using LLama.Common;
using LLama.Native;
using LLama.Sampling;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Llm
{
    public sealed class LlamaSharpService : ILlmService, ITokenCounter, IDisposable
    {
        private static readonly string[] AssistantPrefixes = { "Bot:", "Help:", "Assistant:" };

        private readonly GenerationOptions _options;
        private readonly ILogger<LlamaSharpService> _logger;
        private PromptBuilder _promptBuilder;
        private PromptFormat _format = PromptFormat.ChatMl;
        private readonly ConversationTracker _tracker;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

        private LLamaWeights? _weights;
        private MtmdWeights? _visionWeights;
        private ModelParams? _parameters;
        private InteractiveExecutor? _executor;
        private LLamaContext? _context;
        private string _mediaMarker = string.Empty;

        public LlamaSharpService(IOptions<GenerationOptions> options, ILogger<LlamaSharpService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _promptBuilder = new PromptBuilder(this, _options, _format);
            _tracker = new ConversationTracker(_options);

            UseGpu = _options.UseGpu;
        }

        public int Count(string text)
        {
            var weights = _weights;

            if (weights == null)
                return EstimateTokens(text);

            return weights.Tokenize(text, add_bos: false, special: true, encoding: Encoding.UTF8).Length;
        }

        public bool IsLoaded => _weights != null && _executor != null;
        public string LoadedModelPath { get; private set; } = string.Empty;

        public bool UseGpu { get; set; }
        public BackendStatus Backend { get; private set; } = BackendStatus.Cpu;
        public double LastTokensPerSecond { get; private set; }

        public async Task LoadModelAsync(string modelPath, string? visionProjectionPath = null, CancellationToken cancellationToken = default)
        {
            EnsureFileExists(modelPath, ".gguf model file not found.");

            if (IsModelReady(modelPath))
                return;

            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                if (IsModelReady(modelPath))
                    return;

                UnloadInternal();

                NativeBackend.BeginLoad();

                await Task.Run(() => LoadWeights(modelPath, visionProjectionPath), cancellationToken);

                LoadedModelPath = modelPath;
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
                UnloadInternal();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        public async Task<string> CompleteAsync(string question, string content, CancellationToken cancellationToken = default)
        {
            await PrepareAsync([], cancellationToken);

            RestartContext();

            var prompt = _promptBuilder.Build([], question, content);
            var filter = new StopTokenFilter(_format);
            var answer = new StringBuilder();

            try
            {
                await foreach (var chunk in _executor!.InferAsync(prompt.Text, CreateInferenceParams(_options.MaxNoteTokens), cancellationToken))
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

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
            string conversationId,
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            string documentContext = "",
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var messages = await PrepareAsync(history, cancellationToken);
            var input = PrepareInput(conversationId, messages, userPrompt, imagePath, documentContext);
            var filter = new StopTokenFilter(_format);
            var throughput = new ThroughputMeter();
            var isFirstChunk = true;
            var completed = false;

            try
            {
                await foreach (var chunk in _executor!.InferAsync(input.Text, CreateInferenceParams(_options.MaxTokens), cancellationToken))
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

                Remember(completed, conversationId, messages.Count, documentContext);
            }
        }

        public void Dispose()
        {
            UnloadInternal();

            _loadLock.Dispose();
        }

        #region Private Methods

        private bool IsModelReady(string modelPath)
        {
            return IsLoaded && LoadedModelPath == modelPath;
        }

        private async Task<List<ChatMessage>> PrepareAsync(IEnumerable<ChatMessage> history, CancellationToken cancellationToken)
        {
            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                if (_executor == null)
                    throw new InvalidOperationException("No model is loaded into memory.");

                return history.Where(message => message.IsUser || message.IsAssistant).ToList();
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private PromptResult PrepareInput(string conversationId, List<ChatMessage> history, string userPrompt, string? imagePath, string documentContext)
        {
            var turn = _promptBuilder.BuildTurn(userPrompt);
            var turnTokens = Count(turn);

            if (_tracker.CanContinue(conversationId, history.Count, documentContext, imagePath != null, turnTokens))
            {
                _logger.LogInformation("Continued the loaded context, sending {TokenCount} new tokens instead of the whole conversation", turnTokens);

                return new PromptResult(turn, turnTokens, history.Count, 0);
            }

            RestartContext();

            var result = _promptBuilder.Build(history, AttachImage(userPrompt, imagePath), documentContext);

            _logger.LogInformation(
                "Rebuilt the context with {TokenCount} of {ContextSize} tokens, keeping {IncludedMessages} history messages and dropping {DroppedMessages}",
                result.TokenCount,
                _options.ContextSize,
                result.IncludedMessages,
                result.DroppedMessages);

            return result;
        }

        private void Remember(bool completed, string conversationId, int historyCount, string documentContext)
        {
            if (!completed || _context == null)
            {
                _tracker.Invalidate();

                return;
            }

            _tracker.Advance(conversationId, historyCount, documentContext, ConsumedTokens());
        }

        private int ConsumedTokens()
        {
            return _context!.NativeHandle.MemorySequenceMaxPosition(LLamaSeqId.Zero).Value + 1;
        }

        private static int EstimateTokens(string text)
        {
            return text.Length / 4;
        }

        private void LoadWeights(string modelPath, string? visionProjectionPath)
        {
            try
            {
                LoadWeights(modelPath, visionProjectionPath, RequestedGpuLayers);
            }
            catch (Exception exception) when (RequestedGpuLayers > 0)
            {
                _logger.LogWarning(exception, "Loading {ModelPath} on the GPU failed, falling back to the CPU", modelPath);

                UnloadInternal();
                LoadWeights(modelPath, visionProjectionPath, 0);
            }
        }

        private void LoadWeights(string modelPath, string? visionProjectionPath, int gpuLayers)
        {
            _parameters = new ModelParams(modelPath)
            {
                ContextSize = _options.ContextSize,
                GpuLayerCount = gpuLayers
            };

            _weights = LLamaWeights.LoadFromFile(_parameters);

            LoadVisionWeights(visionProjectionPath);
            UsePromptFormat(_visionWeights == null ? PromptFormat.ChatMl : PromptFormat.Vicuna);
            RestartContext();
        }

        private void UsePromptFormat(PromptFormat format)
        {
            _format = format;
            _promptBuilder = new PromptBuilder(this, _options, format);
        }

        private int RequestedGpuLayers => UseGpu ? _options.GpuLayerCount : 0;

        private void LoadVisionWeights(string? visionProjectionPath)
        {
            if (string.IsNullOrEmpty(visionProjectionPath))
                return;

            EnsureFileExists(visionProjectionPath, "Vision projection file not found.");

            var visionParameters = MtmdContextParams.Default();

            visionParameters.UseGpu = false;

            _visionWeights = MtmdWeights.LoadFromFile(visionProjectionPath, _weights!, visionParameters);
            _mediaMarker = visionParameters.MediaMarker ?? NativeApi.MtmdDefaultMarker() ?? string.Empty;
        }

        private void RestartContext()
        {
            DisposeEmbeds();

            _visionWeights?.ClearMedia();

            _context?.Dispose();
            _context = _weights!.CreateContext(_parameters!);

            _executor = _visionWeights == null
                ? new InteractiveExecutor(_context)
                : new InteractiveExecutor(_context, _visionWeights);

            _tracker.Invalidate();
        }

        private void DisposeEmbeds()
        {
            if (_executor == null)
                return;

            foreach (var embed in _executor.Embeds)
                embed.Dispose();

            _executor.Embeds.Clear();
        }

        private string AttachImage(string userPrompt, string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return userPrompt;

            if (_visionWeights == null || string.IsNullOrEmpty(_mediaMarker))
                throw new InvalidOperationException("The selected vision model is not ready to process images.");

            _executor!.Embeds.Add(_visionWeights.LoadMedia(imagePath));

            return $"{_mediaMarker}\n{userPrompt}";
        }

        private InferenceParams CreateInferenceParams(int maxTokens)
        {
            return new InferenceParams
            {
                MaxTokens = maxTokens,
                AntiPrompts = _format.StopTokens.ToList(),
                SamplingPipeline = new DefaultSamplingPipeline
                {
                    Temperature = _options.Temperature,
                    RepeatPenalty = _options.RepeatPenalty,
                    TopK = _options.TopK,
                    TopP = _options.TopP
                }
            };
        }

        private static string TrimAssistantPrefix(string text)
        {
            var prefix = AssistantPrefixes.FirstOrDefault(text.StartsWith);

            return prefix == null ? text : text.Substring(prefix.Length).TrimStart();
        }

        private static void EnsureFileExists(string filePath, string errorMessage)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException(errorMessage, filePath);
        }

        private void UnloadInternal()
        {
            DisposeEmbeds();

            _executor = null;
            _parameters = null;

            _context?.Dispose();
            _context = null;

            _visionWeights?.Dispose();
            _visionWeights = null;

            _weights?.Dispose();
            _weights = null;

            _tracker.Invalidate();

            LoadedModelPath = string.Empty;
            _mediaMarker = string.Empty;
        }

        #endregion
    }
}
