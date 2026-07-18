using Microsoft.Extensions.DependencyInjection;
using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;
using ZVec.NET.Samples.Shared.Rag;
using ZVec.NET.Samples.Shared.Services;

namespace ZVec.NET.Samples.Shared;

/// <summary>DI helpers for sample hosts (not part of the ZVec.NET package).</summary>
public static class SampleServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IZvecCollection{T}"/> with sample-level open-or-create
    /// (upstream create throws if the path already exists).
    /// </summary>
    public static IServiceCollection AddSampleCollectionOpenOrCreate<T>(
        this IServiceCollection services,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        services.AddSingleton<IZvecCollection<T>>(sp =>
        {
            var factory = sp.GetRequiredService<IZvecFactory>();
            return CollectionBootstrap.OpenOrCreate<T>(factory, path, enableMmap);
        });

        return services;
    }

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

        services.AddHttpClient<LmStudioModelCatalog>((sp, client) =>
        {
            var opts = sp.GetRequiredService<LmStudioOptions>();
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        services.AddSingleton<TextChunker>();
        services.AddSingleton<LmStudioStatusProbe>();
        services.AddSingleton<RagIngestService>();
        services.AddSingleton<RagQueryService>();
        services.AddSingleton<RagAskService>();
        services.AddSingleton<SearchIngestService>();
        services.AddSingleton<SearchQueryService>();
        services.AddSingleton<RecommendService>();
        services.AddSingleton<DatasetSeedService>();
        return services;
    }

    /// <summary>Registers RAG + search + recommend collections under a data root (Maui / AspNet).</summary>
    public static IServiceCollection AddSampleDemoCollections(
        this IServiceCollection services,
        string dataRoot,
        bool enableMmap = SampleDefaults.EnableMmap)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(dataRoot);
        Directory.CreateDirectory(dataRoot);

        services.AddSampleCollectionOpenOrCreate<RagDocument>(
            Path.Combine(dataRoot, SampleDefaults.RagCollectionFolder), enableMmap);
        services.AddSampleCollectionOpenOrCreate<SearchDocument>(
            Path.Combine(dataRoot, SampleDefaults.SearchCollectionFolder), enableMmap);
        services.AddSampleCollectionOpenOrCreate<RecommendItem>(
            Path.Combine(dataRoot, SampleDefaults.RecommendCollectionFolder), enableMmap);
        return services;
    }
}
