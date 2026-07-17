namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Starts background dataset ensure without blocking UI / host listen.</summary>
public static class SampleDatasetBootstrap
{
    private static Task? _background;

    /// <summary>Fire-and-forget ensure of all T1 packs (skip if already present).</summary>
    public static Task StartBackgroundEnsureAsync(CancellationToken ct = default)
    {
        _background ??= Task.Run(async () =>
        {
            try
            {
                var progress = new Progress<string>(msg => Console.WriteLine($"[datasets] {msg}"));
                await DatasetDownloader.EnsureAllAsync(progress, ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[datasets] background download failed: {ex.Message}. T0 fixtures still work.");
            }
        }, ct);

        return _background;
    }

    /// <summary>Await ensure (for Console commands that need T1 packs).</summary>
    public static Task EnsureAllAsync(IProgress<string>? progress = null, CancellationToken ct = default)
        => DatasetDownloader.EnsureAllAsync(progress, ct);
}
