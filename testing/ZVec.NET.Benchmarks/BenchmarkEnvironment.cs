using ZVec.NET.Query;

namespace ZVec.NET.Benchmarks;

internal static class BenchmarkEnvironment
{
    public const int EmbeddingDimension = 768;
    public const string VectorField = "vec";
    public const string ContentField = "content";

    /// <summary>Primary corpus for query/memory benches (README single-vector / allocation targets).</summary>
    public const int SeedDocCount = 10_000;

    /// <summary>Small corpus for warm-query latency (README “small corpus” row — not pure P/Invoke).</summary>
    public const int TinyCorpusSeedCount = 128;

    /// <summary>Batch size for insert throughput (README &gt; 50k docs/sec target).</summary>
    public const int BatchInsertSize = 1000;

    /// <summary>
    /// Binding-suite corpus size. Distinct from upstream VectorDBBench Cohere 1M/10M
    /// (<see cref="UpstreamEngineScaleBaseline"/>).
    /// </summary>
    public const string WorkloadLabel = "768-dim Flat / 10k docs / batch 1000 (SDK binding suite)";

    public static bool TryInitialize(out ZVecFactory factory)
    {
        factory = new ZVecFactory();
        try
        {
            factory.Initialize();
            return true;
        }
        catch (DllNotFoundException)
        {
            Console.WriteLine("WARNING: zvec_c_api not found. Native benchmarks will no-op.");
            return false;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.WriteLine($"WARNING: ZVec initialization failed: {ex.Message}");
            return false;
        }
    }

    public static ZVecCollectionSchema CreateSchema(string name = "benchmarks") =>
        new()
        {
            Name = name,
            Fields =
            [
                new ZVecFieldSchema { Name = ContentField, DataType = ZVecDataType.String }
            ],
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = VectorField,
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = EmbeddingDimension,
                    IndexParam = new ZVecFlatIndexParam { MetricType = ZVecDefaults.Flat.MetricType }
                }
            ]
        };

    public static float[] CreateVector(float fill = 0.5f)
    {
        var vector = new float[EmbeddingDimension];
        Array.Fill(vector, fill);
        return vector;
    }

    public static ZVecDoc CreateDoc(string id, float[] vector, string content) =>
        ZVecDoc.Create(id,
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { [VectorField] = vector },
            fields: new Dictionary<string, object> { [ContentField] = content });

    public static void SeedCollection(IZvecCollection collection, float[] vector, int count)
    {
        const int chunkSize = 1000;
        for (int offset = 0; offset < count; offset += chunkSize)
        {
            int n = Math.Min(chunkSize, count - offset);
            var docs = new ZVecDoc[n];
            for (int i = 0; i < n; i++)
            {
                int id = offset + i;
                docs[i] = CreateDoc($"seed_{id:D5}", vector, $"seed document {id}");
            }

            collection.Insert(docs);
        }
    }

    public static ZVecFilterBuilder SampleFilter() =>
        ZVecFilterBuilder.Create()
            .Where(ContentField, ZVecCompareOp.Eq, "seed document 0");
}
