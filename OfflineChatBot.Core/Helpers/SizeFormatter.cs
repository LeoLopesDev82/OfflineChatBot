using System.Globalization;

namespace OfflineChatBot.Helpers
{
    public static class SizeFormatter
    {
        private const double BytesPerMegabyte = 1024.0 * 1024.0;

        public static double ToMegabytes(long bytes)
        {
            return bytes / BytesPerMegabyte;
        }

        public static string FromMegabytes(double megabytes)
        {
            if (megabytes >= 1024)
                return string.Format(CultureInfo.InvariantCulture, "{0:F2} GB", megabytes / 1024.0);

            return string.Format(CultureInfo.InvariantCulture, "{0:F0} MB", megabytes);
        }

        public static string FromBytes(long bytes)
        {
            return FromMegabytes(ToMegabytes(bytes));
        }
    }
}