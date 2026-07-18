using Microsoft.Extensions.Logging;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Samples.Shared;
using ZVec.NET.Samples.Shared.Data;

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

        builder.Services.AddZVec(options =>
        {
            options.LogLevel = ZVecLogLevel.Warn;
            options.MemoryLimitMb = SampleDefaults.MemoryLimitMb;
            options.QueryThreads = -1;
        });

        builder.Services.AddSampleDemoCollections(dataRoot, SampleDefaults.EnableMmap);
        builder.Services.AddZVecSampleAi();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        var app = builder.Build();
        _ = SampleDatasetBootstrap.StartBackgroundEnsureAsync();
        return app;
    }
}
