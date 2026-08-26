using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Documents
{
    public sealed class DocumentStore : IDocumentStore
    {
        private readonly ILogger<DocumentStore> _logger;

        public DocumentStore(ILogger<DocumentStore> logger)
        {
            _logger = logger;
        }

        public Task SaveAsync(string sessionId, string text)
        {
            return Task.Run(() => Save(sessionId, text));
        }

        public Task<string?> LoadAsync(string sessionId)
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
            return Path.Combine(PathHelper.DocumentsFolder, $"{sessionId}.txt");
        }

        private void Save(string sessionId, string text)
        {
            File.WriteAllText(PathFor(sessionId), text, Encoding.UTF8);

            _logger.LogInformation("Stored the text of the document attached to session {SessionId}", sessionId);
        }

        private string? Load(string sessionId)
        {
            var path = PathFor(sessionId);

            if (!File.Exists(path))
                return null;

            try
            {
                return File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not read the stored document of session {SessionId}", sessionId);

                return null;
            }
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
