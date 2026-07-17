using System.IO.Compression;
using System.Text.Json;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>
/// Loads BEIR FiQA corpus from a local cache folder (jsonl).
/// Download the BEIR fiqa corpus zip into samples/datasets/cache/fiqa/ (≤ ~100 MB).
/// </summary>
public static class FiqaLoader
{
    public static async Task<IReadOnlyList<TextSource>> LoadAsync(
        int? maxItems = null,
        CancellationToken ct = default)
    {
        await DatasetDownloader.EnsureFiqaAsync(ct: ct).ConfigureAwait(false);
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "fiqa");
        Directory.CreateDirectory(dir);

        var corpusPath = FindCorpusFile(dir);
        if (corpusPath is null)
        {
            throw new FileNotFoundException(
                "FiQA corpus not found after download. See samples/datasets/README.md. " +
                DatasetCatalog.Attribution(DatasetCatalog.Fiqa));
        }

        DatasetCatalog.EnsureWithinMbBudget(corpusPath);
        Console.WriteLine(DatasetCatalog.Attribution(DatasetCatalog.Fiqa));

        var results = new List<TextSource>();
        await foreach (var line in ReadLinesAsync(corpusPath, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            using var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;
            var id = root.TryGetProperty("_id", out var idEl) ? idEl.GetString()
                : root.TryGetProperty("id", out var id2) ? id2.GetString() : null;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
            var text = root.TryGetProperty("text", out var tx) ? tx.GetString()
                : root.TryGetProperty("contents", out var c) ? c.GetString() : null;

            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
                continue;

            results.Add(new TextSource(id!, string.IsNullOrWhiteSpace(title) ? id! : title, text!, "fiqa", "fiqa"));
            if (maxItems is int m && results.Count >= m)
                break;
        }

        return results;
    }

    private static string? FindCorpusFile(string dir)
    {
        foreach (var name in new[] { "corpus.jsonl", "corpus.jsonl.gz", "corpus.json" })
        {
            var path = Path.Combine(dir, name);
            if (File.Exists(path))
                return path;
        }

        return Directory.EnumerateFiles(dir, "*corpus*", SearchOption.AllDirectories).FirstOrDefault();
    }

    private static async IAsyncEnumerable<string> ReadLinesAsync(
        string path,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        Stream stream = File.OpenRead(path);
        if (path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
            stream = new GZipStream(stream, CompressionMode.Decompress);

        using (stream)
        using (var reader = new StreamReader(stream))
        {
            while (!reader.EndOfStream)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is not null)
                    yield return line;
            }
        }
    }
}
