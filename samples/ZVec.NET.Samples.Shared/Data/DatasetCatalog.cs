namespace ZVec.NET.Samples.Shared.Data;

/// <summary>Enforces MB-only dataset policy for sample loaders.</summary>
public static class DatasetCatalog
{
    public const string Fiqa = "fiqa";
    public const string NfCorpus = "nfcorpus";
    public const string Quora = "quora";
    public const string MovieLens = "movielens-small";
    public const string AmazonBeauty = "amazon-beauty";

    public static void EnsureWithinMbBudget(string path, long maxBytes = SampleDefaults.MaxPackBytes)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            throw new FileNotFoundException($"Dataset path not found: {path}");

        long size = File.Exists(path)
            ? new FileInfo(path).Length
            : Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length);

        if (size > maxBytes)
        {
            throw new InvalidOperationException(
                $"Dataset at '{path}' is {size / (1024.0 * 1024.0):F1} MB, which exceeds the sample MB budget " +
                $"({maxBytes / (1024.0 * 1024.0):F0} MB). ZVec.NET samples never use GB-scale corpora.");
        }
    }

    public static string Attribution(string datasetId) => datasetId switch
    {
        Fiqa => "BEIR FiQA-2018 (https://huggingface.co/datasets/BeIR/fiqa) — research/demo use; see dataset card for license.",
        NfCorpus => "BEIR NFCorpus (https://huggingface.co/datasets/BeIR/nfcorpus) — research/demo use; see dataset card for license.",
        Quora => "Quora Question Pairs — research use per Quora 2017 release terms; unique questions capped for MB budget.",
        MovieLens => "MovieLens latest-small — GroupLens (https://grouplens.org/datasets/movielens/).",
        AmazonBeauty => "Amazon Reviews 2023 raw_meta_All_Beauty (McAuley Lab) — capped item export for MB budget.",
        _ => datasetId
    };
}
