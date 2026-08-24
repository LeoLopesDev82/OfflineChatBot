using System.Text.Json;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Chat
{
    public class ChatStorageService : IChatStorageService
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly ILogger<ChatStorageService> _logger;

        public ChatStorageService(ILogger<ChatStorageService> logger)
        {
            _logger = logger;
        }

        public async Task<List<ChatSession>> LoadSessionsAsync()
        {
            var filePath = PathHelper.HistoryFilePath;

            if (!File.Exists(filePath))
                return new List<ChatSession>();

            try
            {
                var json = await File.ReadAllTextAsync(filePath);
                var sessions = JsonSerializer.Deserialize<List<ChatSession>>(json, _jsonOptions);

                return sessions ?? new List<ChatSession>();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not read the chat history at {FilePath}, starting with no sessions", filePath);

                return new List<ChatSession>();
            }
        }

        public async Task SaveSessionsAsync(IEnumerable<ChatSession> sessions)
        {
            var filePath = PathHelper.HistoryFilePath;
            var json = JsonSerializer.Serialize(sessions, _jsonOptions);

            await File.WriteAllTextAsync(filePath, json);
        }
    }
}