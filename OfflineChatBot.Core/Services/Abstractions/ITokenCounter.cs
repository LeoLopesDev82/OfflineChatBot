namespace OfflineChatBot.Services.Abstractions
{
    public interface ITokenCounter
    {
        int Count(string text);
    }
}