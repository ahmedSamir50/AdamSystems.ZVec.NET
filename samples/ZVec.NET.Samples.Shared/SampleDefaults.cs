namespace ZVec.NET.Samples.Shared;

/// <summary>Central defaults for ZVec.NET sample apps (LM Studio, chunking, MB dataset caps).</summary>
public static class SampleDefaults
{
    /// <summary>EmbeddingGemma native output size (768-d MRL).</summary>
    public const int VectorDimensions = 768;

    public const string LmStudioBaseUrl = "http://127.0.0.1:1234/v1";
    public const string EmbeddingModelId = "text-embedding-google_embeddinggemma-300m-qat";
    public const string ChatModelId = "google/gemma-4-e2b";

    public const int ChunkMaxChars = 800;
    public const int ChunkOverlapChars = 120;
    public const int DefaultTopK = 5;

    public const int MaxPackBytes = 100 * 1024 * 1024;
    public const int PreferredPackBytes = 50 * 1024 * 1024;

    public const int QuoraMaxUniqueQuestions = 50_000;
    public const int AmazonBeautyMaxItems = 25_000;
    public const int SimpleWikiMaxArticles = 10_000;

    public const int MemoryLimitMb = 512;
    public const bool EnableMmap = true;

    public const string RagCollectionFolder = "zvec-samples-rag";
    public const string SearchCollectionFolder = "zvec-samples-search";
    public const string RecommendCollectionFolder = "zvec-samples-recommend";

    public const string LmStudioDownMessage =
        "LM Studio is not reachable at " + LmStudioBaseUrl +
        ". Start LM Studio with both models loaded: embedding (" + EmbeddingModelId +
        ") and chat (" + ChatModelId + "). Both can run concurrently.";

    public const string DimMismatchFormat =
        "Embedding length {0} does not match SampleDefaults.VectorDimensions ({1}). " +
        "Align your LM Studio embedding model with [ZVecVector({1})].";
}
