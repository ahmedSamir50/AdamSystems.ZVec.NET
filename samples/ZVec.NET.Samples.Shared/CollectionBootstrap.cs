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
    /// App-level open-or-create (upstream has no open_or_create — create throws if path exists).
    /// Only creates the parent directory; never pre-creates an empty collection path.
    /// </summary>
    public static IZvecCollection<T> OpenOrCreate<T>(
        IZvecFactory factory,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var options = new ZVecCollectionOptions { EnableMmap = enableMmap };
        if (Directory.Exists(path) && Directory.EnumerateFileSystemEntries(path).Any())
        {
            var opened = factory.Open(path, options);
            return new ZVecCollection<T>(opened);
        }

        // Remove empty dir left by a previous failed create attempt.
        if (Directory.Exists(path) && !Directory.EnumerateFileSystemEntries(path).Any())
        {
            try { Directory.Delete(path); } catch { /* best effort */ }
        }

        var schema = ZVecCollectionSchemaBuilder.From<T>().Build();
        var created = factory.CreateAndOpen(path, schema, options);
        return new ZVecCollection<T>(created);
    }
}
