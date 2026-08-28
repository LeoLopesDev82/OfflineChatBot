using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OfflineChatBot.Helpers;
using OfflineChatBot.Models;
using OfflineChatBot.Services.Abstractions;
using OfflineChatBot.Services.Chat;
using OfflineChatBot.Services.Documents;
using OfflineChatBot.Services.Llm;
using OfflineChatBot.Services.Models;
using OfflineChatBot.Services.Platform;
using OfflineChatBot.ViewModels;
using OfflineChatBot.Views;
using Serilog;

namespace OfflineChatBot
{
    public partial class App : Application
    {
        private const int RetainedLogFileCount = 7;

        private ServiceProvider? _services;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var configuration = BuildConfiguration();

            ConfigureSerilog(configuration);
            ConfigureNativeBackend(configuration);

            DispatcherUnhandledException += OnDispatcherUnhandledException;

            _services = BuildServiceProvider(configuration);
            _services.GetRequiredService<ILogger<App>>().LogInformation("Application started");

            MainWindow = _services.GetRequiredService<MainWindow>();

            MainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _services?.GetService<ILogger<App>>()?.LogInformation("Application closing");
            _services?.Dispose();

            Log.CloseAndFlush();

            base.OnExit(e);
        }

        #region Event Handlers

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log.Fatal(e.Exception, "Unhandled exception on the UI thread");
        }

        #endregion

        #region Private Methods

        private static void ConfigureNativeBackend(IConfiguration configuration)
        {
            var useGpu = configuration.GetValue($"{GenerationOptions.SectionName}:UseGpu", true);

            NativeBackend.Configure(useGpu, message => Log.Debug("llama: {Message}", message.TrimEnd()));
        }

        private static IConfiguration BuildConfiguration()
        {
            return new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
        }

        private static void ConfigureSerilog(IConfiguration configuration)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(ReadMinimumLevel(configuration))
                .WriteTo.File(
                    Path.Combine(PathHelper.LogsFolder, "app-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: RetainedLogFileCount,
                    shared: true)
                .CreateLogger();
        }

        private static Serilog.Events.LogEventLevel ReadMinimumLevel(IConfiguration configuration)
        {
            var configured = configuration["Logging:MinimumLevel"];

            return Enum.TryParse<Serilog.Events.LogEventLevel>(configured, ignoreCase: true, out var level)
                ? level
                : Serilog.Events.LogEventLevel.Information;
        }

        private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
        {
            var services = new ServiceCollection();

            services.AddLogging(builder => builder.AddSerilog(dispose: true));
            services.Configure<GenerationOptions>(configuration.GetSection(GenerationOptions.SectionName));

            services.AddSingleton(_ => new HttpClient { Timeout = Timeout.InfiniteTimeSpan });
            services.AddSingleton<ModelFileDownloader>();

            services.AddSingleton<ILlmService, LlamaSharpService>();
            services.AddSingleton<IModelManagerService, ModelManagerService>();
            services.AddSingleton<IChatStorageService, ChatStorageService>();
            services.AddSingleton<IDocumentStore, DocumentStore>();
            services.AddSingleton<WorkbookReader>();
            services.AddSingleton<BlockDetector>();
            services.AddSingleton<SpreadsheetProfiler>();
            services.AddSingleton<SpreadsheetSummary>();
            services.AddSingleton<IDocumentTextExtractor>(provider => new CompositeTextExtractor(
            [
                new PlainTextExtractor(),
                new PdfTextExtractor(),
                new WordTextExtractor(),
                new SpreadsheetTextExtractor(
                    provider.GetRequiredService<WorkbookReader>(),
                    provider.GetRequiredService<BlockDetector>(),
                    provider.GetRequiredService<SpreadsheetSummary>())
            ]));
            services.AddSingleton<TextSplitter>();
            services.AddSingleton<IDocumentReader, DocumentReader>();
            services.AddSingleton<IDocumentScanner, DocumentScanner>();
            services.AddSingleton<QueryRunner>();
            services.AddSingleton<ISpreadsheetQueryService, SpreadsheetQueryService>();
            services.AddSingleton<IQuestionRouter, QuestionRouter>();
            services.AddSingleton<IDialogService, DialogService>();
            services.AddSingleton<IResourceMonitor, ProcessResourceMonitor>();
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();
            services.AddSingleton<ITokenCounter>(provider => (ITokenCounter)provider.GetRequiredService<ILlmService>());

            services.AddSingleton<AppStatusViewModel>();
            services.AddSingleton<ModelManagerViewModel>();
            services.AddSingleton<MainViewModel>();

            services.AddSingleton<MainWindow>();
            services.AddTransient<ModelManagerWindow>();

            return services.BuildServiceProvider();
        }

        #endregion
    }
}