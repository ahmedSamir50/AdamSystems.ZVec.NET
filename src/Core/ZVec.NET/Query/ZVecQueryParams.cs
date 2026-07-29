namespace ZVec.NET;

/// <summary>Index-specific search parameters (ef_search, nprobe, etc.). Subclass per index family.</summary>
/// <remarks>
/// Maps to typed <c>zvec_query_params_*</c> create/set APIs. Upstream removed
/// <c>QueryParams::set_type()</c>; ZVec.NET never P/Invokes a type setter.
/// </remarks>
public abstract class ZVecQueryParams
{
}

/// <summary>HNSW index search parameters.</summary>
public sealed class ZVecHnswQueryParams : ZVecQueryParams
{
    /// <summary>Size of search candidate list during query execution. Larger increases recall but reduces performance.</summary>
    public int? EfSearch { get; init; }

    /// <summary>Optional search radius. When set, results outside the radius are excluded.</summary>
    public float? Radius { get; init; }

    /// <summary>When true, use linear (brute-force) search instead of the HNSW graph.</summary>
    public bool? IsLinear { get; init; }

    /// <summary>When true, apply the index refiner during search.</summary>
    public bool? IsUsingRefiner { get; init; }
}

/// <summary>IVF index search parameters.</summary>
public sealed class ZVecIvfQueryParams : ZVecQueryParams
{
    /// <summary>Number of cluster lists to probe during search.</summary>
    public int? Nprobe { get; init; }

    /// <summary>Optional search radius.</summary>
    public float? Radius { get; init; }

    /// <summary>When true, use linear search within probed lists.</summary>
    public bool? IsLinear { get; init; }

    /// <summary>When true, apply the index refiner during search.</summary>
    public bool? IsUsingRefiner { get; init; }

    /// <summary>Scale factor passed to native IVF query params. Default is <see cref="ZVecDefaults.Query.IvfScaleFactor"/>.</summary>
    public float? ScaleFactor { get; init; }
}

/// <summary>Flat (brute-force) index search parameters.</summary>
public sealed class ZVecFlatQueryParams : ZVecQueryParams
{
    /// <summary>Optional search radius.</summary>
    public float? Radius { get; init; }

    /// <summary>When true, force linear search mode.</summary>
    public bool? IsLinear { get; init; }

    /// <summary>When true, apply the index refiner during search.</summary>
    public bool? IsUsingRefiner { get; init; }

    /// <summary>Scale factor for Flat query params. Default is <see cref="ZVecDefaults.Query.FlatScaleFactor"/>.</summary>
    public float? ScaleFactor { get; init; }
}

/// <summary>Vamana index search parameters.</summary>
public sealed class ZVecVamanaQueryParams : ZVecQueryParams
{
    /// <summary>Search-time candidate list size (ef_search).</summary>
    public int? EfSearch { get; init; }

    /// <summary>Optional search radius.</summary>
    public float? Radius { get; init; }

    /// <summary>When true, use linear search.</summary>
    public bool? IsLinear { get; init; }

    /// <summary>When true, apply the index refiner during search.</summary>
    public bool? IsUsingRefiner { get; init; }
}

/// <summary>DiskANN index search parameters.</summary>
/// <remarks>DiskANN indexes are Linux-only. Prefer setting these when querying a DiskANN field.</remarks>
public sealed class ZVecDiskAnnQueryParams : ZVecQueryParams
{
    /// <summary>Search frontier / list size. Default is <see cref="ZVecDefaults.Query.DiskAnnListSize"/>.</summary>
    public int? ListSize { get; init; }

    /// <summary>Optional search radius.</summary>
    public float? Radius { get; init; }

    /// <summary>When true, use linear search.</summary>
    public bool? IsLinear { get; init; }

    /// <summary>When true, apply the index refiner during search.</summary>
    public bool? IsUsingRefiner { get; init; }
}
