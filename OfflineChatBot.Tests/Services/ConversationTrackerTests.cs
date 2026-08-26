using OfflineChatBot.Models;
using OfflineChatBot.Services.Llm;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class ConversationTrackerTests
    {
        [Fact]
        public void ANewTracker_HasNothingToContinue()
        {
            var tracker = Create();

            Assert.False(tracker.CanContinue("chat-1", 0, string.Empty, false, 10));
        }

        [Fact]
        public void AfterATurn_TheSameConversationContinues()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 300);

            Assert.True(tracker.CanContinue("chat-1", 2, string.Empty, false, 10));
        }

        [Fact]
        public void AnotherConversation_StartsOver()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 300);

            Assert.False(tracker.CanContinue("chat-2", 2, string.Empty, false, 10));
        }

        [Fact]
        public void AttachingADocument_StartsOver()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 300);

            Assert.False(tracker.CanContinue("chat-1", 2, "excerpt from the contract", false, 10));
        }

        [Fact]
        public void AnImage_StartsOver()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 300);

            Assert.False(tracker.CanContinue("chat-1", 2, string.Empty, true, 10));
        }

        [Fact]
        public void AHistoryThatDoesNotFollowFromTheLastTurn_StartsOver()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 300);

            Assert.False(tracker.CanContinue("chat-1", 4, string.Empty, false, 10));
        }

        [Fact]
        public void SeveralTurns_KeepContinuing()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 100);
            tracker.Advance("chat-1", 2, string.Empty, 100);
            tracker.Advance("chat-1", 4, string.Empty, 100);

            Assert.True(tracker.CanContinue("chat-1", 6, string.Empty, false, 10));
        }

        [Fact]
        public void WhenTheAnswerWouldNotFitInWhatIsLeft_StartsOver()
        {
            var tracker = Create(contextSize: 1000, maxTokens: 200);

            tracker.Advance("chat-1", 0, string.Empty, 700);

            Assert.True(tracker.CanContinue("chat-1", 2, string.Empty, false, 100));
            Assert.False(tracker.CanContinue("chat-1", 2, string.Empty, false, 101));
        }

        [Fact]
        public void TheRoomLeft_ShrinksWithEveryTurn()
        {
            var tracker = Create(contextSize: 1000, maxTokens: 200);

            tracker.Advance("chat-1", 0, string.Empty, 400);

            Assert.True(tracker.CanContinue("chat-1", 2, string.Empty, false, 300));

            tracker.Advance("chat-1", 2, string.Empty, 400);

            Assert.False(tracker.CanContinue("chat-1", 4, string.Empty, false, 300));
        }

        [Fact]
        public void Invalidating_StartsOver()
        {
            var tracker = Create();

            tracker.Advance("chat-1", 0, string.Empty, 300);
            tracker.Invalidate();

            Assert.False(tracker.CanContinue("chat-1", 2, string.Empty, false, 10));
        }

        private static ConversationTracker Create(uint contextSize = 8192, int maxTokens = 2048)
        {
            return new ConversationTracker(new GenerationOptions { ContextSize = contextSize, MaxTokens = maxTokens });
        }
    }
}
