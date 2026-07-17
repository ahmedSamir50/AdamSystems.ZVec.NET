using System.Text.Json;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Loads committed T0 fixtures (no download).</summary>
public static class FixtureLoader
{
    public static IReadOnlyList<TextSource> LoadRagFixtures()
    {
        var dir = Path.Combine(SamplePaths.FixturesRoot, "rag");
        if (Directory.Exists(dir))
        {
            var fromDisk = Directory.EnumerateFiles(dir, "*.md")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(path =>
                {
                    var text = File.ReadAllText(path);
                    var name = Path.GetFileNameWithoutExtension(path);
                    return new TextSource(name, name, text, path, "fixture");
                })
                .ToArray();
            if (fromDisk.Length > 0)
                return fromDisk;
        }

        // Fallback when samples/datasets is not on disk (e.g. packaged MAUI).
        return BuiltInRagFixtures();
    }

    public static IReadOnlyList<TextSource> BuiltInRagFixtures() =>
    [
        new TextSource(
            "zvec-overview",
            "ZVec.NET overview",
            "ZVec.NET is a .NET SDK for Alibaba ZVec, an embedded vector database. It supports HNSW indexes, typed ODM with Mapping attributes, and DI via AddZVec. Dispose closes; Destroy deletes on-disk data. Edge apps can use AppData with mmap for offline RAG.",
            "builtin:zvec-overview",
            "fixture"),
        new TextSource(
            "rag-basics",
            "Offline RAG with ZVec.NET",
            "RAG chunks documents, embeds each chunk with a local model, stores vectors in ZVec, then retrieves top-K chunks for a question. These samples use LM Studio on localhost for embeddings and Gemma 4 for chat. If chat is down, citations still return from ZVec.",
            "builtin:rag-basics",
            "fixture"),
        new TextSource(
            "edge-devices",
            "Edge and offline devices",
            "ZVec is designed for local and edge deployments. MAUI Blazor Hybrid stores collections under AppData with EnableMmap. Native binaries are RID-specific until NuGet multi-RID packaging in Epic E21.",
            "builtin:edge-devices",
            "fixture")
    ];

    public static IReadOnlyList<TextSource> LoadSearchFixtures()
    {
        var path = Path.Combine(SamplePaths.FixturesRoot, "search", "questions.json");
        if (!File.Exists(path))
            return [];

        var items = JsonSerializer.Deserialize<List<FixtureQuestion>>(File.ReadAllText(path)) ?? [];
        return items.Select(q => new TextSource(q.Id, q.Text, q.Text, "fixture:search", "fixture")).ToArray();
    }

    public static IReadOnlyList<RecommendItem> LoadRecommendFixtures()
    {
        var path = Path.Combine(SamplePaths.FixturesRoot, "recommend", "items.json");
        if (!File.Exists(path))
            return [];

        var items = JsonSerializer.Deserialize<List<FixtureItem>>(File.ReadAllText(path)) ?? [];
        return items.Select(i => new RecommendItem
        {
            Id = i.Id,
            Title = i.Title,
            Category = i.Category,
            Description = i.Description
        }).ToArray();
    }

    private sealed class FixtureQuestion
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
    }

    private sealed class FixtureItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Category { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
