using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using OfflineChatBot.Services.Abstractions;
using OfflineChatBot.Views;

namespace OfflineChatBot.Services.Platform
{
    public sealed class DialogService : IDialogService
    {
        private const string ImageFilter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*";
        private const string DocumentFilter = "Documents|*.pdf;*.docx;*.xlsx;*.txt;*.md;*.csv|All Files|*.*";

        private readonly IServiceProvider _serviceProvider;

        public DialogService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public Task ShowInformationAsync(string message, string caption)
        {
            return AskAsync(message, caption, MessageBoxButton.OK);
        }

        public async Task<bool> ConfirmAsync(string message, string caption)
        {
            return await AskAsync(message, caption, MessageBoxButton.YesNo) == MessageBoxResult.Yes;
        }

        public string? PickImageFile()
        {
            return Pick(ImageFilter);
        }

        public string? PickDocumentFile()
        {
            return Pick(DocumentFilter);
        }

        public void ShowModelManager()
        {
            var window = _serviceProvider.GetRequiredService<ModelManagerWindow>();

            window.Owner = Application.Current.MainWindow;

            window.ShowDialog();
        }

        #region Private Methods

        private static Task<MessageBoxResult> AskAsync(string message, string caption, MessageBoxButton button)
        {
            var host = HostOfActiveWindow();

            if (host == null)
                return Task.FromResult(MessageBox.Show(message, caption, button));

            return host.AskAsync(message, caption, button);
        }

        private static DialogHost? HostOfActiveWindow()
        {
            var window = Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(candidate => candidate.IsLoaded && candidate.IsActive);

            return window == null ? null : FindHost(window);
        }

        private static DialogHost? FindHost(DependencyObject parent)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);

                if (child is DialogHost host)
                    return host;

                var found = FindHost(child);

                if (found != null)
                    return found;
            }

            return null;
        }

        private static string? Pick(string filter)
        {
            var dialog = new OpenFileDialog { Filter = filter };

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        #endregion
    }
}
