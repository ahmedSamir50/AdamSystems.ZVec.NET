using ZVec.NET;
using ZVec.NET.Mapping;
using ZVec.NET.Query;
using ZVec.NET.Samples.Shared;
using ZVec.NET.Samples.Shared.Data;
using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;
using ZVec.NET.Samples.Shared.Rag;
using ZVec.NET.Samples.Shared.Services;

if (args.Length == 0)
{
    PrintHelp();
    return 0;
}

var command = args[0].ToLowerInvariant();
var datasetProgress = new Progress<string>(msg => Console.WriteLine($"[datasets] {msg}"));

if (command is "ingest" or "search" or "recommend")
{
    try
    {
        await SampleDatasetBootstrap.EnsureAllAsync(datasetProgress);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[datasets] ensure failed: {ex.Message}. T0 fixtures still work.");
    }
}
else if (command is not ("help" or "-h" or "--help"))
{
    _ = SampleDatasetBootstrap.StartBackgroundEnsureAsync();
}

return command switch
{
    "basics" => await RunBasicsAsync(),
    "ingest" => await RunIngestAsync(args),
    "ask" => await RunAskAsync(args),
    "search" => await RunSearchAsync(args),
    "recommend" => await RunRecommendAsync(args),
    "help" or "-h" or "--help" => PrintHelp(),
    _ => Unknown(command)
};

static int PrintHelp()
{
    Console.WriteLine("""
        ZVec.NET.Samples.Console (.NET 10) — offline RAG / search / recommend

        Commands:
          basics                         Typed ODM + ZVecDoc vignette (synthetic vectors, no LM Studio)
          ingest [--fixtures|--file path]  Embed + upsert into RAG collection (needs LM Studio embeddings)
          ask "question"                 Retrieve + Gemma 4 answer (needs LM Studio)
          search "query"                 Semantic search over RAG/search collection
          recommend "query"              Similar items (fixtures or MovieLens cache)

        Prerequisites:
          - Native zvec_c_api for win-x64 in SDK runtimes (see repo Appendix C)
          - LM Studio at http://127.0.0.1:1234/v1 with BOTH models loaded concurrently:
              embed: text-embedding-google_embeddinggemma-300m-qat
              chat:  google/gemma-4-e2b
          - T1 datasets download into samples/datasets/cache/ on startup (skip if present)
          - Target framework: net10.0 only
        """);
    return 0;
}

static int Unknown(string command)
{
    Console.Error.WriteLine($"Unknown command: {command}");
    PrintHelp();
    return 1;
}

static async Task<int> RunBasicsAsync()
{
    var path = SamplePaths.CollectionPath("basics-demo");
    if (Directory.Exists(path))
    {
        try { Directory.Delete(path, recursive: true); } catch { /* best effort */ }
    }

    using var factory = CollectionBootstrap.CreateFactory();
    var schema = ZVecCollectionSchemaBuilder.From<DemoDoc>().Build();
    using var untyped = factory.CreateAndOpen(path, schema);
    using IZvecCollection<DemoDoc> typed = new ZVecCollection<DemoDoc>(untyped);

    var vec = SyntheticVector(1);
    typed.Insert(new DemoDoc
    {
        Id = "d1",
        Title = "Hello ZVec",
        Category = "demo",
        Embedding = vec
    });

    var hits = typed.Query(d => d.Embedding, vec, topK: 3, filter: d => d.Category == "demo");
    Console.WriteLine("Typed ODM hits:");
    foreach (var hit in hits)
        Console.WriteLine($"  {hit.Record.Id} {hit.Record.Title} score={hit.Score:F4}");

    // Dynamic escape hatch
    untyped.Insert(ZVecDoc.Create(
        "dyn-1",
        denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["Embedding"] = SyntheticVector(2) },
        fields: new Dictionary<string, object> { ["Title"] = "Dynamic doc", ["Category"] = "demo" }));

    var dyn = untyped.Query(
        new ZVecQuery { FieldName = "Embedding", Vector = SyntheticVector(2) },
        topk: 3,
        filter: ZVecFilterBuilder.Create().Where("Category", ZVecCompareOp.Eq, "demo"));
    Console.WriteLine("Dynamic ZVecDoc hits:");
    foreach (var doc in dyn)
        Console.WriteLine($"  {doc.Id} score={doc.Score:F4}");

    Console.WriteLine("basics OK (no LM Studio required).");
    await Task.CompletedTask;
    return 0;
}

