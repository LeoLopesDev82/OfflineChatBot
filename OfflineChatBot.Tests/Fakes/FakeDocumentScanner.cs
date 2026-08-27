using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeDocumentScanner : IDocumentScanner
    {
        public string Notes { get; set; } = "From part 1 of 3:\nThe delivery takes thirty days.";
        public string? LastQuestion { get; private set; }
        public int ScanCount { get; private set; }

        public Task<string> ScanAsync(
            ReadDocument document,
            string question,
            IProgress<ScanProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            LastQuestion = question;
            ScanCount++;

            progress?.Report(new ScanProgress(1, document.Parts, TimeSpan.Zero));

            return Task.FromResult(Notes);
        }
    }
}
