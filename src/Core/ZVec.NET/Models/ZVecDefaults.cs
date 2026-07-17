namespace ZVec.NET;

/// <summary>
/// Named SDK defaults aligned with native ZVec configurations.
/// Centralizes all default parameters for collection options, indices, and querying.
/// </summary>
public static class ZVecDefaults
{
    /// <summary>
    /// Mutable ABI gate overrides for tests. Defaults forward to <see cref="ZVecNativeAbi"/>.
    /// Production code should prefer <see cref="ZVecNativeAbi"/> constants.
    /// </summary>
    public static class Version
    {
        /// <summary>Minimum major version required by the ABI gate. Defaults to <see cref="ZVecNativeAbi.MinimumMajor"/>.</summary>
        public static int ExpectedMajor { get; set; } = ZVecNativeAbi.MinimumMajor;

        /// <summary>Minimum minor version required by the ABI gate. Defaults to <see cref="ZVecNativeAbi.MinimumMinor"/>.</summary>
        public static int ExpectedMinor { get; set; } = ZVecNativeAbi.MinimumMinor;

        /// <summary>Minimum patch version required by the ABI gate. Defaults to <see cref="ZVecNativeAbi.MinimumPatch"/>.</summary>
        public static int ExpectedPatch { get; set; } = ZVecNativeAbi.MinimumPatch;

        /// <summary>If true, bypasses the ABI version check during initialization.</summary>
        public static bool BypassAbiCheck { get; set; }
    }

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

        /// <summary>Default maximum concurrent reads: 0 (unlimited).</summary>
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

    /// <summary>Filter expression tokens and formatting characters for <c>ZVecFilterBuilder</c>.</summary>
    public static class Filter
    {
        /// <summary>Logical AND keyword.</summary>
        public const string And = "AND";

        /// <summary>Logical OR keyword.</summary>
        public const string Or = "OR";

        /// <summary>Logical NOT keyword.</summary>
        public const string Not = "NOT";

        /// <summary>IN membership keyword.</summary>
        public const string In = "IN";

        /// <summary>LIKE pattern keyword.</summary>
        public const string Like = "LIKE";

        /// <summary>CONTAIN_ANY array-intersection keyword.</summary>
        public const string ContainAny = "CONTAIN_ANY";

        /// <summary>CONTAIN_ALL array-subset keyword.</summary>
        public const string ContainAll = "CONTAIN_ALL";

        /// <summary>Equality operator.</summary>
        public const string Eq = "=";

        /// <summary>Inequality operator.</summary>
        public const string Ne = "!=";

        /// <summary>Greater-than operator.</summary>
        public const string Gt = ">";

        /// <summary>Less-than operator.</summary>
        public const string Lt = "<";

        /// <summary>Greater-than-or-equal operator.</summary>
        public const string Ge = ">=";

        /// <summary>Less-than-or-equal operator.</summary>
        public const string Le = "<=";

        /// <summary>Boolean true literal.</summary>
        public const string True = "true";

        /// <summary>Boolean false literal.</summary>
        public const string False = "false";

        /// <summary>Null literal.</summary>
        public const string Null = "null";

        /// <summary>Space separator between tokens.</summary>
        public const string Space = " ";

        /// <summary>Comma separator between list values.</summary>
        public const string Comma = ",";

        /// <summary>Comma followed by space between list values.</summary>
        public const string CommaSpace = ", ";

        /// <summary>Opening parenthesis.</summary>
        public const string OpenParen = "(";

        /// <summary>Closing parenthesis.</summary>
        public const string CloseParen = ")";

        /// <summary>Opening square bracket.</summary>
        public const string OpenBracket = "[";

        /// <summary>Closing square bracket.</summary>
        public const string CloseBracket = "]";

        /// <summary>Double-quote character used to delimit string literals.</summary>
        public const char DoubleQuote = '"';

        /// <summary>Single-quote character that must be escaped inside string literals.</summary>
        public const char SingleQuote = '\'';

        /// <summary>Backslash escape character.</summary>
        public const char Backslash = '\\';
    }

    /// <summary>Default parameters for Reranking.</summary>
    public static class Rerank
    {
        /// <summary>Default rank constant for RRF reranking: 60.</summary>
        public const int RankConstant = 60;
    }

    /// <summary>Default options for process-wide initialization.</summary>
    public static class GlobalOptions
    {
        /// <summary>Default log type: Console.</summary>
        public const ZVecLogType LogType = ZVecLogType.Console;

        /// <summary>Default log level: Warn.</summary>
        public const ZVecLogLevel LogLevel = ZVecLogLevel.Warn;

        /// <summary>Default query threads: -1 (auto).</summary>
        public const int QueryThreads = -1;

        /// <summary>Default max concurrent native calls: 0 (unlimited).</summary>
        public const int MaxConcurrentNativeCalls = 0;
    }

    /// <summary>Centralized error message strings.</summary>
    public static class Errors
    {
        /// <summary>Message shown when write lock is re-entered on the same context.</summary>
        public const string WriteLockReentrancyNotSupported = "Write lock reentrancy is not supported.";

