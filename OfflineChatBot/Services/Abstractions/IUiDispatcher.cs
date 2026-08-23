namespace OfflineChatBot.Services.Abstractions
{
    public interface IUiDispatcher
    {
        Task InvokeAsync(Action action, CancellationToken cancellationToken = default);
    }
}