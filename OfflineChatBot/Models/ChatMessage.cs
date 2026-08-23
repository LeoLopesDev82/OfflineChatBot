using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OfflineChatBot.Models
{
    public partial class ChatMessage : ObservableObject
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsThinking))]
        private string _content = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsThinking))]
        [property: JsonIgnore]
        private bool _isStreaming;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasImage))]
        private string? _attachedImagePath;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public MessageSender Sender { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;

        [JsonIgnore]
        public bool IsUser => Sender == MessageSender.User;

        [JsonIgnore]
        public bool IsAssistant => Sender == MessageSender.Assistant;

        [JsonIgnore]
        public bool IsSystem => Sender == MessageSender.System;

        [JsonIgnore]
        public bool HasImage => !string.IsNullOrEmpty(AttachedImagePath);

        [JsonIgnore]
        public bool IsThinking => IsAssistant && IsStreaming && string.IsNullOrWhiteSpace(Content);
    }
}