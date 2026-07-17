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
    {
        Directory.CreateDirectory(path);
        var options = new ZVecCollectionOptions { EnableMmap = enableMmap };
        if (Directory.EnumerateFileSystemEntries(path).Any())
        {
            var opened = factory.Open(path, options);
            return new ZVecCollection<RagDocument>(opened);
        }

        var schema = ZVecCollectionSchemaBuilder.From<RagDocument>().Build();
        var created = factory.CreateAndOpen(path, schema, options);
        return new ZVecCollection<RagDocument>(created);
    }

    public static IZvecCollection<RecommendItem> OpenRecommend(
        IZvecFactory factory,
        string path,
        bool enableMmap = SampleDefaults.EnableMmap)
    {
        Directory.CreateDirectory(path);
        var options = new ZVecCollectionOptions { EnableMmap = enableMmap };
        if (Directory.EnumerateFileSystemEntries(path).Any())
        {
            var opened = factory.Open(path, options);
            return new ZVecCollection<RecommendItem>(opened);
        }

        var schema = ZVecCollectionSchemaBuilder.From<RecommendItem>().Build();
        var created = factory.CreateAndOpen(path, schema, options);
        return new ZVecCollection<RecommendItem>(created);
    }
}
