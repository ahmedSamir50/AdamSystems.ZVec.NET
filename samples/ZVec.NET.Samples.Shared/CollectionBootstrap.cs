using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared;

/// <summary>Opens typed sample collections (create-if-missing) for console / hosts.</summary>
public static class CollectionBootstrap
{
    public static IZvecFactory CreateFactory(int memoryLimitMb = SampleDefaults.MemoryLimitMb)
    {
        var factory = new ZVecFactory();
        factory.Initialize(new ZVecOptions
        {
            LogLevel = ZVecLogLevel.Warn,
            MemoryLimitMb = memoryLimitMb,
            QueryThreads = -1
        });
        return factory;
    }

    public static IZvecCollection<RagDocument> OpenRag(
        IZvecFactory factory,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
        => OpenOrCreate<RagDocument>(factory, path, enableMmap);

    public static IZvecCollection<SearchDocument> OpenSearch(
        IZvecFactory factory,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
        => OpenOrCreate<SearchDocument>(factory, path, enableMmap);

    public static IZvecCollection<RecommendItem> OpenRecommend(
        IZvecFactory factory,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
        => OpenOrCreate<RecommendItem>(factory, path, enableMmap);

    /// <summary>
    /// Typed wrapper around <see cref="IZvecFactory.OpenOrCreate"/>.
    /// </summary>
    public static IZvecCollection<T> OpenOrCreate<T>(
        IZvecFactory factory,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var options = new ZVecCollectionOptions { EnableMmap = enableMmap };
        var schema = ZVecCollectionSchemaBuilder.From<T>().Build();
        var untyped = factory.OpenOrCreate(path, schema, options);
        return new ZVecCollection<T>(untyped);
    }
}
