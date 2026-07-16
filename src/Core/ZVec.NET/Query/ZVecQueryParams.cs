namespace ZVec.NET;

/// <summary>Index-specific search parameters (ef_search, nprobe, etc.). Subclass per index family.</summary>
public abstract class ZVecQueryParams
{
}

/// <summary>HNSW index search parameters.</summary>
public sealed class ZVecHnswQueryParams : ZVecQueryParams
{
    /// <summary>Size of search candidate list during query execution. Larger increases recall but reduces performance.</summary>
    public int? EfSearch { get; init; }
}

/// <summary>IVF index search parameters.</summary>
public sealed class ZVecIvfQueryParams : ZVecQueryParams
{
    /// <summary>Number of cluster lists to probe during search.</summary>
    public int? Nprobe { get; init; }
}
