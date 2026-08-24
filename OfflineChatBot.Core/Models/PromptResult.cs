namespace OfflineChatBot.Models
{
    public readonly record struct PromptResult(string Text, int TokenCount, int IncludedMessages, int DroppedMessages);
}