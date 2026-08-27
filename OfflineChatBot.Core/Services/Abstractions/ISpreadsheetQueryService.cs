using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Abstractions
{
    public interface ISpreadsheetQueryService
    {
        bool CanQuery(string? filePath);

        Task<QueryOutcome> AskAsync(string filePath, string question, CancellationToken cancellationToken = default);
    }
}
