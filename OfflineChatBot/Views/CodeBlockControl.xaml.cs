using System.Windows;
using System.Windows.Controls;

namespace OfflineChatBot.Views
{
    public partial class CodeBlockControl : UserControl
    {
        public static readonly DependencyProperty CodeLanguageProperty =
            DependencyProperty.Register(nameof(CodeLanguage), typeof(string), typeof(CodeBlockControl), new PropertyMetadata("code"));

        public static readonly DependencyProperty CodeProperty =
            DependencyProperty.Register(nameof(Code), typeof(string), typeof(CodeBlockControl), new PropertyMetadata(string.Empty));

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

        public CodeBlockControl()
        {
            InitializeComponent();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Code))
            {
                Clipboard.SetText(Code);
                if (sender is Button btn)
                {
                    var originalText = btn.Content;
                    btn.Content = "Copied!";
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = System.TimeSpan.FromSeconds(2)
                    };
                    timer.Tick += (s, args) =>
                    {
                        btn.Content = originalText;
                        timer.Stop();
                    };
                    timer.Start();
                }
            }
        }
    }
}
