namespace AdamSystems.ZVec.NET;

/// <summary>
/// Named SDK defaults aligned with native ZVec configurations.
/// Centralizes all default parameters for collection options, indices, and querying.
/// </summary>
public static class ZVecDefaults
{
    /// <summary>Default parameters for HNSW index.</summary>
    public static class Hnsw
    {
        /// <summary>Default metric type: Cosine similarity.</summary>
        public const ZVecMetricType MetricType = ZVecMetricType.Cosine;

        /// <summary>Default number of bi-directional links (M): 16.</summary>
        public const int M = 16;

        /// <summary>Default size of dynamic candidate list during construction: 200.</summary>
        public const int EfConstruction = 200;

        /// <summary>Default quantization type: Undefined (no quantization).</summary>
        public const ZVecQuantizeType QuantizeType = ZVecQuantizeType.Undefined;
    }

    /// <summary>Default parameters for HNSW with RaBitQ quantization index.</summary>
    public static class HnswRabitq
    {
        /// <summary>Default metric type: Cosine similarity.</summary>
        public const ZVecMetricType MetricType = ZVecMetricType.Cosine;

        /// <summary>Default number of bi-directional links (M): 16.</summary>
        public const int M = 16;

        /// <summary>Default size of dynamic candidate list during construction: 200.</summary>
        public const int EfConstruction = 200;

        /// <summary>Default total bits for quantization: 7.</summary>
        public const int TotalBits = 7;

        /// <summary>Default number of clusters: 16.</summary>
        public const int NumClusters = 16;

        /// <summary>Default sample count: 0.</summary>
        public const int SampleCount = 0;
    }

    /// <summary>Default parameters for IVF (Inverted File) index.</summary>
    public static class Ivf
    {
        /// <summary>Default metric type: L2 (Euclidean) distance.</summary>
        public const ZVecMetricType MetricType = ZVecMetricType.L2;

        /// <summary>Default number of centroids: 256.</summary>
        public const int CentroidsNum = 256;

        /// <summary>Default nlist parameter (number of cluster units): 16.</summary>
        public const int Nlist = 16;

        /// <summary>Default nprobe parameter (number of clusters to query): 8.</summary>
        public const int Nprobe = 8;

        /// <summary>Default quantization type: Undefined.</summary>
        public const ZVecQuantizeType QuantizeType = ZVecQuantizeType.Undefined;
    }

    /// <summary>Default parameters for DiskANN index.</summary>
    public static class DiskAnn
    {
        /// <summary>Default metric type: L2 distance.</summary>
        public const ZVecMetricType MetricType = ZVecMetricType.L2;

        /// <summary>Default maximum out-degree of graph nodes: 100.</summary>
        public const int MaxDegree = 100;

        /// <summary>Default search list size during index construction: 50.</summary>
        public const int ListSize = 50;

        /// <summary>Default number of chunks for Product Quantization (PQ): 0.</summary>
        public const int PqChunkNum = 0;

        /// <summary>Default quantization type: Undefined.</summary>
        public const ZVecQuantizeType QuantizeType = ZVecQuantizeType.Undefined;
    }

    /// <summary>Default parameters for Vamana index.</summary>
    public static class Vamana
    {
        /// <summary>Default metric type: L2 distance.</summary>
        public const ZVecMetricType MetricType = ZVecMetricType.L2;

        /// <summary>Default maximum out-degree of graph nodes: 64.</summary>
        public const int MaxDegree = 64;

        /// <summary>Default search list size during search: 100.</summary>
        public const int SearchListSize = 100;

        /// <summary>Default scaling factor for search/construction: 1.2.</summary>
        public const float Alpha = 1.2f;

        /// <summary>Default quantization type: Undefined.</summary>
        public const ZVecQuantizeType QuantizeType = ZVecQuantizeType.Undefined;

        /// <summary>Default whether to saturate the search graph: false.</summary>
        public const bool SaturateGraph = false;

        /// <summary>Default whether to use contiguous memory layout: false.</summary>
        public const bool UseContiguousMemory = false;

        /// <summary>Default whether to use ID map: false.</summary>
        public const bool UseIdMap = false;
    }

    /// <summary>Default parameters for Flat (brute-force) index.</summary>
    public static class Flat
    {
        /// <summary>Default metric type: L2 distance.</summary>
        public const ZVecMetricType MetricType = ZVecMetricType.L2;

        /// <summary>Default quantization type: Undefined.</summary>
        public const ZVecQuantizeType QuantizeType = ZVecQuantizeType.Undefined;
    }

    /// <summary>Default parameters for Inverted index (scalars).</summary>
    public static class Invert
    {
        /// <summary>Default whether to enable range optimization: false.</summary>
        public const bool EnableRangeOptimization = false;

        /// <summary>Default whether to enable extended wildcard search: false.</summary>
        public const bool EnableExtendedWildcard = false;
    }

    /// <summary>Default parameters for Full-Text Search (FTS) index.</summary>
    public static class Fts
    {
        /// <summary>Default FTS tokenizer: Standard.</summary>
        public const ZVecFtsTokenizer Tokenizer = ZVecFtsTokenizer.Standard;

        /// <summary>Default FTS token filters: Lowercase.</summary>
        public static readonly IReadOnlyList<ZVecFtsTokenFilter> Filters = [ZVecFtsTokenFilter.Lowercase];
    }

    /// <summary>Default parameters for Collection schemas.</summary>
    public static class Collection
    {
        /// <summary>Default maximum documents per segment: 10,000,000.</summary>
        public const int MaxDocCountPerSegment = 10_000_000;

        /// <summary>Default maximum concurrent reads: 0 (uses Environment.ProcessorCount at open time).</summary>
        public const int MaxConcurrentReads = 0;
    }

    /// <summary>Default options for Collections.</summary>
    public static class CollectionOptions
    {
        /// <summary>Default read-only setting: false.</summary>
        public const bool ReadOnly = false;

        /// <summary>Default whether memory-mapped files (mmap) are enabled: true.</summary>
        public const bool EnableMmap = true;
    }

    /// <summary>Default parameters for Querying.</summary>
    public static class Query
    {
        /// <summary>Default group size for grouped queries: 1.</summary>
        public const int GroupSize = 1;

        /// <summary>Default top-K retrieval limit: 10.</summary>
        public const int Topk = 10;

        /// <summary>Default FTS operator: Or.</summary>
        public const ZVecFtsDefaultOperator DefaultOperator = ZVecFtsDefaultOperator.Or;
    }

    /// <summary>Default parameters for Reranking.</summary>
    public static class Rerank
    {
        /// <summary>Default rank constant for RRF reranking: 60.</summary>
        public const int RankConstant = 60;
    }
}
