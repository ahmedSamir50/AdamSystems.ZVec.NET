using ZVec.NET.Samples.Shared.Models;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Loads MovieLens latest-small movies.csv from cache (MB zip).</summary>
public static class MovieLensLoader
{
    public static async Task<IReadOnlyList<RecommendItem>> LoadAsync(CancellationToken ct = default)
    {
        await DatasetDownloader.EnsureMovieLensAsync(ct: ct).ConfigureAwait(false);
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "movielens-small");
        Directory.CreateDirectory(dir);

        var moviesCsv = Directory.EnumerateFiles(dir, "movies.csv", SearchOption.AllDirectories).FirstOrDefault()
                        ?? Path.Combine(dir, "movies.csv");

        if (!File.Exists(moviesCsv))
        {
            throw new FileNotFoundException(
                $"MovieLens movies.csv not found after download under '{dir}'. " +
                DatasetCatalog.Attribution(DatasetCatalog.MovieLens));
        }

        DatasetCatalog.EnsureWithinMbBudget(moviesCsv);
        Console.WriteLine(DatasetCatalog.Attribution(DatasetCatalog.MovieLens));

        var items = new List<RecommendItem>();
        using var reader = new StreamReader(moviesCsv);
        _ = await reader.ReadLineAsync(ct).ConfigureAwait(false); // header

        while (!reader.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // movieId,title,genres — title may contain commas inside quotes
            var parsed = ParseCsvLine(line);
            if (parsed.Count < 3)
                continue;

            var id = parsed[0]?.Trim() ?? "";
            var title = parsed[1]?.Trim() ?? "";
            var genres = parsed[2]?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(title))
                continue;

            items.Add(new RecommendItem
            {
                Id = id,
                Title = title,
                Category = genres.Replace("|", ",", StringComparison.Ordinal),
                Description = $"{title}. Genres: {genres.Replace("|", ", ", StringComparison.Ordinal)}"
            });
        }

        return items;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
