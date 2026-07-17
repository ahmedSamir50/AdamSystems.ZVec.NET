namespace ZVec.NET.Samples.Shared.Data;

/// <summary>
/// Loads unique questions from Quora Question Pairs TSV (≈55–60 MB file),
/// capped at <see cref="SampleDefaults.QuoraMaxUniqueQuestions"/>.
/// </summary>
public static class QuoraLoader
{
    public static async Task<IReadOnlyList<TextSource>> LoadAsync(
        int maxUnique = SampleDefaults.QuoraMaxUniqueQuestions,
        CancellationToken ct = default)
    {
        await DatasetDownloader.EnsureQuoraAsync(ct: ct).ConfigureAwait(false);
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "quora");
        Directory.CreateDirectory(dir);

        var tsv = Directory.EnumerateFiles(dir, "*.tsv", SearchOption.AllDirectories).FirstOrDefault()
                  ?? Directory.EnumerateFiles(dir, "*.csv", SearchOption.AllDirectories).FirstOrDefault();

        if (tsv is null)
        {
            throw new FileNotFoundException(
                $"Quora TSV not found after download under '{dir}'. See samples/datasets/README.md. " +
                DatasetCatalog.Attribution(DatasetCatalog.Quora));
        }

        DatasetCatalog.EnsureWithinMbBudget(tsv);
        Console.WriteLine(DatasetCatalog.Attribution(DatasetCatalog.Quora));

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<TextSource>();

        using var reader = new StreamReader(tsv);
        var header = await reader.ReadLineAsync(ct).ConfigureAwait(false);
        var sep = tsv.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) ? ',' : '\t';

        while (!reader.EndOfStream && results.Count < maxUnique)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split(sep);
            // Expected: id, qid1, qid2, question1, question2, is_duplicate (variants exist)
            if (parts.Length < 5)
                continue;

            TryAdd(parts, 1, 3, seen, results, maxUnique);
            if (results.Count >= maxUnique) break;
            TryAdd(parts, 2, 4, seen, results, maxUnique);
        }

        return results;
    }

    private static void TryAdd(
        string[] parts,
        int qidIndex,
        int textIndex,
        HashSet<string> seen,
        List<TextSource> results,
        int maxUnique)
    {
        if (qidIndex >= parts.Length || textIndex >= parts.Length)
            return;
        var qid = parts[qidIndex].Trim().Trim('"');
        var text = parts[textIndex].Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(qid) || string.IsNullOrWhiteSpace(text))
            return;
        if (!seen.Add(qid))
            return;
        results.Add(new TextSource(qid, text, text, "quora", "quora"));
    }
}
