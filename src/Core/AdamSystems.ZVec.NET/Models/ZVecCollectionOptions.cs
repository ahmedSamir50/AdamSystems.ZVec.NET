namespace AdamSystems.ZVec.NET;

/// <summary>Per-collection open options (mmap, read-only, reader concurrency).</summary>
public sealed class ZVecCollectionOptions
{
    /// <summary>Whether to open the collection in read-only mode. Default is false.</summary>
    public bool ReadOnly { get; init; } = ZVecDefaults.CollectionOptions.ReadOnly;

    /// <summary>Whether to enable memory-mapped files (mmap) for querying. Default is true.</summary>
    public bool EnableMmap { get; init; } = ZVecDefaults.CollectionOptions.EnableMmap;

    /// <summary>Maximum concurrent read calls allowed. 0 = use <see cref="System.Environment.ProcessorCount"/> at open time.</summary>
    public int MaxConcurrentReads { get; init; } = ZVecDefaults.Collection.MaxConcurrentReads;
}
