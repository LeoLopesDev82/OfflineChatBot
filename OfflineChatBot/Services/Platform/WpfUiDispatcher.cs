using System.Windows;
using System.Windows.Threading;
using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Services.Platform
{
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        public Task InvokeAsync(Action action, CancellationToken cancellationToken = default)
        {
            return Application.Current.Dispatcher
                .InvokeAsync(action, DispatcherPriority.Background, cancellationToken)
                .Task;
        }
    }
}