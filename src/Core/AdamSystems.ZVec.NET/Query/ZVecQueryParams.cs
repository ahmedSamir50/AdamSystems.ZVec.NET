namespace AdamSystems.ZVec.NET;

/// <summary>Index-specific search params (ef_search, nprobe, etc.). Subclass per index family.</summary>
public abstract class ZVecQueryParams
{
}

/// <summary>HNSW search params.</summary>
public sealed class ZVecHnswQueryParams : ZVecQueryParams
{
    public int? EfSearch { get; init; }
}

/// <summary>IVF search params.</summary>
public sealed class ZVecIvfQueryParams : ZVecQueryParams
{
    public int? Nprobe { get; init; }
}
