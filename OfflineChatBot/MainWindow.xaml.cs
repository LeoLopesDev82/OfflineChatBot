using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;
using OfflineChatBot.ViewModels;
using OfflineChatBot.Views;

namespace OfflineChatBot
{
    public partial class MainWindow : ChromelessWindow
    {
        public MainViewModel ViewModel { get; }

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();

            ViewModel = viewModel;
            DataContext = viewModel;

            Loaded += OnLoaded;
        }

        #region Event Handlers

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();
        }

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!ViewModel.HasOpenRename || IsRenameRelated(e.OriginalSource as DependencyObject))
                return;

            ViewModel.CommitAllRenamesCommand.Execute(null);

            Keyboard.ClearFocus();
            FocusManager.SetFocusedElement(this, this);
        }

        private void AttachmentMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement { ContextMenu: { } attachmentMenu })
                return;

            attachmentMenu.PlacementTarget = (UIElement)sender;
            attachmentMenu.IsOpen = true;
        }

        private void TitleTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox { IsVisible: true } textBox)
                return;

            Dispatcher.InvokeAsync(() =>
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                textBox.SelectAll();
            }, DispatcherPriority.Input);
        }

        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            ViewModel.CommitRenameChatCommand.Execute(SessionOf(sender));
        }

        private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter && e.Key != Key.Escape)
                return;

            ViewModel.CommitRenameChatCommand.Execute(SessionOf(sender));

            e.Handled = true;

            Keyboard.ClearFocus();
        }

        #endregion

        #region Private Methods

        private static ChatSession? SessionOf(object sender)
        {
            return (sender as FrameworkElement)?.DataContext as ChatSession;
        }

        private static bool IsRenameRelated(DependencyObject? source)
        {
            if (source == null)
                return false;

            return source.FindParent<TextBox>() != null || source.FindParent<Button>()?.Name == "BtnRename";
        }

        #endregion
    }
}