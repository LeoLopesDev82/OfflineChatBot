using System.IO;
using LLama;
using LLama.Common;
using LLama.Extensions;
using LLama.Native;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Llm
{
    public sealed class LlamaEmbeddingService : IEmbeddingService, IDisposable
    {
        private const uint EmbeddingContextSize = 1024;

        private readonly GenerationOptions _options;
        private readonly ILogger<LlamaEmbeddingService> _logger;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        private LLamaWeights? _weights;
        private LLamaEmbedder? _embedder;

        public LlamaEmbeddingService(IOptions<GenerationOptions> options, ILogger<LlamaEmbeddingService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public bool IsLoaded => _embedder != null;

        public async Task LoadAsync(string modelPath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(modelPath))
                throw new FileNotFoundException("Embedding model file not found.", modelPath);

            await _lock.WaitAsync(cancellationToken);

            try
            {
                if (IsLoaded)
                    return;

                await Task.Run(() => Load(modelPath), cancellationToken);

                _logger.LogInformation("Loaded embedding model {ModelPath} with {Dimensions} dimensions", modelPath, _embedder!.EmbeddingSize);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task UnloadAsync()
        {
            await _lock.WaitAsync();

            try
            {
                Unload();
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            await _lock.WaitAsync(cancellationToken);

            try
            {
                var embedder = _embedder ?? throw new InvalidOperationException("The embedding model is not loaded.");
                var vectors = await embedder.GetEmbeddings(text, cancellationToken);

                return vectors[0].EuclideanNormalization();
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Dispose()
        {
            Unload();

            _lock.Dispose();
        }

        #region Private Methods

        private void Load(string modelPath)
        {
            var parameters = new ModelParams(modelPath)
            {
                ContextSize = EmbeddingContextSize,
                Embeddings = true,
                PoolingType = LLamaPoolingType.Mean,
                GpuLayerCount = _options.UseGpu ? _options.GpuLayerCount : 0
            };

            _weights = LLamaWeights.LoadFromFile(parameters);
            _embedder = new LLamaEmbedder(_weights, parameters);
        }

        private void Unload()
        {
            _embedder?.Dispose();
            _embedder = null;

            _weights?.Dispose();
            _weights = null;
        }

        #endregion
    }
}
