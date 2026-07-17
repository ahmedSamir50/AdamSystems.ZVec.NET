using Microsoft.Extensions.DependencyInjection;
using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Rag;
using ZVec.NET.Samples.Shared.Services;

namespace ZVec.NET.Samples.Shared;

/// <summary>DI helpers for sample hosts (not part of the ZVec.NET package).</summary>
public static class SampleServiceCollectionExtensions
{
    public static IServiceCollection AddZVecSampleAi(
        this IServiceCollection services,
        Action<LmStudioOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new LmStudioOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);

        services.AddHttpClient<IEmbeddingClient, LmStudioEmbeddingClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<LmStudioOptions>();
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddHttpClient<IChatClient, LmStudioChatClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<LmStudioOptions>();
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        services.AddSingleton<TextChunker>();
        services.AddSingleton<LmStudioStatusProbe>();
        services.AddSingleton<RagIngestService>();
        services.AddSingleton<RagQueryService>();
        services.AddSingleton<RagAskService>();
        services.AddSingleton<RecommendService>();
        return services;
    }
}
