namespace OfflineChatBot.Services.Abstractions
{
    public interface IDialogService
    {
        Task ShowInformationAsync(string message, string caption);
        Task<bool> ConfirmAsync(string message, string caption);
        string? PickImageFile();
        string? PickDocumentFile();
        void ShowModelManager();
    }
}
