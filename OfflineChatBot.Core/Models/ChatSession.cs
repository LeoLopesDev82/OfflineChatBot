using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OfflineChatBot.Models
{
    public partial class ChatSession : ObservableObject
    {
        public const string DefaultTitle = "New Chat";

        private const int TitleMaxLength = 30;

        [ObservableProperty]
        private string _title = DefaultTitle;

        [ObservableProperty]
        [property: JsonIgnore]
        private bool _isEditing;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string? DocumentName { get; set; }
        public string? DocumentPath { get; set; }

        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();

        [JsonIgnore]
        public bool HasDefaultTitle => Messages.Count == 0 || Title == DefaultTitle;

        public void RenameFromPrompt(string prompt)
        {
            if (!HasDefaultTitle)
                return;

            Title = prompt.Length > TitleMaxLength ? prompt.Substring(0, TitleMaxLength) + "..." : prompt;
        }

        public ChatMessage AddUserMessage(string content, string? imagePath, string? documentName)
        {
            return AddMessage(new ChatMessage
            {
                Sender = MessageSender.User,
                Content = content,
                AttachedImagePath = imagePath,
                AttachedDocumentName = documentName
            });
        }

        public ChatMessage AddStreamingAssistantMessage()
        {
            return AddMessage(new ChatMessage
            {
                Sender = MessageSender.Assistant,
                Content = string.Empty,
                IsStreaming = true
            });
        }

        #region Private Methods

        private ChatMessage AddMessage(ChatMessage message)
        {
            Messages.Add(message);

            return message;
        }

        #endregion
    }
}