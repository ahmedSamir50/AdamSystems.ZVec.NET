using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZVec.NET.DependencyInjection;
using ZVec.NET.Samples.AspNet;
using ZVec.NET.Samples.Shared;
using ZVec.NET.Samples.Shared.LmStudio;
using ZVec.NET.Samples.Shared.Models;
using ZVec.NET.Samples.Shared.Rag;
using ZVec.NET.Samples.Shared.Services;

// ZVec.NET.Samples.AspNet — .NET 10 Minimal API demo (never packed; not part of E21 CI).
// Host Dispose / hosted service shuts down the ZVec factory when the app stops.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHostedService<DatasetDownloadHostedService>();

var ragPath = builder.Configuration["SamplePaths:RagCollection"];
if (string.IsNullOrWhiteSpace(ragPath))
    ragPath = SamplePaths.CollectionPath(SampleDefaults.RagCollectionFolder);

var recommendPath = SamplePaths.CollectionPath(SampleDefaults.RecommendCollectionFolder);

builder.Services.AddZVec(builder.Configuration.GetSection("ZVec"));
builder.Services.AddZVecCollection<RagDocument>(options =>
{
    options.Path = ragPath;
    options.EnableMmap = SampleDefaults.EnableMmap;
    options.Create = true;
});
builder.Services.AddZVecCollection<RecommendItem>(options =>
{
    options.Path = recommendPath;
    options.EnableMmap = SampleDefaults.EnableMmap;
    options.Create = true;
});

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
    endpoints = new[] { "/health", "/rag/ingest", "/rag/query", "/rag/ask", "/search", "/recommend" }
}));

app.MapHealthChecks("/health");

app.MapPost("/rag/ingest", async (IngestRequest req, RagIngestService ingest, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Text))
        return Results.BadRequest(new { error = "text is required" });

    var result = await ingest.IngestTextAsync(
        req.Title ?? "api-ingest",
        req.Source ?? "api",
        req.Text,
        req.Tags ?? "",
        ct: ct);
    return Results.Ok(result);
});

app.MapPost("/rag/query", async (QueryRequest req, RagQueryService query, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    var hits = await query.QueryAsync(req.Question, req.TopK <= 0 ? SampleDefaults.DefaultTopK : req.TopK, ct);
    return Results.Ok(new { hits });
});

app.MapPost("/rag/ask", async (QueryRequest req, RagAskService ask, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    var result = await ask.AskAsync(req.Question, req.TopK <= 0 ? SampleDefaults.DefaultTopK : req.TopK, ct);
    return Results.Ok(new { answer = result.Answer, hits = result.Citations, usedChat = result.UsedChat });
});

app.MapPost("/search", async (QueryRequest req, RagQueryService query, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    var hits = await query.QueryAsync(req.Question, req.TopK <= 0 ? SampleDefaults.DefaultTopK : req.TopK, ct);
    return Results.Ok(new { hits });
});

app.MapPost("/recommend", async (QueryRequest req, RecommendService recommend, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Question))
        return Results.BadRequest(new { error = "question is required" });
    var hits = await recommend.SimilarAsync(req.Question, req.TopK <= 0 ? SampleDefaults.DefaultTopK : req.TopK, ct);
    return Results.Ok(new { hits });
});

app.MapPost("/recommend/seed-fixtures", async (RecommendService recommend, CancellationToken ct) =>
{
    var items = ZVec.NET.Samples.Shared.Data.FixtureLoader.LoadRecommendFixtures();
    var count = await recommend.UpsertItemsAsync(items, ct: ct);
    return Results.Ok(new { upserted = count });
});

app.Run();

internal sealed record IngestRequest(string? Title, string? Source, string Text, string? Tags);
internal sealed record QueryRequest(string Question, int TopK = SampleDefaults.DefaultTopK);
