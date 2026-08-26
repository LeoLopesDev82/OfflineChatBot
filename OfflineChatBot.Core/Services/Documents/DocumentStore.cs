using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class DocumentStore : IDocumentStore
    {
        private const int FormatVersion = 1;

        private readonly ILogger<DocumentStore> _logger;

        public DocumentStore(ILogger<DocumentStore> logger)
        {
            _logger = logger;
        }

        public Task SaveAsync(string sessionId, IndexedDocument document)
        {
            return Task.Run(() => Save(sessionId, document));
        }

        public Task<IndexedDocument?> LoadAsync(string sessionId)
        {
            return Task.Run(() => Load(sessionId));
        }

        public Task DeleteAsync(string sessionId)
        {
            return Task.Run(() => Delete(sessionId));
        }

        #region Private Methods

        private static string PathFor(string sessionId)
        {
            return Path.Combine(PathHelper.DocumentsFolder, $"{sessionId}.bin");
        }

        private void Save(string sessionId, IndexedDocument document)
        {
            using var stream = File.Create(PathFor(sessionId));
            using var writer = new BinaryWriter(stream, Encoding.UTF8);

            writer.Write(FormatVersion);
            writer.Write(document.Name);
            writer.Write(document.Chunks.Count);

            foreach (var chunk in document.Chunks)
            {
                writer.Write(chunk.Index);
                writer.Write(chunk.Text);
                writer.Write(chunk.Embedding.Length);

                foreach (var value in chunk.Embedding)
                    writer.Write(value);
            }

            _logger.LogInformation("Stored {ChunkCount} chunks of {DocumentName} for session {SessionId}", document.Chunks.Count, document.Name, sessionId);
        }

        private IndexedDocument? Load(string sessionId)
        {
            var path = PathFor(sessionId);

            if (!File.Exists(path))
                return null;

            try
            {
                return Read(path);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not read the stored document for session {SessionId}", sessionId);

                return null;
            }
        }

        private static IndexedDocument Read(string path)
        {
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream, Encoding.UTF8);

            reader.ReadInt32();

            var name = reader.ReadString();
            var count = reader.ReadInt32();
            var chunks = new List<IndexedChunk>(count);

            for (var position = 0; position < count; position++)
                chunks.Add(ReadChunk(reader));

            return new IndexedDocument { Name = name, Chunks = chunks };
        }

        private static IndexedChunk ReadChunk(BinaryReader reader)
        {
            var index = reader.ReadInt32();
            var text = reader.ReadString();
            var length = reader.ReadInt32();
            var embedding = new float[length];

            for (var position = 0; position < length; position++)
                embedding[position] = reader.ReadSingle();

            return new IndexedChunk(index, text, embedding);
        }

        private void Delete(string sessionId)
        {
            var path = PathFor(sessionId);

            if (!File.Exists(path))
                return;

            File.Delete(path);

            _logger.LogInformation("Removed the stored document of session {SessionId}", sessionId);
        }

        #endregion
    }
}
