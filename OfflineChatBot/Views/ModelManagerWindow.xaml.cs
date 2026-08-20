using System.Windows;
using OfflineChatBot.Models;
using OfflineChatBot.ViewModels;

namespace OfflineChatBot.Views
{
    public partial class ModelManagerWindow : Window
    {
        public MainViewModel ViewModel => (MainViewModel)DataContext;

        public ModelManagerWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            DataContext = viewModel;
        }

        #region Event Handlers

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private async void DownloadModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ModelInfo model)
            {
                model.DownloadCts = new CancellationTokenSource();
            
                await ViewModel.DownloadModelWithCtsAsync(model, model.DownloadCts.Token);
            }
        }

        private void CancelDownload_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ModelInfo model)
            {
                model.DownloadCts?.Cancel();
            }
        }

        private async void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ModelInfo model)
            {
                var result = CustomMessageBoxWindow.Show($"Are you sure you want to delete the model file for {model.Name}?", "Confirm Deletion", MessageBoxButton.YesNo, this);
                
                if (result == MessageBoxResult.Yes)
                {
                    await ViewModel.DeleteModelAsync(model);
                }
            }
        }

        #endregion

    }
}