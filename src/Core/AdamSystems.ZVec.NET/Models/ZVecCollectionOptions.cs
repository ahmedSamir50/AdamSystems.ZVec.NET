namespace AdamSystems.ZVec.NET;

/// <summary>Per-collection open options (mmap, read-only, reader concurrency).</summary>
public sealed class ZVecCollectionOptions
{
    public bool ReadOnly { get; init; } = false;
    public bool EnableMmap { get; init; } = true;

    /// <summary>0 = use <see cref="Environment.ProcessorCount"/> at open time.</summary>
    public int MaxConcurrentReads { get; init; } = ZVecDefaults.Collection.MaxConcurrentReads;
}
