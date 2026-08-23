using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace OfflineChatBot.Helpers
{
    public static class MarkdownHelper
    {
        private static readonly Regex TokenRegex = new Regex(@"(`[^`]+`|\*\*[^*]+\*\*|\*[^*]+\*)", RegexOptions.Compiled);
        private static readonly Regex NumberedListRegex = new Regex(@"^\d+\.\s", RegexOptions.Compiled);

        public static readonly DependencyProperty MarkdownTextProperty =
            DependencyProperty.RegisterAttached(
                "MarkdownText",
                typeof(string),
                typeof(MarkdownHelper),
                new PropertyMetadata(string.Empty, OnMarkdownTextChanged));

        public static string GetMarkdownText(DependencyObject obj)
        {
            return (string)obj.GetValue(MarkdownTextProperty);
        }

        public static void SetMarkdownText(DependencyObject obj, string value)
        {
            obj.SetValue(MarkdownTextProperty, value);
        }

        #region Private Methods

        private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not TextBlock textBlock) return;

            var rawText = e.NewValue as string ?? string.Empty;

            textBlock.Inlines.Clear();

            if (string.IsNullOrEmpty(rawText)) return;

            ProcessMarkdownLines(rawText, textBlock);
        }

        private static void ProcessMarkdownLines(string rawText, TextBlock textBlock)
        {
            var lines = rawText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                if (IsHeaderLine(line))
                    ProcessHeaderLine(line, textBlock);
                else if (IsUnorderedListLine(line))
                    ProcessUnorderedListLine(line, textBlock);
                else if (IsNumberedListLine(line))
                    ProcessNumberedListLine(line, textBlock);
                else
                    ProcessRegularLine(line, textBlock);

                if (i < lines.Length - 1)
                    textBlock.Inlines.Add(new LineBreak());
            }
        }

        private static bool IsHeaderLine(string line)
        {
            return line.StartsWith("### ") || line.StartsWith("#### ") || line.StartsWith("## ") || line.StartsWith("# ");
        }

        private static void ProcessHeaderLine(string line, TextBlock textBlock)
        {
            var headerText = line.TrimStart('#').Trim();

            var headerSpan = new Span
            {
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)Application.Current.Resources["AccentBrush"]
            };

            ParseInlineFormattedText(headerText, headerSpan);

            textBlock.Inlines.Add(headerSpan);
        }

        private static bool IsUnorderedListLine(string line)
        {
            return line.StartsWith("- ") || line.StartsWith("* ");
        }

        private static void ProcessUnorderedListLine(string line, TextBlock textBlock)
        {
            var listText = "  •  " + line.Substring(2).Trim();
            var listSpan = new Span();

            ParseInlineFormattedText(listText, listSpan);

            textBlock.Inlines.Add(listSpan);
        }

        private static bool IsNumberedListLine(string line)
        {
            return NumberedListRegex.IsMatch(line);
        }

        private static void ProcessNumberedListLine(string line, TextBlock textBlock)
        {
            var listSpan = new Span();

            ParseInlineFormattedText("  " + line.Trim(), listSpan);

            textBlock.Inlines.Add(listSpan);
        }

        private static void ProcessRegularLine(string line, TextBlock textBlock)
        {
            var lineSpan = new Span();

            ParseInlineFormattedText(line, lineSpan);

            textBlock.Inlines.Add(lineSpan);
        }

        private static void ParseInlineFormattedText(string text, Span targetSpan)
        {
            var parts = TokenRegex.Split(text);

            var accentBrush = (Brush)Application.Current.Resources["AccentBrush"];
            var codeBgBrush = (Brush)Application.Current.Resources["CodeBgBrush"];
            var textPrimaryBrush = (Brush)Application.Current.Resources["TextPrimaryBrush"];

            foreach (var part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;

                if (IsCodeBlock(part))
                    ProcessCodeBlock(part, targetSpan, codeBgBrush, accentBrush);
                else if (IsBoldText(part))
                    ProcessBoldText(part, targetSpan, textPrimaryBrush);
                else if (IsItalicText(part))
                    ProcessItalicText(part, targetSpan, textPrimaryBrush);
                else
                    ProcessRegularText(part, targetSpan, textPrimaryBrush);
            }
        }

        private static bool IsCodeBlock(string part)
        {
            return part.StartsWith("`") && part.EndsWith("`") && part.Length >= 2;
        }

        private static void ProcessCodeBlock(string part, Span targetSpan, Brush codeBgBrush, Brush accentBrush)
        {
            var codeContent = part.Substring(1, part.Length - 2);

            var border = new Border
            {
                Background = codeBgBrush,
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(5, 1, 5, 1),
                Margin = new Thickness(2, 0, 2, 0)
            };

            var txt = new TextBlock
            {
                Text = codeContent,
                FontFamily = new FontFamily("Consolas, Cascadia Code, Courier New"),
                Foreground = accentBrush,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            };

            border.Child = txt;

            targetSpan.Inlines.Add(new InlineUIContainer(border) { BaselineAlignment = BaselineAlignment.Center });
        }

        private static bool IsBoldText(string part)
        {
            return part.StartsWith("**") && part.EndsWith("**") && part.Length >= 4;
        }

        private static void ProcessBoldText(string part, Span targetSpan, Brush textPrimaryBrush)
        {
            var boldContent = part.Substring(2, part.Length - 4);

            targetSpan.Inlines.Add(new Run(boldContent)
            {
                FontWeight = FontWeights.Bold,
                Foreground = textPrimaryBrush
            });
        }

        private static bool IsItalicText(string part)
        {
            return part.StartsWith("*") && part.EndsWith("*") && part.Length >= 2;
        }

        private static void ProcessItalicText(string part, Span targetSpan, Brush textPrimaryBrush)
        {
            var italicContent = part.Substring(1, part.Length - 2);

            targetSpan.Inlines.Add(new Run(italicContent)
            {
                FontStyle = FontStyles.Italic,
                Foreground = textPrimaryBrush
            });
        }

        private static void ProcessRegularText(string part, Span targetSpan, Brush textPrimaryBrush)
        {
            targetSpan.Inlines.Add(new Run(part)
            {
                Foreground = textPrimaryBrush
            });
        }

        #endregion
    }
}