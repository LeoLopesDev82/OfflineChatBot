using OfflineChatBot.Models;
using OfflineChatBot.Services.Llm;
using OfflineChatBot.Tests.Fakes;
using Xunit;

namespace OfflineChatBot.Tests.Services
{
    public class PromptBuilderTests
    {
        [Fact]
        public void Build_WithoutHistory_StartsWithSystemPromptAndEndsWaitingForAssistant()
        {
            var prompt = BuildNormalized([], "Hello");

            Assert.StartsWith("<|im_start|>system", prompt);
            Assert.Contains(PromptFormat.ChatMl.SystemPrompt, prompt);
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
        public void Build_WhenEverythingFits_KeepsTheWholeHistory()
        {
            var history = CreateHistory(6);

            var result = CreateBuilder(contextSize: 8192).Build(history, "Now");

            Assert.Equal(6, result.IncludedMessages);
            Assert.Equal(0, result.DroppedMessages);
        }

        [Fact]
        public void Build_WhenHistoryExceedsTheBudget_DropsTheOldestMessagesFirst()
        {
            var history = CreateHistory(40);

            var result = CreateBuilder(contextSize: 700, maxTokens: 100).Build(history, "Now");

            Assert.True(result.DroppedMessages > 0);
            Assert.Equal(40, result.IncludedMessages + result.DroppedMessages);
            Assert.DoesNotContain("Message 1 ", result.Text);
            Assert.Contains("Message 40 ", result.Text);
        }

        [Fact]
        public void Build_ReservesRoomForTheAnswer()
        {
            var history = CreateHistory(40);

            var generous = CreateBuilder(contextSize: 2000, maxTokens: 100).Build(history, "Now");
            var reserved = CreateBuilder(contextSize: 2000, maxTokens: 1500).Build(history, "Now");

            Assert.True(reserved.IncludedMessages < generous.IncludedMessages);
        }

        [Fact]
        public void Build_WhenNothingFits_KeepsOnlyTheCurrentQuestion()
        {
            var history = CreateHistory(10);

            var result = CreateBuilder(contextSize: 200, maxTokens: 100).Build(history, "Now");

            Assert.Equal(0, result.IncludedMessages);
            Assert.Equal(10, result.DroppedMessages);
            Assert.Contains("Now", result.Text);
        }

        [Fact]
        public void Build_ReportsHowManyTokensThePromptUses()
        {
            var result = CreateBuilder(contextSize: 8192).Build([], "Now");

            Assert.Equal(result.Text.Length / 4, result.TokenCount);
        }

        [Fact]
        public void Build_HonoursTheConfiguredHistoryCeiling()
        {
            var history = CreateHistory(40);

            var uncapped = CreateBuilder(contextSize: 8192).Build(history, "Now");
            var capped = CreateBuilder(contextSize: 8192, maxHistoryTokens: 200).Build(history, "Now");

            Assert.True(capped.IncludedMessages < uncapped.IncludedMessages);
            Assert.True(capped.IncludedMessages > 0);
        }

        [Fact]
        public void RemoveStopTokens_StripsEveryKnownToken()
        {
            var cleaned = PromptFormat.ChatMl.RemoveStopTokens("a<|im_end|>b<|im_start|>c<|endoftext|>");

            Assert.Equal("abc", cleaned);
        }

        [Fact]
        public void Build_InVicuna_UsesTheFormatTheVisionModelWasTrainedOn()
        {
            var history = new List<ChatMessage>
            {
                new ChatMessage { Sender = MessageSender.User, Content = "First question" },
                new ChatMessage { Sender = MessageSender.Assistant, Content = "First answer" }
            };

            var prompt = CreateBuilder(contextSize: 8192, format: PromptFormat.Vicuna).Build(history, "Second question").Text;

            Assert.StartsWith(PromptFormat.Vicuna.SystemPrompt, prompt);
            Assert.Contains("USER: First question", prompt);
            Assert.Contains("ASSISTANT: First answer</s>", prompt);
            Assert.EndsWith("ASSISTANT:", prompt);
            Assert.DoesNotContain("<|im_start|>", prompt);
        }

        [Fact]
        public void Build_InVicuna_StillDropsTheOldestMessagesWhenTheBudgetRunsOut()
        {
            var history = CreateHistory(40);

            var result = CreateBuilder(contextSize: 700, maxTokens: 100, format: PromptFormat.Vicuna).Build(history, "Now");

            Assert.True(result.DroppedMessages > 0);
            Assert.Equal(40, result.IncludedMessages + result.DroppedMessages);
            Assert.DoesNotContain("Message 1 ", result.Text);
            Assert.Contains("Message 40 ", result.Text);
        }

        [Fact]
        public void RemoveStopTokens_InVicuna_StripsTheEndOfTurnMarker()
        {
            var cleaned = PromptFormat.Vicuna.RemoveStopTokens("an answer</s>");

            Assert.Equal("an answer", cleaned);
        }
        private static PromptBuilder CreateBuilder(uint contextSize, int maxTokens = 2048, int maxHistoryTokens = int.MaxValue, PromptFormat? format = null)
        {
            var options = new GenerationOptions
            {
                ContextSize = contextSize,
                MaxTokens = maxTokens,
                MaxHistoryTokens = maxHistoryTokens
            };

            return new PromptBuilder(new FakeTokenCounter(), options, format ?? PromptFormat.ChatMl);
        }

        private static List<ChatMessage> CreateHistory(int count)
        {
            return Enumerable.Range(1, count)
                .Select(index => new ChatMessage
                {
                    Sender = index % 2 == 1 ? MessageSender.User : MessageSender.Assistant,
                    Content = $"Message {index} with enough words to take up a measurable amount of space"
                })
                .ToList();
        }

        private static string BuildNormalized(IEnumerable<ChatMessage> history, string userPrompt)
        {
            return CreateBuilder(contextSize: 8192).Build(history, userPrompt).Text.ReplaceLineEndings("\n");
        }
    }
}
