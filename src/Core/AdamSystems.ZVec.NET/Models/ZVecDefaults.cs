namespace AdamSystems.ZVec.NET;

/// <summary>
/// Named SDK defaults aligned with native / Python parity.
/// Prefer these over magic numbers in callers and property initializers.
/// </summary>
public static class ZVecDefaults
{
    public static class Hnsw
    {
        public const int M = 16;
        public const int EfConstruction = 200;
    }

    public static class HnswRabitq
    {
        public const int M = 16;
        public const int EfConstruction = 200;
        public const int TotalBits = 7;
        public const int NumClusters = 16;
        public const int SampleCount = 0;
    }

    public static class Ivf
    {
        public const int CentroidsNum = 256;
        public const int Nlist = 16;
        public const int Nprobe = 8;
    }

    public static class DiskAnn
    {
        public const int MaxDegree = 100;
        public const int ListSize = 50;
        public const int PqChunkNum = 0;
    }

    public static class Vamana
    {
        public const int MaxDegree = 64;
        public const int SearchListSize = 100;
        public const float Alpha = 1.2f;
    }

    public static class Fts
    {
        public const ZVecFtsTokenizer Tokenizer = ZVecFtsTokenizer.Standard;
        public static readonly IReadOnlyList<ZVecFtsTokenFilter> Filters = [ZVecFtsTokenFilter.Lowercase];
    }

    public static class Collection
    {
        public const int MaxDocCountPerSegment = 10_000_000;

        /// <summary>0 = use <see cref="Environment.ProcessorCount"/> at open time.</summary>
        public const int MaxConcurrentReads = 0;
    }

    public static class Query
    {
        public const int GroupSize = 1;
        public const int Topk = 10;
    }

    public static class Rerank
    {
        public const int RankConstant = 60;
    }
}
