using System.Text.Json;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Loads committed T0 fixtures (no download).</summary>
public static class FixtureLoader
{
    private static readonly JsonSerializerOptions FixtureJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

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
        if (File.Exists(path))
        {
            var items = JsonSerializer.Deserialize<List<FixtureQuestion>>(File.ReadAllText(path), FixtureJson) ?? [];
            var valid = items
                .Where(q => !string.IsNullOrWhiteSpace(q.Id) && !string.IsNullOrWhiteSpace(q.Text))
                .Select(q => new TextSource(q.Id.Trim(), q.Text.Trim(), q.Text.Trim(), "fixture:search", "fixture"))
                .ToArray();
            if (valid.Length > 0)
                return valid;
        }

        return BuiltInSearchFixtures();
    }

    public static IReadOnlyList<TextSource> BuiltInSearchFixtures() =>
    [
        new("q1", "How do I open a ZVec collection in .NET?", "How do I open a ZVec collection in .NET?", "builtin:search", "fixture"),
        new("q2", "What is the difference between Dispose and Destroy?", "What is the difference between Dispose and Destroy?", "builtin:search", "fixture"),
        new("q3", "How do typed ODM attributes map to schema?", "How do typed ODM attributes map to schema?", "builtin:search", "fixture"),
        new("q4", "How can I run semantic search offline on a device?", "How can I run semantic search offline on a device?", "builtin:search", "fixture"),
        new("q5", "What LM Studio models are used for embeddings and chat?", "What LM Studio models are used for embeddings and chat?", "builtin:search", "fixture")
    ];

    public static IReadOnlyList<RecommendItem> LoadRecommendFixtures()
    {
        var path = Path.Combine(SamplePaths.FixturesRoot, "recommend", "items.json");
        if (File.Exists(path))
        {
            var items = JsonSerializer.Deserialize<List<FixtureItem>>(File.ReadAllText(path), FixtureJson) ?? [];
            var valid = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Id) && !string.IsNullOrWhiteSpace(i.Title))
                .Select(i => new RecommendItem
                {
                    Id = i.Id.Trim(),
                    Title = i.Title.Trim(),
                    Category = i.Category?.Trim() ?? "",
                    Description = i.Description?.Trim() ?? ""
                })
                .ToArray();
            if (valid.Length > 0)
                return valid;
        }

        return BuiltInRecommendFixtures();
    }

    public static IReadOnlyList<RecommendItem> BuiltInRecommendFixtures() =>
    [
        new() { Id = "m1", Title = "The Matrix", Category = "Sci-Fi, Action", Description = "A computer hacker learns about the true nature of reality." },
        new() { Id = "m2", Title = "Inception", Category = "Sci-Fi, Thriller", Description = "A thief who steals secrets through dream-sharing technology." },
        new() { Id = "m3", Title = "Interstellar", Category = "Sci-Fi, Drama", Description = "Explorers travel through a wormhole in space." },
        new() { Id = "m4", Title = "Spirited Away", Category = "Animation, Fantasy", Description = "A young girl enters a world ruled by gods and spirits." },
        new() { Id = "m5", Title = "The Godfather", Category = "Crime, Drama", Description = "The aging patriarch of a crime dynasty transfers control to his son." }
    ];

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
