using System.Globalization;
using System.Text.RegularExpressions;
using LLama.Native;
using OfflineChatBot.Models;

namespace OfflineChatBot.Services.Llm
{
    public static class NativeBackend
    {
        private static readonly Regex DeviceRegex = new Regex(@"using device \w+ \((?<name>[^)]+)\)", RegexOptions.Compiled);
        private static readonly Regex OffloadRegex = new Regex(@"offloaded (?<done>\d+)/(?<total>\d+) layers to GPU", RegexOptions.Compiled);
        private static readonly Regex BufferRegex = new Regex(@"(?<device>[A-Za-z]+\d+) +\w* *buffer size *= *(?<size>[\d.]+) MiB", RegexOptions.Compiled);

        private static string _device = string.Empty;
        private static int _offloadedLayers;
        private static int _totalLayers;
        private static double _videoMemory;

        public static BackendStatus Current => _offloadedLayers > 0
            ? new BackendStatus(_device, _offloadedLayers, _totalLayers, _videoMemory)
            : BackendStatus.Cpu;

        public static void Configure(bool preferGpu, Action<string> onNativeLog)
        {
            NativeLibraryConfig.All
                .WithVulkan(preferGpu)
                .WithCuda(preferGpu)
                .WithAutoFallback(true)
                .WithLogCallback((_, message) => Observe(message, onNativeLog));
        }

        public static void BeginLoad()
        {
            _offloadedLayers = 0;
            _totalLayers = 0;
            _videoMemory = 0;
        }

        #region Private Methods

        private static void Observe(string message, Action<string> onNativeLog)
        {
            onNativeLog(message);

            ReadDevice(message);
            ReadOffloadedLayers(message);
            ReadVideoMemory(message);
        }

        private static void ReadDevice(string message)
        {
            var match = DeviceRegex.Match(message);

            if (!match.Success)
                return;

            _device = match.Groups["name"].Value;
        }

        private static void ReadOffloadedLayers(string message)
        {
            var match = OffloadRegex.Match(message);

            if (!match.Success)
                return;

            _offloadedLayers = int.Parse(match.Groups["done"].Value);
            _totalLayers = int.Parse(match.Groups["total"].Value);
        }

        private static void ReadVideoMemory(string message)
        {
            var match = BufferRegex.Match(message);

            if (!match.Success || match.Groups["device"].Value.Contains("CPU"))
                return;

            _videoMemory += double.Parse(match.Groups["size"].Value, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}