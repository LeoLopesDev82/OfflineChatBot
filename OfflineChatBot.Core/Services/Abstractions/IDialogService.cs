namespace OfflineChatBot.Services.Abstractions
{
    public interface IDialogService
    {
        void ShowInformation(string message, string caption);
        bool Confirm(string message, string caption);
        string? PickImageFile();
        string? PickDocumentFile();
        void ShowModelManager();
    }
}