using System.IO;

namespace OfflineChatBot.Services.Models
{
    public static class ModelFileStore
    {
        private const int FirstRetryDelayMs = 150;
        private const int SecondRetryDelayMs = 300;

        public static long GetSizeInBytes(string filePath)
        {
            return File.Exists(filePath) ? new FileInfo(filePath).Length : 0;
        }

        public static async Task<bool> DeleteAsync(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                return true;

            await ReleaseNativeHandlesAsync(FirstRetryDelayMs);

            if (TryDelete(filePath))
                return true;

            await ReleaseNativeHandlesAsync(SecondRetryDelayMs);

            return TryDelete(filePath);
        }

        public static bool TryDelete(string filePath)
        {
            try
            {
                File.Delete(filePath);

                return true;
            }
            catch
            {
                return false;
            }
        }

        #region Private Methods

        private static async Task ReleaseNativeHandlesAsync(int delayMs)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            await Task.Delay(delayMs);
        }

        #endregion
    }
}