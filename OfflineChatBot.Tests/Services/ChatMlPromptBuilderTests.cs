using OfflineChatBot.Models;
using OfflineChatBot.Services.Llm;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class ChatMlPromptBuilderTests
    {
        [Fact]
        public void Build_WithoutHistory_StartsWithSystemPromptAndEndsWaitingForAssistant()
        {
            var prompt = BuildNormalized([], "Hello");

            Assert.StartsWith("<|im_start|>system", prompt);
            Assert.Contains(ChatMlPromptBuilder.SystemPrompt, prompt);
            Assert.Contains("<|im_start|>user\nHello", prompt);
            Assert.EndsWith("<|im_start|>assistant\n", prompt);
        }

        [Fact]
        public void Build_WithHistory_MapsSendersToRoles()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Sender = MessageSender.User, Content = "First question" },
                new ChatMessage { Sender = MessageSender.Assistant, Content = "First answer" }
            };

            var prompt = BuildNormalized(history, "Second question");

            Assert.Contains("<|im_start|>user\nFirst question", prompt);
            Assert.Contains("<|im_start|>assistant\nFirst answer", prompt);
        }

        [Fact]
        public void Build_SystemMessagesInHistory_AreIgnored()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Sender = MessageSender.System, Content = "Internal note" }
            };

            var prompt = BuildNormalized(history, "Hello");

            Assert.DoesNotContain("Internal note", prompt);
        }

        [Fact]
        public void Build_LongHistory_KeepsOnlyTheLastTenMessages()
        {
            var history = Enumerable.Range(1, 14)
                .Select(index => new ChatMessage { Sender = MessageSender.User, Content = $"Message {index}" })
                .ToList();

            var prompt = BuildNormalized(history, "Now");

            Assert.DoesNotContain("Message 4", prompt);
            Assert.Contains("Message 5", prompt);
            Assert.Contains("Message 14", prompt);
        }

        [Fact]
        public void RemoveStopTokens_StripsEveryKnownToken()
        {
            var cleaned = ChatMlPromptBuilder.RemoveStopTokens("a<|im_end|>b<|im_start|>c<|endoftext|>");

            Assert.Equal("abc", cleaned);
        }

        private static string BuildNormalized(IEnumerable<ChatMessage> history, string userPrompt)
        {
            return ChatMlPromptBuilder.Build(history, userPrompt).ReplaceLineEndings("\n");
        }
    }
}