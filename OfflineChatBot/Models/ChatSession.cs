using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace OfflineChatBot.Models
{
    public partial class ChatSession : ObservableObject
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [ObservableProperty]
        private string _title = "New Chat";

        [ObservableProperty]
        [System.Text.Json.Serialization.JsonIgnore]
        private bool _isEditing;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ObservableCollection<ChatMessage> Messages { get; set; } = new ObservableCollection<ChatMessage>();
    }
}