using System.Text.RegularExpressions;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OfflineChatBot.Models
{
    public enum MessageSender
    {
        User,
        Assistant,
        System
    }

    public class ChatMessageSegment
    {
        public bool IsCode { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Language { get; set; } = "code";
        public string Code { get; set; } = string.Empty;
    }

    public partial class ChatMessage : ObservableObject
    {
        #region Fields

        private static readonly Regex CodeBlockRegex = new Regex(@"```(?:language:)?([a-zA-Z0-9_#+\-]*)\r?\n([\s\S]*?)```", RegexOptions.Compiled);
        private DispatcherTimer? _thinkingTimer;
        private List<ChatMessageSegment>? _cachedSegments;

        [ObservableProperty]
        private string _content = string.Empty;

        [ObservableProperty]
        private bool _isStreaming;

        [ObservableProperty]
        private string _thinkingDots = ".";

        #endregion

        #region Properties

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public MessageSender Sender { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        public bool IsUser => Sender == MessageSender.User;
        public bool IsAssistant => Sender == MessageSender.Assistant;
        public bool IsSystem => Sender == MessageSender.System;

        public bool IsThinking => IsAssistant && IsStreaming && string.IsNullOrWhiteSpace(Content);

        public List<ChatMessageSegment> Segments
        {
            get
            {
                if (_cachedSegments == null)
                {
                    _cachedSegments = ParseSegments();
                }

                return _cachedSegments;
            }
        }

        #endregion

        #region MVVM Interceptors

        partial void OnContentChanged(string value)
        {
            InvalidateCacheAndUpdateState();
        }

        partial void OnIsStreamingChanged(bool value)
        {
            InvalidateCacheAndUpdateState();
        }

        #endregion

        #region Private Methods

        private void InvalidateCacheAndUpdateState()
        {
            _cachedSegments = null;

            OnPropertyChanged(nameof(IsThinking));
            OnPropertyChanged(nameof(Segments));
            UpdateThinkingState();
        }

        private void UpdateThinkingState()
        {
            if (!IsThinking)
            {
                StopThinkingTimer();

                return;
            }

            StartThinkingTimer();
        }

        private void StartThinkingTimer()
        {
            if (_thinkingTimer != null) return;

            _thinkingTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            
            int count = 1;

            _thinkingTimer.Tick += (s, e) =>
            {
                count = (count % 3) + 1;

                ThinkingDots = new string('.', count);
            };

            _thinkingTimer.Start();
        }

        private void StopThinkingTimer()
        {
            _thinkingTimer?.Stop();
            _thinkingTimer = null;
        }

        private List<ChatMessageSegment> ParseSegments()
        {
            var segments = new List<ChatMessageSegment>();
            
            if (string.IsNullOrWhiteSpace(Content)) return segments;

            var matches = CodeBlockRegex.Matches(Content);

            int lastIndex = 0;

            foreach (Match match in matches)
            {
                ExtractTextSegment(segments, lastIndex, match.Index);
                ExtractCodeSegment(segments, match);

                lastIndex = match.Index + match.Length;
            }

            ExtractRemainingTextSegment(segments, lastIndex);

            return segments;
        }

        private void ExtractTextSegment(List<ChatMessageSegment> segments, int startIndex, int endIndex)
        {
            if (endIndex <= startIndex) return;

            var textPart = Content.Substring(startIndex, endIndex - startIndex).Trim();

            if (string.IsNullOrEmpty(textPart)) return;

            segments.Add(new ChatMessageSegment
            {
                IsCode = false,
                Text = textPart
            });
        }

        private void ExtractCodeSegment(List<ChatMessageSegment> segments, Match match)
        {
            var lang = CleanLanguageString(match.Groups[1].Value);
            var codePart = match.Groups[2].Value.TrimEnd();

            segments.Add(new ChatMessageSegment
            {
                IsCode = true,
                Language = lang,
                Code = codePart
            });
        }

        private string CleanLanguageString(string rawLang)
        {
            if (string.IsNullOrWhiteSpace(rawLang)) return "code";
            
            return rawLang.Replace("language:", "").Replace("lang-", "").Trim();
        }

        private void ExtractRemainingTextSegment(List<ChatMessageSegment> segments, int startIndex)
        {
            if (startIndex >= Content.Length) return;

            var remaining = Content.Substring(startIndex).Trim();

            if (string.IsNullOrEmpty(remaining)) return;

            segments.Add(new ChatMessageSegment
            {
                IsCode = false,
                Text = remaining
            });
        }

        #endregion
    }
}