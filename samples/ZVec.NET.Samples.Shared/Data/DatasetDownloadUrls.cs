namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Known public download URLs for MB-capped sample datasets (not committed to the repo).</summary>
public static class DatasetDownloadUrls
{
    public const string FiqaZip =
        "https://public.ukp.informatik.tu-darmstadt.de/thakur/BEIR/datasets/fiqa.zip";

    public const string NfCorpusZip =
        "https://public.ukp.informatik.tu-darmstadt.de/thakur/BEIR/datasets/nfcorpus.zip";

    public const string MovieLensSmallZip =
        "https://files.grouplens.org/datasets/movielens/ml-latest-small.zip";

    public const string QuoraTsv =
        "https://huggingface.co/datasets/aisuko/quora_duplicate_questions/resolve/main/quora_duplicate_questions.tsv";

    /// <summary>
    /// Amazon All_Beauty metadata JSONL (gzip). We stream and stop after the item/MB cap —
    /// never keep a GB-scale extract.
    /// </summary>
    public const string AmazonBeautyMetaJsonlGz =
        "https://datarepo.eng.ucsd.edu/mcauley_group/data/amazon_2023/raw/meta_categories/meta_All_Beauty.jsonl.gz";
}
