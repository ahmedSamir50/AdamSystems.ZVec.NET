using ZVec.NET.Samples.Shared.Data;
using ZVec.NET.Samples.Shared.Rag;

namespace ZVec.NET.Samples.Shared.Services;

/// <summary>Seeds T0/T1 corpora into RAG, search, and recommend collections.</summary>
public sealed class DatasetSeedService
{
    private readonly RagIngestService _ragIngest;
    private readonly SearchIngestService _searchIngest;
    private readonly RecommendService _recommend;

    public DatasetSeedService(
        RagIngestService ragIngest,
        SearchIngestService searchIngest,
        RecommendService recommend)
    {
        _ragIngest = ragIngest ?? throw new ArgumentNullException(nameof(ragIngest));
        _searchIngest = searchIngest ?? throw new ArgumentNullException(nameof(searchIngest));
        _recommend = recommend ?? throw new ArgumentNullException(nameof(recommend));
    }

    public async Task<int> SeedRagFixturesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var fixtures = FixtureLoader.LoadRagFixtures();
        var total = 0;
        foreach (var fx in fixtures)
        {
            ct.ThrowIfCancellationRequested();
            var result = await _ragIngest.IngestTextAsync(fx.Title, fx.Source, fx.Body, fx.Tags, progress, ct)
                .ConfigureAwait(false);
            total += result.ChunkCount;
        }

        progress?.Report($"Seeded {total} RAG chunk(s) from {fixtures.Count} fixture(s).");
        return total;
    }

    public async Task<int> SeedFiqaAsync(
        int maxItems = SampleDefaults.DemoSeedMaxItems,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sources = await FiqaLoader.LoadAsync(maxItems, ct).ConfigureAwait(false);
        return await IngestTextSourcesAsync(_ragIngest, sources, "fiqa", progress, ct).ConfigureAwait(false);
    }

    public async Task<int> SeedSearchFixturesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var fixtures = FixtureLoader.LoadSearchFixtures();
        if (fixtures.Count == 0)
        {
            progress?.Report("No search fixtures found.");
            return 0;
        }

        return await IngestTextSourcesAsync(_searchIngest, fixtures, "search-fixture", progress, ct)
            .ConfigureAwait(false);
    }

    public async Task<int> SeedNfCorpusAsync(
        int maxItems = SampleDefaults.DemoSeedMaxItems,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sources = await NfCorpusLoader.LoadAsync(maxItems, ct).ConfigureAwait(false);
        return await IngestTextSourcesAsync(_searchIngest, sources, "nfcorpus", progress, ct).ConfigureAwait(false);
    }

    public async Task<int> SeedQuoraAsync(
        int maxItems = SampleDefaults.DemoSeedMaxItems,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var sources = await QuoraLoader.LoadAsync(maxItems, ct).ConfigureAwait(false);
        return await IngestTextSourcesAsync(_searchIngest, sources, "quora", progress, ct).ConfigureAwait(false);
    }

    public async Task<int> SeedRecommendFixturesAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var items = FixtureLoader.LoadRecommendFixtures();
        return await _recommend.UpsertItemsAsync(items, progress, ct).ConfigureAwait(false);
    }

    public async Task<int> SeedMovieLensAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var items = await MovieLensLoader.LoadAsync(ct).ConfigureAwait(false);
        if (items.Count > SampleDefaults.DemoSeedMaxItems)
            items = items.Take(SampleDefaults.DemoSeedMaxItems).ToArray();
        return await _recommend.UpsertItemsAsync(items, progress, ct).ConfigureAwait(false);
    }

    public async Task<int> SeedAmazonBeautyAsync(
        int maxItems = SampleDefaults.DemoSeedMaxItems,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var items = await AmazonMetaLoader.LoadAsync(maxItems, ct).ConfigureAwait(false);
        return await _recommend.UpsertItemsAsync(items, progress, ct).ConfigureAwait(false);
    }

    private static async Task<int> IngestTextSourcesAsync(
        RagIngestService ingest,
        IReadOnlyList<TextSource> sources,
        string tag,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var total = 0;
        var i = 0;
        foreach (var src in sources)
        {
            ct.ThrowIfCancellationRequested();
            i++;
            if (i % 50 == 0)
                progress?.Report($"Ingesting {tag} {i}/{sources.Count}…");

            var result = await ingest.IngestTextAsync(src.Title, src.Source, src.Body, tag, progress: null, ct)
                .ConfigureAwait(false);
            total += result.ChunkCount;
        }

        progress?.Report($"Seeded {total} chunk(s) from {sources.Count} {tag} doc(s).");
        return total;
    }

    private static async Task<int> IngestTextSourcesAsync(
        SearchIngestService ingest,
        IReadOnlyList<TextSource> sources,
        string tag,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        var total = 0;
        var i = 0;
        foreach (var src in sources)
        {
            ct.ThrowIfCancellationRequested();
            i++;
            if (i % 50 == 0)
                progress?.Report($"Ingesting {tag} {i}/{sources.Count}…");

            var result = await ingest.IngestTextAsync(src.Title, src.Source, src.Body, tag, progress: null, ct)
                .ConfigureAwait(false);
            total += result.ChunkCount;
        }

        progress?.Report($"Seeded {total} chunk(s) from {sources.Count} {tag} doc(s).");
        return total;
    }
}
