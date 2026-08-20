using System.IO;

namespace OfflineChatBot.Helpers
{
    public static class PathHelper
    {
        public static string AppDataFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "OfflineChatBot");

        public static string ModelsFolder
        {
            get
            {
                var folder = Path.Combine(AppDataFolder, "Models");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return folder;
            }
        }

        public static string ChatsFolder
        {
            get
            {
                var folder = Path.Combine(AppDataFolder, "Chats");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);
                
                return folder;
            }
        }

        public static string HistoryFilePath => Path.Combine(ChatsFolder, "sessions.json");
    }
}