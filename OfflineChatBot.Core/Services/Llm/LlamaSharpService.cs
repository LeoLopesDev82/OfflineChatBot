using System.Runtime.CompilerServices;
using System.Text;
using LLama;
using LLama.Abstractions;
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
        private readonly ChatMlPromptBuilder _promptBuilder;
        private readonly SemaphoreSlim _loadLock = new SemaphoreSlim(1, 1);

        private LLamaWeights? _weights;
        private MtmdWeights? _visionWeights;
        private ModelParams? _parameters;
        private ILLamaExecutor? _executor;
        private LLamaContext? _context;
        private string _mediaMarker = string.Empty;

        public LlamaSharpService(IOptions<GenerationOptions> options, ILogger<LlamaSharpService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _promptBuilder = new ChatMlPromptBuilder(this, _options);

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

        public async IAsyncEnumerable<string> GenerateResponseStreamAsync(
            IEnumerable<ChatMessage> history,
            string userPrompt,
            string? imagePath = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var executor = await GetLoadedExecutorAsync(cancellationToken);

            ResetVisionState(executor);

            var prompt = BuildPrompt(history, AttachImage(executor, userPrompt, imagePath));
            var filter = new StopTokenFilter();
            var throughput = new ThroughputMeter();
            var isFirstChunk = true;

            await foreach (var chunk in executor.InferAsync(prompt, CreateInferenceParams(), cancellationToken))
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

            LastTokensPerSecond = throughput.TokensPerSecond;

            _logger.LogInformation("Generated {TokenCount} tokens at {TokensPerSecond:F1} tokens per second", throughput.TokenCount, LastTokensPerSecond);

            if (!string.IsNullOrEmpty(remainingText))
                yield return remainingText;
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

        private string BuildPrompt(IEnumerable<ChatMessage> history, string userPrompt)
        {
            var result = _promptBuilder.Build(history, userPrompt);

            _logger.LogInformation(
                "Prompt uses {TokenCount} of {ContextSize} tokens, keeping {IncludedMessages} history messages and dropping {DroppedMessages}",
                result.TokenCount,
                _options.ContextSize,
                result.IncludedMessages,
                result.DroppedMessages);

            return result.Text;
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

            _executor = string.IsNullOrEmpty(visionProjectionPath)
                ? new StatelessExecutor(_weights, _parameters)
                : CreateVisionExecutor(visionProjectionPath);
        }

        private int RequestedGpuLayers => UseGpu ? _options.GpuLayerCount : 0;

        private ILLamaExecutor CreateVisionExecutor(string visionProjectionPath)
        {
            EnsureFileExists(visionProjectionPath, "Vision projection file not found.");

            var visionParameters = MtmdContextParams.Default();

            visionParameters.UseGpu = false;

            _visionWeights = MtmdWeights.LoadFromFile(visionProjectionPath, _weights!, visionParameters);
            _mediaMarker = visionParameters.MediaMarker ?? NativeApi.MtmdDefaultMarker() ?? string.Empty;
            _context = _weights!.CreateContext(_parameters!);

            return new InteractiveExecutor(_context, _visionWeights);
        }

        private async Task<ILLamaExecutor> GetLoadedExecutorAsync(CancellationToken cancellationToken)
        {
            await _loadLock.WaitAsync(cancellationToken);

            try
            {
                return _executor ?? throw new InvalidOperationException("No model is loaded into memory.");
            }
            finally
            {
                _loadLock.Release();
            }
        }

        private void ResetVisionState(ILLamaExecutor executor)
        {
            if (executor is not InteractiveExecutor interactiveExecutor)
                return;

            _context?.NativeHandle.MemoryClear();

            foreach (var embed in interactiveExecutor.Embeds)
                embed.Dispose();

            interactiveExecutor.Embeds.Clear();

            _visionWeights?.ClearMedia();
        }

        private string AttachImage(ILLamaExecutor executor, string userPrompt, string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath) || _visionWeights == null)
                return userPrompt;

            if (executor is not InteractiveExecutor visionExecutor || string.IsNullOrEmpty(_mediaMarker))
                throw new InvalidOperationException("The selected vision model is not ready to process images.");

            visionExecutor.Embeds.Add(_visionWeights.LoadMedia(imagePath));

            return $"{_mediaMarker}\n{userPrompt}";
        }

        private InferenceParams CreateInferenceParams()
        {
            return new InferenceParams
            {
                MaxTokens = _options.MaxTokens,
                AntiPrompts = ChatMlPromptBuilder.StopTokens.ToList(),
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
            _executor = null;
            _parameters = null;

            _context?.Dispose();
            _context = null;

            _visionWeights?.Dispose();
            _visionWeights = null;

            _weights?.Dispose();
            _weights = null;

            LoadedModelPath = string.Empty;
            _mediaMarker = string.Empty;
        }

        #endregion
    }
}