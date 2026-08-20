using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using OfflineChatBot.Models;
using OfflineChatBot.ViewModels;

namespace OfflineChatBot
{
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainViewModel();
            DataContext = ViewModel;

            Loaded += MainWindow_Loaded;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            SubscribeToCurrentSession();
            ScrollToBottom();
        }

        #region Title Bar Buttons

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        #endregion 

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentSession))
            {
                SubscribeToCurrentSession();

                Dispatcher.InvokeAsync(ScrollToBottom, DispatcherPriority.Background);
            }
        }

        private void SubscribeToCurrentSession()
        {
            if (ViewModel.CurrentSession != null)
            {
                ViewModel.CurrentSession.Messages.CollectionChanged -= Messages_CollectionChanged;
                ViewModel.CurrentSession.Messages.CollectionChanged += Messages_CollectionChanged;

                foreach (var msg in ViewModel.CurrentSession.Messages)
                {
                    msg.PropertyChanged -= Msg_PropertyChanged;
                    msg.PropertyChanged += Msg_PropertyChanged;
                }
            }
        }

        private void Messages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.NewItems != null)
            {
                foreach (ChatMessage msg in e.NewItems)
                {
                    msg.PropertyChanged -= Msg_PropertyChanged;
                    msg.PropertyChanged += Msg_PropertyChanged;
                }
            }

            Dispatcher.InvokeAsync(ScrollToBottom, DispatcherPriority.Background);
        }

        private void Msg_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ChatMessage.Content) || e.PropertyName == nameof(ChatMessage.IsStreaming))
            {
                Dispatcher.InvokeAsync(ScrollToBottom, DispatcherPriority.Background);
            }
        }

        private void ScrollToBottom()
        {
            try { ChatScrollViewer.ScrollToEnd(); } catch { }
        }

        private void UserInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;

                if (ViewModel.SendMessageCommand.CanExecute(null))
                {
                    ViewModel.SendMessageCommand.Execute(null);
                
                    ScrollToBottom();
                }
            }
        }
    }
}