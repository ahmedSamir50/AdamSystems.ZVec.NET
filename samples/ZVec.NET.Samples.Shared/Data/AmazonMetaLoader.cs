using System.Text.Json;
using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>
/// Loads a capped Amazon All_Beauty metadata JSONL export (title/features/description only).
/// Full category dumps that exceed the MB budget are rejected.
/// </summary>
public static class AmazonMetaLoader
{
    public static async Task<IReadOnlyList<RecommendItem>> LoadAsync(
        int maxItems = SampleDefaults.AmazonBeautyMaxItems,
        CancellationToken ct = default)
    {
        await DatasetDownloader.EnsureAmazonBeautyAsync(ct: ct).ConfigureAwait(false);
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "amazon-beauty");
        Directory.CreateDirectory(dir);

        var file = Directory.EnumerateFiles(dir, "*.jsonl", SearchOption.AllDirectories).FirstOrDefault()
                   ?? Directory.EnumerateFiles(dir, "*.json", SearchOption.AllDirectories).FirstOrDefault();

        if (file is null)
        {
            throw new FileNotFoundException(
                $"Amazon Beauty metadata not found after download under '{dir}'. " +
                DatasetCatalog.Attribution(DatasetCatalog.AmazonBeauty));
        }

        DatasetCatalog.EnsureWithinMbBudget(file);
        Console.WriteLine(DatasetCatalog.Attribution(DatasetCatalog.AmazonBeauty));

        var items = new List<RecommendItem>();
        using var reader = new StreamReader(file);
        string? line;
        while (items.Count < maxItems &&
               (line = await reader.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var id = root.TryGetProperty("parent_asin", out var asin) ? asin.GetString()
                : root.TryGetProperty("asin", out var a2) ? a2.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            var features = root.TryGetProperty("features", out var f) && f.ValueKind == JsonValueKind.Array
                ? string.Join(" ", f.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                : "";
            var description = root.TryGetProperty("description", out var d) && d.ValueKind == JsonValueKind.Array
                ? string.Join(" ", d.EnumerateArray().Select(x => x.GetString()).Where(s => !string.IsNullOrWhiteSpace(s)))
                : root.TryGetProperty("description", out var d2) ? d2.GetString() ?? "" : "";

            var category = root.TryGetProperty("main_category", out var c) ? c.GetString() ?? "Beauty" : "Beauty";
            var body = string.Join(" ", new[] { features, description }.Where(s => !string.IsNullOrWhiteSpace(s)));

            items.Add(new RecommendItem
            {
                Id = id!,
                Title = title,
                Category = category,
                Description = string.IsNullOrWhiteSpace(body) ? title : body
            });
        }

        return items;
    }
}
