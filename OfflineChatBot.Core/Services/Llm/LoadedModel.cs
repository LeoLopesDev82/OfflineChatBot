using System.Text;
using LLama;
using LLama.Abstractions;
using LLama.Common;
using LLama.Native;
using Microsoft.Extensions.Logging;

namespace OfflineChatBot.Services.Llm
{
    public sealed class LoadedModel : IDisposable
    {
        private const string VisionNotReadyMessage = "The selected vision model is not ready to process images.";

        private readonly ModelParams _parameters;
        private readonly LLamaWeights _weights;
        private readonly MtmdWeights? _visionWeights;
        private readonly string _mediaMarker;

        private LLamaContext _context;
        private InteractiveExecutor _executor;

        private LoadedModel(string path, ModelParams parameters, LLamaWeights weights, MtmdWeights? visionWeights, string mediaMarker)
        {
            Path = path;

            _parameters = parameters;
            _weights = weights;
            _visionWeights = visionWeights;
            _mediaMarker = mediaMarker;

            _context = weights.CreateContext(parameters);
            _executor = CreateExecutor();
        }

        public string Path { get; }

        public PromptFormat Format => _visionWeights == null ? PromptFormat.ChatMl : PromptFormat.Vicuna;

        public static LoadedModel Load(string modelPath, string? visionProjectionPath, int gpuLayers, uint contextSize, ILogger logger)
        {
            try
            {
                return LoadWith(modelPath, visionProjectionPath, gpuLayers, contextSize);
            }
            catch (Exception exception) when (gpuLayers > 0)
            {
                logger.LogWarning(exception, "Loading {ModelPath} on the GPU failed, falling back to the CPU", modelPath);

                return LoadWith(modelPath, visionProjectionPath, 0, contextSize);
            }
        }

        public int Tokenize(string text)
        {
            return _weights.Tokenize(text, add_bos: false, special: true, encoding: Encoding.UTF8).Length;
        }

        public int ConsumedTokens()
        {
            return _context.NativeHandle.MemorySequenceMaxPosition(LLamaSeqId.Zero).Value + 1;
        }

        public void RestartContext()
        {
            DisposeEmbeds();

            _visionWeights?.ClearMedia();

            _context.Dispose();
            _context = _weights.CreateContext(_parameters);
            _executor = CreateExecutor();
        }

        public string AttachImage(string userPrompt, string? imagePath)
        {
            if (string.IsNullOrEmpty(imagePath))
                return userPrompt;

            if (_visionWeights == null || string.IsNullOrEmpty(_mediaMarker))
                throw new InvalidOperationException(VisionNotReadyMessage);

            _executor.Embeds.Add(_visionWeights.LoadMedia(imagePath));

            return $"{_mediaMarker}\n{userPrompt}";
        }

        public IAsyncEnumerable<string> InferAsync(string prompt, IInferenceParams parameters, CancellationToken cancellationToken)
        {
            return _executor.InferAsync(prompt, parameters, cancellationToken);
        }

        public void Dispose()
        {
            DisposeEmbeds();

            _context.Dispose();
            _visionWeights?.Dispose();
            _weights.Dispose();
        }

        #region Private Methods

        private static LoadedModel LoadWith(string modelPath, string? visionProjectionPath, int gpuLayers, uint contextSize)
        {
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = contextSize,
                GpuLayerCount = gpuLayers
            };

            var weights = LLamaWeights.LoadFromFile(parameters);

            MtmdWeights? vision = null;

            try
            {
                var loaded = LoadVisionWeights(weights, visionProjectionPath);

                vision = loaded.Weights;

                return new LoadedModel(modelPath, parameters, weights, vision, loaded.MediaMarker);
            }
            catch
            {
                vision?.Dispose();
                weights.Dispose();

                throw;
            }
        }

        private static (MtmdWeights? Weights, string MediaMarker) LoadVisionWeights(LLamaWeights weights, string? visionProjectionPath)
        {
            if (string.IsNullOrEmpty(visionProjectionPath))
                return (null, string.Empty);

            var visionParameters = MtmdContextParams.Default();

            visionParameters.UseGpu = false;

            var vision = MtmdWeights.LoadFromFile(visionProjectionPath, weights, visionParameters);
            var marker = visionParameters.MediaMarker ?? NativeApi.MtmdDefaultMarker() ?? string.Empty;

            return (vision, marker);
        }

        private InteractiveExecutor CreateExecutor()
        {
            return _visionWeights == null
                ? new InteractiveExecutor(_context)
                : new InteractiveExecutor(_context, _visionWeights);
        }

        private void DisposeEmbeds()
        {
            foreach (var embed in _executor.Embeds)
                embed.Dispose();

            _executor.Embeds.Clear();
        }

        #endregion
    }
}