        /// <summary>Message shown when a collection operation is attempted before the factory is initialized.</summary>
        public const string FactoryNotInitialized = "ZVecFactory is not initialized. Call Initialize() or InitializeAsync() first.";        

        /// <summary>Message shown when Destroy is called more than once on a collection.</summary>
        public const string CollectionAlreadyDestroyed = "This collection has already been destroyed.";        

        /// <summary>Message shown when the native library fails to allocate a document.</summary>
        public const string NativeDocCreateFailed = "Failed to create native document (zvec_doc_create returned null).";

        /// <summary>Message shown when an unsupported data type is passed to a scalar field.</summary>
        public const string NativeDataTypeNotSupported = "Data type {0} is not supported for scalar fields.";

        /// <summary>Message shown when the native library fails to allocate a vector query.</summary>
        public const string NativeQueryCreateFailed = "Failed to create native vector query (zvec_vector_query_create returned null).";

        /// <summary>Message shown when the native library fails to allocate index parameters.</summary>
        public const string NativeIndexParamsCreateFailed = "Failed to create native index parameters (zvec_index_params_create returned null).";

        /// <summary>Message shown when the native library fails to allocate field schema.</summary>
        public const string NativeFieldSchemaCreateFailed = "Failed to create native field schema (zvec_field_schema_create returned null).";

        /// <summary>Message shown when the native library fails to allocate collection schema.</summary>
        public const string NativeCollectionSchemaCreateFailed = "Failed to create native collection schema (zvec_collection_schema_create returned null).";

        /// <summary>Message shown when the native library fails to allocate collection options.</summary>
        public const string NativeCollectionOptionsCreateFailed = "Failed to create native collection options (zvec_collection_options_create returned null).";

        /// <summary>Message shown when trying to register a collection via DI without a path.</summary>
        public const string CollectionPathRequired = "Collection path must be provided.";

        /// <summary>Message shown when the native library fails to allocate a multi-query.</summary>
        public const string NativeMultiQueryCreateFailed = "Failed to create native multi-query.";

        /// <summary>Message shown when the native library fails to allocate a sub-query.</summary>
        public const string NativeSubQueryCreateFailed = "Failed to create native sub-query.";

        /// <summary>Message shown when the native library fails to allocate an FTS query handle.</summary>
        public const string NativeFtsQueryCreateFailed = "Failed to create native FTS query handle.";

        /// <summary>Message shown when a collection/field/vector name is null, empty, or whitespace.</summary>
        public const string NameRequired = "Name cannot be empty or whitespace.";

        /// <summary>Message shown when a vector dimension is not strictly positive.</summary>
        public const string VectorDimensionMustBePositive = "Vector dimension must be strictly positive.";

        /// <summary>Message shown when max document count per segment is not strictly positive.</summary>
        public const string MaxDocCountPerSegmentMustBePositive = "Max document count per segment must be strictly positive.";

        /// <summary>Message shown when <c>ZVecFilterBuilder.Where</c> receives an unsupported compare operator.</summary>
        public const string UnsupportedCompareOp = "Unsupported compare operator {0}.";

        /// <summary>Message shown when a filter field name is null, empty, or whitespace.</summary>
        public const string FilterFieldNameRequired = "Filter field name cannot be empty or whitespace.";

        /// <summary>Message shown when a LIKE pattern is null.</summary>
        public const string FilterLikePatternRequired = "LIKE pattern cannot be null.";

        /// <summary>Message shown when an IN/CONTAIN values array is null.</summary>
        public const string FilterValuesRequired = "Values cannot be null.";

        /// <summary>Message shown when Not() cannot rewrite an expression into a native-supported form.</summary>
        public const string FilterNotUnsupported =
            "Unary NOT is not supported by the native ZVec filter engine. Use !=, NOT IN, or NOT CONTAIN_* forms, or pass a simple comparison/In/Contain expression to Not().";

        /// <summary>
        /// Platform gate: HNSW-RaBitQ requires x86_64 with AVX2 or higher (not available on ARM).
        /// </summary>
        public const string RabitqRequiresX64Avx2 =
            "HNSW-RaBitQ is currently supported only on x86_64 with AVX2 or higher instruction set support. It is not available on ARM architectures.";

        /// <summary>
        /// Platform gate: DiskANN requires Linux and libaio.
        /// </summary>
        public const string DiskAnnRequiresLinuxLibaio =
            "DiskANN is currently supported on Linux only and requires the libaio library (Linux asynchronous I/O) to be installed on the system.";

        /// <summary>ABI mismatch message format: requires &gt;= min with major == requiredMajor. Args: minVersion, requiredMajor, foundVersion.</summary>
        public const string AbiMismatchRequiresMinSameMajor =
            "ZVec ABI version mismatch. Requires native library version >= '{0}' with major == {1}, but found: '{2}'. Please update your native binaries.";
    }
}