static async Task<int> RunIngestAsync(string[] args)
{
    using var factory = CollectionBootstrap.CreateFactory();
    var ragPath = SamplePaths.CollectionPath(SampleDefaults.RagCollectionFolder);
    using var collection = CollectionBootstrap.OpenRag(factory, ragPath);
    using var http = CreateHttp();
    var opts = new LmStudioOptions();
    IEmbeddingClient embeddings = new LmStudioEmbeddingClient(http, opts);
    var ingest = new RagIngestService(collection, embeddings);
    var progress = new Progress<string>(Console.WriteLine);

    if (args.Any(a => a.Equals("--fixtures", StringComparison.OrdinalIgnoreCase)) || args.Length == 1)
    {
        var fixtures = FixtureLoader.LoadRagFixtures();
        if (fixtures.Count == 0)
        {
            Console.Error.WriteLine("No rag fixtures found under samples/datasets/fixtures/rag.");
            return 1;
        }

        foreach (var fx in fixtures)
        {
            var result = await ingest.IngestTextAsync(fx.Title, fx.Source, fx.Body, fx.Tags, progress);
            Console.WriteLine($"Ingested {result.ChunkCount} chunk(s) from {result.Source}");
        }

        return 0;
    }

    var fileIdx = Array.FindIndex(args, a => a.Equals("--file", StringComparison.OrdinalIgnoreCase));
    if (fileIdx >= 0 && fileIdx + 1 < args.Length)
    {
        var result = await ingest.IngestFileAsync(args[fileIdx + 1], progress: progress);
        Console.WriteLine($"Ingested {result.ChunkCount} chunk(s) from {result.Source}");
        return 0;
    }

    Console.Error.WriteLine("Usage: ingest [--fixtures] | ingest --file path");
    return 1;
}

static async Task<int> RunAskAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: ask \"your question\"");
        return 1;
    }

    var question = string.Join(' ', args.Skip(1));
    using var factory = CollectionBootstrap.CreateFactory();
    using var collection = CollectionBootstrap.OpenRag(factory, SamplePaths.CollectionPath(SampleDefaults.RagCollectionFolder));
    using var httpEmbed = CreateHttp();
    using var httpChat = CreateHttp();
    var opts = new LmStudioOptions();
    var embeddings = new LmStudioEmbeddingClient(httpEmbed, opts);
    var chat = new LmStudioChatClient(httpChat, opts);
    var ask = new RagAskService(new RagQueryService(collection, embeddings), chat);

    var result = await ask.AskAsync(question);
    Console.WriteLine(result.UsedChat ? "Answer (Gemma):" : "Answer (retrieve-only):");
    Console.WriteLine(result.Answer);
    Console.WriteLine();
    Console.WriteLine("Citations:");
    foreach (var c in result.Citations)
        Console.WriteLine($"  [{c.Id}] {c.Title} ({c.Score:F4}) — {c.Snippet}");
    return 0;
}

static async Task<int> RunSearchAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: search \"query\"");
        return 1;
    }

    var query = string.Join(' ', args.Skip(1));
    using var factory = CollectionBootstrap.CreateFactory();
    using var collection = CollectionBootstrap.OpenRag(factory, SamplePaths.CollectionPath(SampleDefaults.RagCollectionFolder));
    using var http = CreateHttp();
    var embeddings = new LmStudioEmbeddingClient(http, new LmStudioOptions());
    var search = new RagQueryService(collection, embeddings);
    var hits = await search.QueryAsync(query);
    foreach (var h in hits)
        Console.WriteLine($"{h.Score:F4}  {h.Title}  {h.Snippet}");
    return 0;
}

static async Task<int> RunRecommendAsync(string[] args)
{
    if (args.Length < 2)
    {
        Console.Error.WriteLine("Usage: recommend \"sci-fi mind-bending\"");
        return 1;
    }

    var query = string.Join(' ', args.Skip(1));
    using var factory = CollectionBootstrap.CreateFactory();
    using var collection = CollectionBootstrap.OpenRecommend(
        factory,
        SamplePaths.CollectionPath(SampleDefaults.RecommendCollectionFolder));
    using var http = CreateHttp();
    var embeddings = new LmStudioEmbeddingClient(http, new LmStudioOptions());
    var recommend = new RecommendService(collection, embeddings);

    if (collection.Stats.DocCount == 0)
    {
        var items = FixtureLoader.LoadRecommendFixtures();
        Console.WriteLine($"Seeding {items.Count} fixture recommend items…");
        await recommend.UpsertItemsAsync(items, new Progress<string>(Console.WriteLine));
    }

    var hits = await recommend.SimilarAsync(query);
    foreach (var h in hits)
        Console.WriteLine($"{h.Score:F4}  {h.Title} [{h.Category}] — {h.Description}");
    return 0;
}

static HttpClient CreateHttp()
{
    var http = new HttpClient { BaseAddress = new Uri(SampleDefaults.LmStudioBaseUrl.TrimEnd('/') + "/") };
    http.Timeout = TimeSpan.FromMinutes(5);
    return http;
}

static ReadOnlyMemory<float> SyntheticVector(int seed)
{
    var data = new float[SampleDefaults.VectorDimensions];
    var rng = new Random(seed);
    for (var i = 0; i < data.Length; i++)
        data[i] = (float)rng.NextDouble();
    var norm = MathF.Sqrt(data.Sum(x => x * x));
    for (var i = 0; i < data.Length; i++)
        data[i] /= norm;
    return data;
}

[ZVecCollection("demo_docs")]
file sealed class DemoDoc
{
    [ZVecId]
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";

    [ZVecVector(SampleDefaults.VectorDimensions)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
