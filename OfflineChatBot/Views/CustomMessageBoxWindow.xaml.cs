using System.Windows;
using System.Windows.Controls;

namespace OfflineChatBot.Views
{
    public partial class CustomMessageBoxWindow : Window
    {
        public CustomMessageBoxWindow(string messageBoxText, string caption, MessageBoxButton button)
        {
            InitializeComponent();

            MessageText.Text = messageBoxText;
            TitleText.Text = caption;

            ShowButtons(button);
        }

        public MessageBoxResult Result { get; private set; } = MessageBoxResult.None;

        public static MessageBoxResult Show(string messageBoxText, string caption, MessageBoxButton button, Window? owner = null)
        {
            var messageBox = new CustomMessageBoxWindow(messageBoxText, caption, button)
            {
                Owner = owner ?? ActiveOwner()
            };

            messageBox.ShowDialog();

            return messageBox.Result;
        }

        #region Event Handlers

        private void ResultButton_Click(object sender, RoutedEventArgs e)
        {
            Result = Enum.Parse<MessageBoxResult>((string)((Button)sender).Tag);

            Close();
        }

        #endregion

        #region Private Methods

        private static Window? ActiveOwner()
        {
            return Application.Current?.Windows
                .OfType<Window>()
                .FirstOrDefault(window => window.IsLoaded && window.IsActive);
        }

        private void ShowButtons(MessageBoxButton button)
        {
            var buttons = ButtonsFor(button);

            foreach (var visibleButton in buttons)
                visibleButton.Visibility = Visibility.Visible;

            buttons.First().Focus();
        }

        private Button[] ButtonsFor(MessageBoxButton button)
        {
            return button switch
            {
                MessageBoxButton.OKCancel => new[] { BtnOk, BtnCancel },
                MessageBoxButton.YesNo => new[] { BtnYes, BtnNo },
                MessageBoxButton.YesNoCancel => new[] { BtnYes, BtnNo, BtnCancel },
                _ => new[] { BtnOk }
            };
        }

        #endregion
    }
}