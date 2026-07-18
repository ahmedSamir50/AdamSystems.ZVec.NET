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
    return await RunInteractiveAsync();

return await DispatchAsync(args);

static async Task<int> RunInteractiveAsync()
{
    _ = SampleDatasetBootstrap.StartBackgroundEnsureAsync();
    PrintHelp();
    using var host = SampleConsoleHost.Create();

    while (true)
    {
        Console.WriteLine();
        Console.Write("> ");
        var line = Console.ReadLine();
        if (line is null)
            return 0;

        var parts = SplitArgs(line);
        if (parts.Length == 0)
            continue;

        var cmd = parts[0].ToLowerInvariant();
        if (cmd is "quit" or "exit" or "q")
            return 0;

        try
        {
            await DispatchHostAsync(host, parts);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
        }
    }
}

static async Task<int> DispatchAsync(string[] args)
{
    var command = args[0].ToLowerInvariant();
    if (command is "help" or "-h" or "--help")
    {
        PrintHelp();
        return 0;
    }

    if (command is "ingest" or "search" or "recommend" or "rag" or "seed")
    {
        try
        {
            await SampleDatasetBootstrap.EnsureAllAsync(
                new Progress<string>(m => Console.WriteLine($"[datasets] {m}")));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[datasets] ensure failed: {ex.Message}. T0 fixtures still work.");
        }
    }
    else if (command is not "basics")
    {
        _ = SampleDatasetBootstrap.StartBackgroundEnsureAsync();
    }

    using var host = SampleConsoleHost.Create();
    try
    {
        await DispatchHostAsync(host, args);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

static async Task DispatchHostAsync(SampleConsoleHost host, string[] args)
{
    var cmd = args[0].ToLowerInvariant();
    switch (cmd)
    {
        case "help":
        case "-h":
        case "--help":
            PrintHelp();
            break;
        case "basics":
            await RunBasicsAsync();
            break;
        case "status":
            await host.PrintStatusAsync();
            break;
        case "models":
            await host.SelectModelsAsync();
            break;
        case "ingest":
            await host.RunLegacyIngestAsync(args);
            break;
        case "ask":
            await host.AskAsync(JoinRest(args));
            break;
        case "search":
            if (args.Length >= 2 && args[1].StartsWith("seed-", StringComparison.OrdinalIgnoreCase))
                await host.RunSearchSeedAsync(args[1]);
            else
                await host.SearchAsync(JoinRest(args));
            break;
        case "recommend":
            if (args.Length >= 2 && args[1].StartsWith("seed-", StringComparison.OrdinalIgnoreCase))
                await host.RunRecommendSeedAsync(args[1]);
            else
                await host.RecommendAsync(JoinRest(args));
            break;
        case "rag":
            await host.RunRagCommandAsync(args.Skip(1).ToArray());
            break;
        default:
            Console.Error.WriteLine($"Unknown command: {cmd}");
            PrintHelp();
            break;
    }
}

static void PrintHelp()
{
    Console.WriteLine("""
        ZVec.NET.Samples.Console (.NET 10) — offline RAG / search / recommend

        Run with no args for an interactive menu. Commands:
          status                         LM Studio + dataset + doc counts
          models                         List LM Studio models; select embed + chat
          basics                         Typed ODM vignette (no LM Studio)
          rag seed-fixtures|seed-eg-faq|seed-fiqa|ask
          search seed-fixtures|seed-nfcorpus|seed-quora|query
          recommend seed-fixtures|seed-movielens|seed-amazon|query
          ingest [--fixtures|--file path]  (legacy; same as rag seed-fixtures / file)
          ask / search / recommend <text>  (legacy shortcuts)
          help | quit

        Defaults: EmbeddingGemma + google/gemma-4-e2b (change via models).
        T1 packs download into samples/datasets/cache/ (skip if present).
        """);
}

static string JoinRest(string[] args)
    => args.Length < 2 ? "" : string.Join(' ', args.Skip(1));

static string[] SplitArgs(string line)
{
    var list = new List<string>();
    var current = new System.Text.StringBuilder();
    var inQuotes = false;
    foreach (var ch in line)
    {
        if (ch == '"')
        {
            inQuotes = !inQuotes;
            continue;
        }

        if (char.IsWhiteSpace(ch) && !inQuotes)
        {
            if (current.Length > 0)
            {
                list.Add(current.ToString());
                current.Clear();
            }

            continue;
        }

        current.Append(ch);
    }

    if (current.Length > 0)
        list.Add(current.ToString());
    return list.ToArray();
}

static async Task RunBasicsAsync()
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

file sealed class SampleConsoleHost : IDisposable
{
    private readonly IZvecFactory _factory;
    private readonly IZvecCollection<RagDocument> _rag;
    private readonly IZvecCollection<SearchDocument> _search;
    private readonly IZvecCollection<RecommendItem> _recommend;
    private readonly HttpClient _httpEmbed;
    private readonly HttpClient _httpChat;
    private readonly HttpClient _httpModels;
    private readonly LmStudioOptions _options;
    private readonly IEmbeddingClient _embeddings;
    private readonly IChatClient _chat;
    private readonly RagIngestService _ragIngest;
    private readonly RagAskService _ask;
    private readonly RagQueryService _ragQuery;
    private readonly SearchIngestService _searchIngest;
    private readonly SearchQueryService _searchQuery;
    private readonly RecommendService _recommendService;
    private readonly DatasetSeedService _seed;
    private readonly LmStudioModelCatalog _models;
    private readonly LmStudioStatusProbe _probe;
    private readonly Progress<string> _progress = new(m => Console.WriteLine(m));

    private SampleConsoleHost(
        IZvecFactory factory,
        IZvecCollection<RagDocument> rag,
        IZvecCollection<SearchDocument> search,
        IZvecCollection<RecommendItem> recommend,
        HttpClient httpEmbed,
        HttpClient httpChat,
        HttpClient httpModels,
        LmStudioOptions options)
    {
        _factory = factory;
        _rag = rag;
        _search = search;
        _recommend = recommend;
        _httpEmbed = httpEmbed;
        _httpChat = httpChat;
        _httpModels = httpModels;
        _options = options;
        _embeddings = new LmStudioEmbeddingClient(httpEmbed, options);
        _chat = new LmStudioChatClient(httpChat, options);
        var chunker = new TextChunker();
        _ragIngest = new RagIngestService(rag, _embeddings, chunker);
        _ragQuery = new RagQueryService(rag, _embeddings);
        _ask = new RagAskService(_ragQuery, _chat, options);
        _searchIngest = new SearchIngestService(search, _embeddings, chunker);
        _searchQuery = new SearchQueryService(search, _embeddings);
        _recommendService = new RecommendService(recommend, _embeddings);
        _seed = new DatasetSeedService(_ragIngest, _searchIngest, _recommendService);
        _models = new LmStudioModelCatalog(httpModels, options, _embeddings);
        _probe = new LmStudioStatusProbe(_embeddings, _chat, options);
    }

    public static SampleConsoleHost Create()
    {
        var factory = CollectionBootstrap.CreateFactory();
        var rag = CollectionBootstrap.OpenRag(factory, SamplePaths.CollectionPath(SampleDefaults.RagCollectionFolder));
        var search = CollectionBootstrap.OpenSearch(factory, SamplePaths.CollectionPath(SampleDefaults.SearchCollectionFolder));
        var recommend = CollectionBootstrap.OpenRecommend(factory, SamplePaths.CollectionPath(SampleDefaults.RecommendCollectionFolder));
        var options = new LmStudioOptions();
        return new SampleConsoleHost(
            factory, rag, search, recommend,
            CreateHttp(), CreateHttp(), CreateHttp(), options);
    }

    private static HttpClient CreateHttp()
    {
        var http = new HttpClient { BaseAddress = new Uri(SampleDefaults.LmStudioBaseUrl.TrimEnd('/') + "/") };
        http.Timeout = TimeSpan.FromMinutes(5);
        return http;
    }

    public async Task PrintStatusAsync()
    {
        var status = await _probe.ProbeAsync();
        Console.WriteLine(status.Message);
        Console.WriteLine($"  embed model: {status.EmbeddingModel}  ok={status.EmbeddingsOk}");
        Console.WriteLine($"  chat model:  {status.ChatModel}  ok={status.ChatOk}");
        Console.WriteLine($"  RAG docs: {_rag.Stats.DocCount} @ {_rag.Path}");
        Console.WriteLine($"  Search docs: {_search.Stats.DocCount} @ {_search.Path}");
        Console.WriteLine($"  Recommend docs: {_recommend.Stats.DocCount} @ {_recommend.Path}");
        Console.WriteLine($"  packs: fiqa={DatasetDownloader.IsFiqaReady()} nf={DatasetDownloader.IsNfCorpusReady()} " +
                          $"quora={DatasetDownloader.IsQuoraReady()} ml={DatasetDownloader.IsMovieLensReady()} " +
                          $"amazon={DatasetDownloader.IsAmazonBeautyReady()}");
    }

    public async Task SelectModelsAsync()
    {
        var ids = await _models.ListModelIdsAsync();
        if (ids.Count == 0)
        {
            Console.WriteLine("No models returned. Is LM Studio running at " + _options.BaseUrl + "?");
            return;
        }

        Console.WriteLine("LM Studio models:");
        for (var i = 0; i < ids.Count; i++)
        {
            var mark = LmStudioModelCatalog.LooksLikeEmbeddingModel(ids[i]) ? " [embed?]" : "";
            Console.WriteLine($"  {i + 1}. {ids[i]}{mark}");
        }

        Console.WriteLine($"Current embed: {_options.EmbeddingModel}");
        Console.WriteLine($"Current chat:  {_options.ChatModel}");
        Console.Write("Embedding model (# or id, Enter=keep): ");
        var embedPick = ResolvePick(Console.ReadLine(), ids, _options.EmbeddingModel);
        Console.Write("Chat model (# or id, Enter=keep): ");
        var chatPick = ResolvePick(Console.ReadLine(), ids, _options.ChatModel);

        var result = await _models.ApplySelectionAsync(embedPick, chatPick);
        Console.WriteLine($"Selected embed={result.EmbeddingModel} chat={result.ChatModel} embedOk={result.EmbeddingOk}");
        if (result.EmbeddingError is not null)
            Console.WriteLine("  " + result.EmbeddingError);
        if (result.Warning is not null && !result.EmbeddingOk)
            Console.WriteLine("  " + result.Warning);
    }

    private static string ResolvePick(string? input, IReadOnlyList<string> ids, string current)
    {
        if (string.IsNullOrWhiteSpace(input))
            return current;
        input = input.Trim();
        if (int.TryParse(input, out var n) && n >= 1 && n <= ids.Count)
            return ids[n - 1];
        return input;
    }

    public async Task RunRagCommandAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: rag seed-fixtures | seed-eg-faq | seed-fiqa | ask");
            return;
        }

        switch (args[0].ToLowerInvariant())
        {
            case "seed-fixtures":
                await _seed.SeedRagFixturesAsync(_progress);
                break;
            case "seed-eg-faq":
                await _seed.SeedArabicEgFaqAsync(_progress);
                break;
            case "seed-fiqa":
                await EnsureDatasetsAsync();
                await _seed.SeedFiqaAsync(progress: _progress);
                break;
            case "ask":
                await AskAsync(string.Join(' ', args.Skip(1)));
                break;
            default:
                Console.WriteLine("Unknown rag subcommand.");
                break;
        }
    }

    public async Task RunSearchSeedAsync(string sub)
    {
        await EnsureDatasetsAsync();
        switch (sub.ToLowerInvariant())
        {
            case "seed-fixtures":
                await _seed.SeedSearchFixturesAsync(_progress);
                break;
            case "seed-nfcorpus":
                await _seed.SeedNfCorpusAsync(progress: _progress);
                break;
            case "seed-quora":
                await _seed.SeedQuoraAsync(progress: _progress);
                break;
            default:
                Console.WriteLine("Unknown search seed.");
                break;
        }
    }

    public async Task RunRecommendSeedAsync(string sub)
    {
        await EnsureDatasetsAsync();
        switch (sub.ToLowerInvariant())
        {
            case "seed-fixtures":
                await _seed.SeedRecommendFixturesAsync(_progress);
                break;
            case "seed-movielens":
                await _seed.SeedMovieLensAsync(_progress);
                break;
            case "seed-amazon":
                await _seed.SeedAmazonBeautyAsync(progress: _progress);
                break;
            default:
                Console.WriteLine("Unknown recommend seed.");
                break;
        }
    }

    public async Task RunLegacyIngestAsync(string[] args)
    {
        if (args.Any(a => a.Equals("--file", StringComparison.OrdinalIgnoreCase)))
        {
            var idx = Array.FindIndex(args, a => a.Equals("--file", StringComparison.OrdinalIgnoreCase));
            if (idx < 0 || idx + 1 >= args.Length)
            {
                Console.WriteLine("Usage: ingest --file path");
                return;
            }

            var result = await _ragIngest.IngestFileAsync(args[idx + 1], progress: _progress);
            Console.WriteLine($"Ingested {result.ChunkCount} chunk(s) from {result.Source}");
            return;
        }

        await _seed.SeedRagFixturesAsync(_progress);
    }

    public async Task AskAsync(string? question)
    {
        question = await PromptWithSuggestionsAsync(question, DemoPromptCatalog.Rag, DemoPromptCatalog.RagBlurb);
        if (string.IsNullOrWhiteSpace(question))
            return;

        var result = await _ask.AskAsync(question);
        Console.WriteLine(result.UsedChat ? "Answer (chat):" : "Answer (retrieve-only):");
        Console.WriteLine(result.Answer);
        Console.WriteLine("Citations:");
        foreach (var c in result.Citations)
            Console.WriteLine($"  [{c.Id}] {c.Title} ({c.Score:F4}) — {c.Snippet}");
    }

    public async Task SearchAsync(string? query)
    {
        if (argsLooksLikeSeed(query))
        {
            await RunSearchSeedAsync(query!);
            return;
        }

        query = await PromptWithSuggestionsAsync(query, DemoPromptCatalog.Search, DemoPromptCatalog.SearchBlurb);
        if (string.IsNullOrWhiteSpace(query))
            return;

        if (_search.Stats.DocCount == 0)
        {
            Console.WriteLine("Search collection empty. Try: search seed-fixtures");
            return;
        }

        var hits = await _searchQuery.QueryAsync(query);
        foreach (var h in hits)
            Console.WriteLine($"{h.Score:F4}  {h.Title}  {h.Snippet}");
    }

    public async Task RecommendAsync(string? query)
    {
        if (argsLooksLikeSeed(query))
        {
            await RunRecommendSeedAsync(query!);
            return;
        }

        query = await PromptWithSuggestionsAsync(query, DemoPromptCatalog.Recommend, DemoPromptCatalog.RecommendBlurb);
        if (string.IsNullOrWhiteSpace(query))
            return;

        if (_recommend.Stats.DocCount == 0)
        {
            Console.WriteLine("Recommend collection empty — seeding T0 fixtures…");
            await _seed.SeedRecommendFixturesAsync(_progress);
        }

        var hits = await _recommendService.SimilarAsync(query);
        foreach (var h in hits)
            Console.WriteLine($"{h.Score:F4}  {h.Title} [{h.Category}] — {h.Description}");
    }

    private static bool argsLooksLikeSeed(string? q)
        => q is not null && q.StartsWith("seed-", StringComparison.OrdinalIgnoreCase);

    private static Task<string?> PromptWithSuggestionsAsync(
        string? provided,
        IReadOnlyList<DemoPrompt> prompts,
        string blurb)
    {
        if (!string.IsNullOrWhiteSpace(provided))
            return Task.FromResult<string?>(provided);

        Console.WriteLine(blurb);
        Console.WriteLine("Suggested:");
        for (var i = 0; i < prompts.Count; i++)
            Console.WriteLine($"  {i + 1}. {prompts[i].Label}  ({prompts[i].CorpusHint})");
        Console.Write("Query (# or text): ");
        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
            return Task.FromResult<string?>(null);
        input = input.Trim();
        if (int.TryParse(input, out var n) && n >= 1 && n <= prompts.Count)
            return Task.FromResult<string?>(prompts[n - 1].Query);
        return Task.FromResult<string?>(input);
    }

    private static async Task EnsureDatasetsAsync()
    {
        try
        {
            await SampleDatasetBootstrap.EnsureAllAsync(
                new Progress<string>(m => Console.WriteLine($"[datasets] {m}")));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[datasets] {ex.Message}");
        }
    }

    public void Dispose()
    {
        _rag.Dispose();
        _search.Dispose();
        _recommend.Dispose();
        _factory.Dispose();
        _httpEmbed.Dispose();
        _httpChat.Dispose();
        _httpModels.Dispose();
    }
}
