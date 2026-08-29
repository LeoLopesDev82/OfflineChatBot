using OfflineChatBot.Models;
using Xunit;

namespace OfflineChatBot.Tests.Models
{
    public class ChatSessionTests
    {
        [Fact]
        public void Snapshot_CopiesEverythingThatIsSaved()
        {
            var session = new ChatSession
            {
                Title = "A conversation",
                DocumentName = "contract.pdf",
                DocumentPath = @"C:\docs\contract.pdf",
                DocumentTokens = 1840,
                DocumentParts = 3
            };

            session.AddUserMessage("A question", null, null);

            var snapshot = session.Snapshot();

            Assert.Equal(session.Id, snapshot.Id);
            Assert.Equal("A conversation", snapshot.Title);
            Assert.Equal(session.CreatedAt, snapshot.CreatedAt);
            Assert.Equal("contract.pdf", snapshot.DocumentName);
            Assert.Equal(@"C:\docs\contract.pdf", snapshot.DocumentPath);
            Assert.Equal(1840, snapshot.DocumentTokens);
            Assert.Equal(3, snapshot.DocumentParts);
            Assert.Equal("A question", Assert.Single(snapshot.Messages).Content);
        }

        [Fact]
        public void Snapshot_DoesNotChangeWhenTheConversationGrowsAfterwards()
        {
            var session = new ChatSession();

            session.AddUserMessage("A question", null, null);

            var snapshot = session.Snapshot();

            session.AddStreamingAssistantMessage();

            Assert.Single(snapshot.Messages);
            Assert.Equal(2, session.Messages.Count);
        }
    }
}
