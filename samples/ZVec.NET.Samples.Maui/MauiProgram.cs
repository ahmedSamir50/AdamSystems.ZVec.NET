using Microsoft.Extensions.Logging;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Samples.Shared;
using ZVec.NET.Samples.Shared.Data;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            });

        builder.Services.AddMauiBlazorWebView();

        var dataRoot = Path.Combine(FileSystem.AppDataDirectory, "zvec");
        Directory.CreateDirectory(dataRoot);
        var ragPath = Path.Combine(dataRoot, SampleDefaults.RagCollectionFolder);

        // Offline / edge: mmap collection under AppData. Host lifetime disposes factory on shutdown.
        builder.Services.AddZVec(options =>
        {
            options.LogLevel = ZVecLogLevel.Warn;
            options.MemoryLimitMb = SampleDefaults.MemoryLimitMb;
            options.QueryThreads = -1;
        });

        builder.Services.AddZVecCollection<RagDocument>(options =>
        {
            options.Path = ragPath;
            options.EnableMmap = SampleDefaults.EnableMmap;
            options.Create = true;
        });

        builder.Services.AddZVecSampleAi();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        // Fire-and-forget T1 packs into samples/datasets/cache/ (skip if already present).
        _ = SampleDatasetBootstrap.StartBackgroundEnsureAsync();
        return app;
    }
}
