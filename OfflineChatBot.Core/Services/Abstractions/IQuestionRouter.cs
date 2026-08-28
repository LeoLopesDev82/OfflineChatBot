namespace OfflineChatBot.Services.Abstractions
{
    public interface IQuestionRouter
    {
        bool NeedsDocument(string message);
    }
}
