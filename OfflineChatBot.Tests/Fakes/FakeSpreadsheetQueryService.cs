using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeSpreadsheetQueryService : ISpreadsheetQueryService
    {
        public bool Queryable { get; set; }
        public bool Answered { get; set; } = true;
        public string Result { get; set; } = "sum = 764 over 29 rows of VENDA.";
        public string? LastQuestion { get; private set; }
        public int AskCount { get; private set; }

        public bool CanQuery(string? filePath)
        {
            return Queryable && filePath != null;
        }

        public Task<QueryOutcome> AskAsync(string filePath, string question, CancellationToken cancellationToken = default)
        {
            LastQuestion = question;
            AskCount++;

            return Task.FromResult(new QueryOutcome(Answered, Result));
        }
    }
}
