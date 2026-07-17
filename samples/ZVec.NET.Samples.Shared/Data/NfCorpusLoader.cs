using System.IO.Compression;
using System.Text.Json;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Loads BEIR NFCorpus from samples/datasets/cache/nfcorpus/ (few MB).</summary>
public static class NfCorpusLoader
{
    public static async Task<IReadOnlyList<TextSource>> LoadAsync(
        int? maxItems = null,
        CancellationToken ct = default)
    {
        await DatasetDownloader.EnsureNfCorpusAsync(ct: ct).ConfigureAwait(false);
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "nfcorpus");
        Directory.CreateDirectory(dir);

        var corpusPath = Directory.EnumerateFiles(dir, "*corpus*", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".json", StringComparison.OrdinalIgnoreCase));

        if (corpusPath is null)
        {
            throw new FileNotFoundException(
                $"NFCorpus not found after download under '{dir}'. See samples/datasets/README.md. " +
                DatasetCatalog.Attribution(DatasetCatalog.NfCorpus));
        }

        DatasetCatalog.EnsureWithinMbBudget(corpusPath);
        Console.WriteLine(DatasetCatalog.Attribution(DatasetCatalog.NfCorpus));

        var results = new List<TextSource>();
        await foreach (var line in ReadLinesAsync(corpusPath, ct).ConfigureAwait(false))
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('['))
            {
                // Optional: whole JSON array file
                if (line.TrimStart().StartsWith('['))
                {
                    var arr = JsonSerializer.Deserialize<List<JsonElement>>(await File.ReadAllTextAsync(corpusPath, ct));
                    if (arr is null) break;
                    foreach (var el in arr)
                    {
                        if (TryParse(el, out var src))
                        {
                            results.Add(src);
                            if (maxItems is int m && results.Count >= m) return results;
                        }
                    }
                    break;
                }
                continue;
            }

            using var doc = JsonDocument.Parse(line);
            if (TryParse(doc.RootElement, out var row))
            {
                results.Add(row);
                if (maxItems is int max && results.Count >= max)
                    break;
            }
        }

        return results;
    }

    private static bool TryParse(JsonElement root, out TextSource source)
    {
        source = null!;
        var id = root.TryGetProperty("_id", out var idEl) ? idEl.GetString()
            : root.TryGetProperty("id", out var id2) ? id2.GetString() : null;
        var title = root.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
        var text = root.TryGetProperty("text", out var tx) ? tx.GetString() : null;
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(text))
            return false;
        source = new TextSource(id!, string.IsNullOrWhiteSpace(title) ? id! : title, text!, "nfcorpus", "nfcorpus");
        return true;
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
