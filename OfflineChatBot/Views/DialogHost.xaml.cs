using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace OfflineChatBot.Views
{
    public partial class DialogHost : UserControl
    {
        private TaskCompletionSource<MessageBoxResult>? _answer;

        public DialogHost()
        {
            InitializeComponent();
        }

        public Task<MessageBoxResult> AskAsync(string message, string caption, MessageBoxButton button)
        {
            Close(MessageBoxResult.None);

            MessageText.Text = message;
            CaptionText.Text = caption;

            ShowButtons(button);

            Visibility = Visibility.Visible;

            _answer = new TaskCompletionSource<MessageBoxResult>(TaskCreationOptions.RunContinuationsAsynchronously);

            ButtonsFor(button).Last().Focus();

            return _answer.Task;
        }

        #region Event Handlers

        private void Answer_Click(object sender, RoutedEventArgs e)
        {
            Close(Enum.Parse<MessageBoxResult>((string)((Button)sender).Tag));
        }

        private void Host_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape || _answer == null)
                return;

            Close(WayOut());

            e.Handled = true;
        }

        #endregion

        #region Private Methods

        private MessageBoxResult WayOut()
        {
            if (CancelButton.Visibility == Visibility.Visible)
                return MessageBoxResult.Cancel;

            return NoButton.Visibility == Visibility.Visible ? MessageBoxResult.No : MessageBoxResult.OK;
        }

        private void Close(MessageBoxResult result)
        {
            Visibility = Visibility.Collapsed;

            var pending = _answer;

            _answer = null;

            pending?.TrySetResult(result);
        }

        private void ShowButtons(MessageBoxButton button)
        {
            OkButton.Visibility = Visibility.Collapsed;
            YesButton.Visibility = Visibility.Collapsed;
            NoButton.Visibility = Visibility.Collapsed;
            CancelButton.Visibility = Visibility.Collapsed;

            foreach (var visible in ButtonsFor(button))
                visible.Visibility = Visibility.Visible;
        }

        private Button[] ButtonsFor(MessageBoxButton button)
        {
            return button switch
            {
                MessageBoxButton.OKCancel => [CancelButton, OkButton],
                MessageBoxButton.YesNo => [NoButton, YesButton],
                MessageBoxButton.YesNoCancel => [CancelButton, NoButton, YesButton],
                _ => [OkButton]
            };
        }

        #endregion
    }
}
