using System.IO;
using System.Text.Json;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services
{
    public class ChatStorageService : IChatStorageService
    {
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

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
            catch
            {
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