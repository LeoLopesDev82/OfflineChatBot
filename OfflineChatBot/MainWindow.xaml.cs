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

        #region Event Handlers

        private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (ShouldIgnoreClickForEditMode(e.OriginalSource as DependencyObject))
                return;

            CloseAllEditModesAndSave();
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.InitializeAsync();

            ViewModel.PropertyChanged += ViewModel_PropertyChanged;

            SubscribeToCurrentSession();
            ScrollToBottom();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        
        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (WindowState == WindowState.Maximized)
                WindowState = WindowState.Normal;
            else
                WindowState = WindowState.Maximized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ViewModel.CurrentSession))
            {
                SubscribeToCurrentSession();

                Dispatcher.InvokeAsync(ScrollToBottom, DispatcherPriority.Background);
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

        private void RenameChat_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ChatSession session)
            {
                foreach (var s in ViewModel.Sessions)
                {
                    s.IsEditing = false;
                }
                
                session.IsEditing = true;
            }
        }

        private void TitleTextBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.TextBox textBox && textBox.IsVisible)
            {
                Dispatcher.InvokeAsync(() => 
                {
                    textBox.Focus();
                    Keyboard.Focus(textBox);
                    textBox.SelectAll();
                }, DispatcherPriority.Input);
            }
        }

        private void TitleTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ChatSession session)
            {
                if (session.IsEditing)
                {
                    session.IsEditing = false;
                    
                    ViewModel.SaveSessionsSilently();
                }
            }
        }

        private void TitleTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Escape)
            {
                if (sender is FrameworkElement element && element.DataContext is ChatSession session)
                {
                    session.IsEditing = false;

                    if (e.Key == Key.Enter)
                    {
                        ViewModel.SaveSessionsSilently();
                    }
                }
        
                e.Handled = true;
                
                Keyboard.ClearFocus();
            }
        }

        #endregion

        #region Private Methods

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

        private void ScrollToBottom()
        {
            try { ChatScrollViewer.ScrollToEnd(); } catch { }
        }

        private bool ShouldIgnoreClickForEditMode(DependencyObject? src)
        {
            if (src == null) return false;

            var btn = FindParent<System.Windows.Controls.Button>(src);
            
            if (btn != null && btn.Name == "BtnRename")
                return true;

            var textBox = FindParent<System.Windows.Controls.TextBox>(src);
            
            if (textBox != null)
                return true;

            return false;
        }

        private void CloseAllEditModesAndSave()
        {
            bool savedAny = false;

            foreach (var session in ViewModel.Sessions)
            {
                if (session.IsEditing)
                {
                    session.IsEditing = false;
            
                    savedAny = true;
                }
            }

            if (savedAny)
            {
                ViewModel.SaveSessionsSilently();
                
                Keyboard.ClearFocus();
                FocusManager.SetFocusedElement(this, this);
            }
        }

        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            DependencyObject? parent = child;
            
            while (parent != null)
            {
                if (parent is T typed) return typed;
                
                DependencyObject? logicalParent = LogicalTreeHelper.GetParent(parent);
                
                if (logicalParent != null)
                {
                    parent = logicalParent;
                
                    continue;
                }
                
                if (parent is System.Windows.Media.Visual || parent is System.Windows.Media.Media3D.Visual3D)
                {
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
                else
                {
                    break;
                }
            }
            
            return null;
        }

        #endregion
    }
}