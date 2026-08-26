using System.IO;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeDocumentReader : IDocumentReader
    {
        public Exception? ReadFailure { get; set; }
        public string Text { get; set; } = "The delivery takes thirty days.";
        public int Tokens { get; set; } = 120;
        public int Parts { get; set; } = 1;
        public TaskCompletionSource? Gate { get; set; }

        public bool CanRead(string filePath)
        {
            return true;
        }

        public async Task<ReadDocument> ReadAsync(string filePath, CancellationToken cancellationToken = default)
        {
            if (ReadFailure != null)
                throw ReadFailure;

            if (Gate != null)
                await Gate.Task;

            return Measure(Path.GetFileName(filePath), Text);
        }

        public ReadDocument Measure(string name, string text)
        {
            return new ReadDocument
            {
                Name = name,
                Text = text,
                Tokens = Tokens,
                Parts = Parts
            };
        }
    }
}
