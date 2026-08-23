using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OfflineChatBot.Services.Abstractions;
using OfflineChatBot.Views;

namespace OfflineChatBot.Services.Platform
{
    public sealed class DialogService : IDialogService
    {
        private const string ImageFilter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*";

        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void ShowInformation(string message, string caption)
        {
            CustomMessageBoxWindow.Show(message, caption, MessageBoxButton.OK);
        }

        public bool Confirm(string message, string caption)
        {
            return CustomMessageBoxWindow.Show(message, caption, MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        public string? PickImageFile()
        {
            var dialog = new OpenFileDialog
            {
                Filter = ImageFilter
            };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public void ShowModelManager()
        {
            var window = _serviceProvider.GetRequiredService<ModelManagerWindow>();

            window.Owner = Application.Current.MainWindow;

            window.ShowDialog();
        }
    }
}