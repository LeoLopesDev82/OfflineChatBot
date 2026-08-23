using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using OfflineChatBot.Services.Abstractions;
using OfflineChatBot.Services.Chat;
using OfflineChatBot.Services.Llm;
using OfflineChatBot.Services.Models;
using OfflineChatBot.Services.Platform;
using OfflineChatBot.ViewModels;
using OfflineChatBot.Views;

namespace OfflineChatBot
{
    public partial class App : Application
    {
        private ServiceProvider? _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _services = BuildServiceProvider();

            MainWindow = _services.GetRequiredService<MainWindow>();
            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.Dispose();

            base.OnExit(e);
        }

        #region Private Methods

        private static ServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();

            services.AddSingleton<ILlmService, LlamaSharpService>();
            services.AddSingleton<IModelManagerService, ModelManagerService>();
            services.AddSingleton<IChatStorageService, ChatStorageService>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IResourceMonitor, ProcessResourceMonitor>();
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();

            services.AddSingleton<AppStatusViewModel>();
            services.AddSingleton<ModelManagerViewModel>();
            services.AddSingleton<MainViewModel>();

            services.AddSingleton<MainWindow>();
            services.AddTransient<ModelManagerWindow>();

            return services.BuildServiceProvider();
        }

        #endregion
    }
}