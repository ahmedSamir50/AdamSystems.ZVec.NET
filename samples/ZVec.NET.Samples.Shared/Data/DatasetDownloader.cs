using System.Collections.Concurrent;
using System.IO.Compression;
using System.Net.Http;

namespace ZVec.NET.Samples.Shared.Data;

/// <summary>
/// Downloads MB-capped sample datasets into gitignored cache on first use.
/// Never re-downloads when a pack is already ready. Not part of the NuGet package.
/// </summary>
public static class DatasetDownloader
{
    private static readonly ConcurrentDictionary<string, Task> InFlight = new(StringComparer.Ordinal);
    private static readonly HttpClient Http = CreateHttp();

    private static HttpClient CreateHttp()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ZVec.NET.Samples/1.0");
        return client;
    }

    public static async Task EnsureAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        await EnsureFiqaAsync(progress, ct).ConfigureAwait(false);
        await EnsureNfCorpusAsync(progress, ct).ConfigureAwait(false);
        await EnsureMovieLensAsync(progress, ct).ConfigureAwait(false);
        await EnsureQuoraAsync(progress, ct).ConfigureAwait(false);
        await EnsureAmazonBeautyAsync(progress, ct).ConfigureAwait(false);
    }

    public static Task EnsureFiqaAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => EnsureSingleFlightAsync(DatasetCatalog.Fiqa, () => EnsureZipPackAsync(
            DatasetCatalog.Fiqa,
            DatasetDownloadUrls.FiqaZip,
            Path.Combine(SamplePaths.CacheRoot, "fiqa"),
            IsFiqaReady,
            progress,
            ct));

    public static Task EnsureNfCorpusAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => EnsureSingleFlightAsync(DatasetCatalog.NfCorpus, () => EnsureZipPackAsync(
            DatasetCatalog.NfCorpus,
            DatasetDownloadUrls.NfCorpusZip,
            Path.Combine(SamplePaths.CacheRoot, "nfcorpus"),
            IsNfCorpusReady,
            progress,
            ct));

    public static Task EnsureMovieLensAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => EnsureSingleFlightAsync(DatasetCatalog.MovieLens, () => EnsureZipPackAsync(
            DatasetCatalog.MovieLens,
            DatasetDownloadUrls.MovieLensSmallZip,
            Path.Combine(SamplePaths.CacheRoot, "movielens-small"),
            IsMovieLensReady,
            progress,
            ct));

    public static Task EnsureQuoraAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => EnsureSingleFlightAsync(DatasetCatalog.Quora, () => EnsureQuoraCoreAsync(progress, ct));

    public static Task EnsureAmazonBeautyAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => EnsureSingleFlightAsync(DatasetCatalog.AmazonBeauty, () => EnsureAmazonBeautyCoreAsync(progress, ct));

    public static bool IsFiqaReady() => FindCorpusFile(Path.Combine(SamplePaths.CacheRoot, "fiqa")) is not null
                                       && File.Exists(ReadyMarker(Path.Combine(SamplePaths.CacheRoot, "fiqa")));

    public static bool IsNfCorpusReady() => FindCorpusFile(Path.Combine(SamplePaths.CacheRoot, "nfcorpus")) is not null
                                            && File.Exists(ReadyMarker(Path.Combine(SamplePaths.CacheRoot, "nfcorpus")));

    public static bool IsMovieLensReady()
    {
        var dir = Path.Combine(SamplePaths.CacheRoot, "movielens-small");
        return FindFile(dir, "movies.csv") is not null && File.Exists(ReadyMarker(dir));
    }

    public static bool IsQuoraReady()
    {
        var dir = Path.Combine(SamplePaths.CacheRoot, "quora");
        return FindAny(dir, "*.tsv") is not null && File.Exists(ReadyMarker(dir));
    }

    public static bool IsAmazonBeautyReady()
    {
        var dir = Path.Combine(SamplePaths.CacheRoot, "amazon-beauty");
        var jsonl = FindAny(dir, "*.jsonl");
        return jsonl is not null
               && new FileInfo(jsonl).Length > 0
               && File.Exists(ReadyMarker(dir));
    }

    private static async Task EnsureSingleFlightAsync(string packId, Func<Task> work)
    {
        var task = InFlight.GetOrAdd(packId, _ => RunAndClearAsync(packId, work));
        await task.ConfigureAwait(false);
    }

    private static async Task RunAndClearAsync(string packId, Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(false);
        }
        finally
        {
            InFlight.TryRemove(packId, out _);
        }
    }

    private static async Task EnsureZipPackAsync(
        string packId,
        string url,
        string destDir,
        Func<bool> isReady,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        SamplePaths.EnsureCacheDirectory();
        Directory.CreateDirectory(destDir);

        if (isReady())
        {
            progress?.Report($"skip {packId}: already present");
            return;
        }

        // Recover from unzipped content without marker (e.g. manual drop).
        var hasContent = (packId == DatasetCatalog.Fiqa || packId == DatasetCatalog.NfCorpus)
                && FindCorpusFile(destDir) is not null
            || packId == DatasetCatalog.MovieLens && FindFile(destDir, "movies.csv") is not null;
        if (hasContent)
        {
            WriteReady(destDir);
            progress?.Report($"skip {packId}: already present");
            return;
        }

        progress?.Report($"downloading {packId}…");
        var tmpZip = Path.Combine(destDir, $"{packId}.zip.tmp");
        try
        {
            await DownloadToFileAsync(url, tmpZip, ct).ConfigureAwait(false);
            DatasetCatalog.EnsureWithinMbBudget(tmpZip);

            progress?.Report($"extracting {packId}…");
            ZipFile.ExtractToDirectory(tmpZip, destDir, overwriteFiles: true);
            File.Delete(tmpZip);

            if (!isReady() && FindCorpusFile(destDir) is null && FindFile(destDir, "movies.csv") is null)
                throw new InvalidOperationException($"Downloaded {packId} but expected files were not found under '{destDir}'.");

            WriteReady(destDir);
            progress?.Report($"ready {packId}");
        }
        catch
        {
            TryDelete(tmpZip);
            throw;
        }
    }

    private static async Task EnsureQuoraCoreAsync(IProgress<string>? progress, CancellationToken ct)
    {
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "quora");
        Directory.CreateDirectory(dir);

        if (IsQuoraReady() || FindAny(dir, "*.tsv") is not null)
        {
            if (!File.Exists(ReadyMarker(dir)) && FindAny(dir, "*.tsv") is not null)
                WriteReady(dir);
            progress?.Report($"skip {DatasetCatalog.Quora}: already present");
            return;
        }

        progress?.Report($"downloading {DatasetCatalog.Quora}…");
        var target = Path.Combine(dir, "quora_duplicate_questions.tsv");
        var tmp = target + ".tmp";
        try
        {
            await DownloadToFileAsync(DatasetDownloadUrls.QuoraTsv, tmp, ct).ConfigureAwait(false);
            DatasetCatalog.EnsureWithinMbBudget(tmp);
            File.Move(tmp, target, overwrite: true);
            WriteReady(dir);
            progress?.Report($"ready {DatasetCatalog.Quora}");
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    private static async Task EnsureAmazonBeautyCoreAsync(IProgress<string>? progress, CancellationToken ct)
    {
        SamplePaths.EnsureCacheDirectory();
        var dir = Path.Combine(SamplePaths.CacheRoot, "amazon-beauty");
        Directory.CreateDirectory(dir);

        var existingJsonl = FindAny(dir, "*.jsonl");
        if (IsAmazonBeautyReady() || existingJsonl is not null)
        {
            if (!File.Exists(ReadyMarker(dir)) && existingJsonl is not null)
                WriteReady(dir);
            progress?.Report($"skip {DatasetCatalog.AmazonBeauty}: already present");
            return;
        }

        progress?.Report($"downloading {DatasetCatalog.AmazonBeauty} (stream, capped)…");
        var target = Path.Combine(dir, "meta_All_Beauty.capped.jsonl");
        var tmp = target + ".tmp";

        try
        {
            using var response = await Http.GetAsync(
                DatasetDownloadUrls.AmazonBeautyMetaJsonlGz,
                HttpCompletionOption.ResponseHeadersRead,
                ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var network = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            await using var gzip = new GZipStream(network, CompressionMode.Decompress);
            using var reader = new StreamReader(gzip);
            await using var writer = new StreamWriter(tmp);

            var count = 0;
            long bytesWritten = 0;
            while (count < SampleDefaults.AmazonBeautyMaxItems)
            {
                ct.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
                if (line is null)
                    break;
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                await writer.WriteLineAsync(line.AsMemory(), ct).ConfigureAwait(false);
                bytesWritten += line.Length + 1;
                count++;

                if (bytesWritten > SampleDefaults.MaxPackBytes)
                    break;
            }

            await writer.FlushAsync(ct).ConfigureAwait(false);
            if (count == 0)
                throw new InvalidOperationException("Amazon Beauty stream produced zero items.");

            File.Move(tmp, target, overwrite: true);
            DatasetCatalog.EnsureWithinMbBudget(target);
            WriteReady(dir);
            progress?.Report($"ready {DatasetCatalog.AmazonBeauty} ({count} items)");
        }
        catch
        {
            TryDelete(tmp);
            throw;
        }
    }

    private static async Task DownloadToFileAsync(string url, string path, CancellationToken ct)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long len && len > SampleDefaults.MaxPackBytes)
        {
            throw new InvalidOperationException(
                $"Remote pack is {len / (1024.0 * 1024.0):F1} MB which exceeds the MB budget.");
        }

        await using var network = await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
        await using var file = File.Create(path);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await network.ReadAsync(buffer.AsMemory(0, buffer.Length), ct).ConfigureAwait(false)) > 0)
        {
            total += read;
            if (total > SampleDefaults.MaxPackBytes)
            {
                await file.DisposeAsync().ConfigureAwait(false);
                TryDelete(path);
                throw new InvalidOperationException(
                    $"Download exceeded MB budget ({SampleDefaults.MaxPackBytes / (1024 * 1024)} MB).");
            }

            await file.WriteAsync(buffer.AsMemory(0, read), ct).ConfigureAwait(false);
        }
    }

    private static string ReadyMarker(string dir) => Path.Combine(dir, ".ready");

    private static void WriteReady(string dir)
    {
        Directory.CreateDirectory(dir);
        File.WriteAllText(ReadyMarker(dir), DateTimeOffset.UtcNow.ToString("O"));
    }

    private static string? FindCorpusFile(string dir)
    {
        if (!Directory.Exists(dir))
            return null;
        foreach (var name in new[] { "corpus.jsonl", "corpus.jsonl.gz", "corpus.json" })
        {
            var hit = Directory.EnumerateFiles(dir, name, SearchOption.AllDirectories).FirstOrDefault();
            if (hit is not null)
                return hit;
        }

        return Directory.EnumerateFiles(dir, "*corpus*", SearchOption.AllDirectories)
            .FirstOrDefault(f => f.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase)
                                 || f.EndsWith(".jsonl.gz", StringComparison.OrdinalIgnoreCase));
    }

    private static string? FindFile(string dir, string fileName)
    {
        if (!Directory.Exists(dir))
            return null;
        return Directory.EnumerateFiles(dir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? FindAny(string dir, string pattern)
    {
        if (!Directory.Exists(dir))
            return null;
        return Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort
        }
    }
}
