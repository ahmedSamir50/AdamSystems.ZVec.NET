using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZVec.NET;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Samples.AspNet;
using ZVec.NET.Samples.Shared;
using ZVec.NET.Samples.Shared.Data;
using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;
using ZVec.NET.Samples.Shared.Rag;
using ZVec.NET.Samples.Shared.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<DatasetDownloadHostedService>();

var dataRoot = Path.Combine(Path.GetTempPath(), "ZVec.NET.Samples");
var ragPathOverride = builder.Configuration["SamplePaths:RagCollection"];
if (!string.IsNullOrWhiteSpace(ragPathOverride))
    dataRoot = Path.GetDirectoryName(Path.GetFullPath(ragPathOverride)) ?? dataRoot;

builder.Services.AddZVec(builder.Configuration.GetSection("ZVec"));
builder.Services.AddSampleDemoCollections(dataRoot, SampleDefaults.EnableMmap);

builder.Services.AddZVecSampleAi(opts =>
{
    builder.Configuration.GetSection("LmStudio").Bind(opts);
});

builder.Services.AddHealthChecks()
    .AddCheck<ZVecHealthCheck>("zvec");

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    sample = "ZVec.NET.Samples.AspNet",
    tfm = "net10.0",
    endpoints = new[]
    {
        "GET /health", "GET /status", "GET /hints", "GET /models", "PUT /models",
        "POST /rag/ingest", "POST /rag/seed-fixtures", "POST /rag/seed-fiqa", "POST /rag/query", "POST /rag/ask",
        "POST /search/seed-fixtures", "POST /search/seed-nfcorpus", "POST /search/seed-quora", "POST /search/query",
        "POST /recommend/seed-fixtures", "POST /recommend/seed-movielens", "POST /recommend/seed-amazon", "POST /recommend/query"
    }
}));

app.MapHealthChecks("/health");

app.MapGet("/status", async (
    LmStudioStatusProbe probe,
    IZvecCollection<RagDocument> rag,
    IZvecCollection<SearchDocument> search,
    IZvecCollection<RecommendItem> recommend,
    LmStudioOptions opts,
    CancellationToken ct) =>
{
    var lm = await probe.ProbeAsync(ct);
    return Results.Ok(new
    {
        lmStudio = new
        {
            lm.Reachable,
            lm.EmbeddingsOk,
            lm.ChatOk,
            lm.Message,
            embeddingModel = opts.EmbeddingModel,
            chatModel = opts.ChatModel
        },
        datasets = new
        {
            fiqa = DatasetDownloader.IsFiqaReady(),
            nfcorpus = DatasetDownloader.IsNfCorpusReady(),
            quora = DatasetDownloader.IsQuoraReady(),
            movielens = DatasetDownloader.IsMovieLensReady(),
            amazonBeauty = DatasetDownloader.IsAmazonBeautyReady()
        },
        collections = new
        {
            rag = new { docs = rag.Stats.DocCount, path = rag.Path },
            search = new { docs = search.Stats.DocCount, path = search.Path },
            recommend = new { docs = recommend.Stats.DocCount, path = recommend.Path }
        }
    });
});

app.MapGet("/hints", () => Results.Ok(DemoPromptCatalog.ToHintsResponse()));

app.MapGet("/models", async (LmStudioModelCatalog catalog, LmStudioOptions opts, CancellationToken ct) =>
{
    var ids = await catalog.ListModelIdsAsync(ct);
    return Results.Ok(new
    {
        models = ids,
        selected = new { embeddingModel = opts.EmbeddingModel, chatModel = opts.ChatModel }
    });
});

app.MapPut("/models", async (ModelSelectRequest req, LmStudioModelCatalog catalog, CancellationToken ct) =>
{
    var result = await catalog.ApplySelectionAsync(req.EmbeddingModel, req.ChatModel, ct);
    if (!result.EmbeddingOk && result.EmbeddingError is not null)
        return Results.Json(result, statusCode: StatusCodes.Status400BadRequest);
    return Results.Ok(result);
});

app.MapPost("/rag/ingest", async (IngestRequest req, RagIngestService ingest, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "text is required" });
    try
    {
        var result = await ingest.IngestTextAsync(
            req.Title ?? "api-ingest", req.Source ?? "api", req.Text, req.Tags ?? "", ct: ct);
        return Results.Ok(result);
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/rag/seed-fixtures", async (DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var n = await seed.SeedRagFixturesAsync(ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/rag/seed-fiqa", async (SeedRequest? req, DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var max = req?.MaxItems > 0 ? req.MaxItems : SampleDefaults.DemoSeedMaxItems;
        var n = await seed.SeedFiqaAsync(max, ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/rag/query", async (QueryRequest req, RagQueryService query, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    try
    {
        var hits = await query.QueryAsync(req.Question, TopK(req.TopK), ct);
        return Results.Ok(new { hits });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/rag/ask", async (QueryRequest req, RagAskService ask, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    try
    {
        var result = await ask.AskAsync(req.Question, TopK(req.TopK), ct);
        return Results.Ok(new { answer = result.Answer, hits = result.Citations, usedChat = result.UsedChat });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/search/seed-fixtures", async (DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var n = await seed.SeedSearchFixturesAsync(ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/search/seed-nfcorpus", async (SeedRequest? req, DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var max = req?.MaxItems > 0 ? req.MaxItems : SampleDefaults.DemoSeedMaxItems;
        var n = await seed.SeedNfCorpusAsync(max, ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/search/seed-quora", async (SeedRequest? req, DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var max = req?.MaxItems > 0 ? req.MaxItems : SampleDefaults.DemoSeedMaxItems;
        var n = await seed.SeedQuoraAsync(max, ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/search/query", async (QueryRequest req, SearchQueryService query, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    try
    {
        var hits = await query.QueryAsync(req.Question, TopK(req.TopK), ct);
        return Results.Ok(new { hits });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/recommend/seed-fixtures", async (DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var n = await seed.SeedRecommendFixturesAsync(ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/recommend/seed-movielens", async (DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var n = await seed.SeedMovieLensAsync(ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/recommend/seed-amazon", async (SeedRequest? req, DatasetSeedService seed, CancellationToken ct) =>
{
    try
    {
        var max = req?.MaxItems > 0 ? req.MaxItems : SampleDefaults.DemoSeedMaxItems;
        var n = await seed.SeedAmazonBeautyAsync(max, ct: ct);
        return Results.Ok(new { upserted = n });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapPost("/recommend/query", async (QueryRequest req, RecommendService recommend, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    try
    {
        var hits = await recommend.SimilarAsync(req.Question, TopK(req.TopK), ct);
        return Results.Ok(new { hits });
    }
    catch (Exception ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Back-compat alias
app.MapPost("/recommend", async (QueryRequest req, RecommendService recommend, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    var hits = await recommend.SimilarAsync(req.Question, TopK(req.TopK), ct);
    return Results.Ok(new { hits });
});

app.Run();

static int TopK(int topK) => topK <= 0 ? SampleDefaults.DefaultTopK : topK;

internal sealed record IngestRequest(string? Title, string? Source, string Text, string? Tags);
internal sealed record QueryRequest(string Question, int TopK = SampleDefaults.DefaultTopK);
internal sealed record SeedRequest(int MaxItems = SampleDefaults.DemoSeedMaxItems);
internal sealed record ModelSelectRequest(string? EmbeddingModel, string? ChatModel);
