using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OfflineChatBot.Views
{
    public partial class CodeBlockControl : UserControl
    {
        private static readonly TimeSpan CopiedFeedbackDuration = TimeSpan.FromSeconds(2);

        public static readonly DependencyProperty CodeLanguageProperty =
            DependencyProperty.Register(nameof(CodeLanguage), typeof(string), typeof(CodeBlockControl), new PropertyMetadata("code"));

        public static readonly DependencyProperty CodeProperty =
            DependencyProperty.Register(nameof(Code), typeof(string), typeof(CodeBlockControl), new PropertyMetadata(string.Empty));

        public CodeBlockControl()
        {
            InitializeComponent();
        }

        public string CodeLanguage
        {
            get => (string)GetValue(CodeLanguageProperty);
            set => SetValue(CodeLanguageProperty, value);
        }

        public string Code
        {
            get => (string)GetValue(CodeProperty);
            set => SetValue(CodeProperty, value);
        }

        #region Event Handlers

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(Code))
                return;

            Clipboard.SetText(Code);

            ShowCopiedFeedback(sender as Button);
        }

        #endregion

        #region Private Methods

        private static void ShowCopiedFeedback(Button? button)
        {
            if (button == null)
                return;

            var originalContent = button.Content;
            var timer = new DispatcherTimer { Interval = CopiedFeedbackDuration };

            button.Content = "Copied!";

            timer.Tick += (_, _) =>
            {
                button.Content = originalContent;

                timer.Stop();
            };

            timer.Start();
        }

        #endregion
    }
}