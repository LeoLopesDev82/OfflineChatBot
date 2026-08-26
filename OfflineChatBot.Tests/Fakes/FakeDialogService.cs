using OfflineChatBot.Services.Abstractions;

namespace OfflineChatBot.Tests.Fakes
{
    public sealed class FakeDialogService : IDialogService
    {
        public bool ConfirmResult { get; set; } = true;
        public string? PickedImage { get; set; }
        public string? PickedDocument { get; set; }

        public List<string> Information { get; } = new List<string>();
        public int ConfirmCount { get; private set; }
        public int ModelManagerCount { get; private set; }

        public void ShowInformation(string message, string caption)
        {
            Information.Add(message);
        }

        public bool Confirm(string message, string caption)
        {
            ConfirmCount++;

            return ConfirmResult;
        }

        public string? PickImageFile()
        {
            return PickedImage;
        }

        public string? PickDocumentFile()
        {
            return PickedDocument;
        }

        public void ShowModelManager()
        {
            ModelManagerCount++;
        }
    }
}
