using System.Windows;

namespace OfflineChatBot.Views
{
    public partial class CustomMessageBoxWindow : Window
    {
        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public CustomMessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button)
        {
            InitializeComponent();
            
            MessageText.Text = messageBoxText;
            TitleText.Text = caption;

            switch (button)
            {
                case MessageBoxButton.OK:
                    BtnOk.Visibility = Visibility.Visible;
 
                    BtnOk.Focus();
                    
                    break;
                case MessageBoxButton.OKCancel:
                    BtnOk.Visibility = Visibility.Visible;                    
                    BtnCancel.Visibility = Visibility.Visible;
                    
                    BtnOk.Focus();
                    
                    break;
                case MessageBoxButton.YesNo:
                    BtnYes.Visibility = Visibility.Visible;                    
                    BtnNo.Visibility = Visibility.Visible;
                    
                    BtnYes.Focus();
                    
                    break;
                case MessageBoxButton.YesNoCancel:
                    BtnYes.Visibility = Visibility.Visible;
                    BtnNo.Visibility = Visibility.Visible;
                    BtnCancel.Visibility = Visibility.Visible;

                    BtnYes.Focus();
                    
                    break;
            }
        }

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, Window? owner = null)
        {
            var msgBox = new CustomMessageBoxWindow(messageBoxText, caption, button);
            
            if (owner != null)
            {
                msgBox.Owner = owner;
            }
            else if (Application.Current.MainWindow != null && Application.Current.MainWindow.IsLoaded)
            {
                msgBox.Owner = Application.Current.MainWindow;
            }
            
            msgBox.ShowDialog();

            return msgBox.Result;
        }

        #region Event Handlers

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.OK;
            
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Cancel;
            
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.Yes;
            
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = MessageBoxResult.No;
            
            Close();
        }

        #endregion

    }
}