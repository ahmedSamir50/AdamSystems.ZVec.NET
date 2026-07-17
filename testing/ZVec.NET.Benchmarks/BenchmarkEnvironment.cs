using ZVec.NET.Query;

namespace ZVec.NET.Benchmarks;

internal static class BenchmarkEnvironment
{
    public const int EmbeddingDimension = 768;
    public const string VectorField = "vec";
    public const string ContentField = "content";
    public const int SeedDocCount = 128;
    public const int BatchInsertSize = 64;

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
        var docs = new ZVecDoc[count];
        for (int i = 0; i < count; i++)
            docs[i] = CreateDoc($"seed_{i:D4}", vector, $"seed document {i}");

        collection.Insert(docs);
    }

    public static ZVecFilterBuilder SampleFilter() =>
        ZVecFilterBuilder.Create()
            .Where(ContentField, ZVecCompareOp.Eq, "seed document 0");
}
