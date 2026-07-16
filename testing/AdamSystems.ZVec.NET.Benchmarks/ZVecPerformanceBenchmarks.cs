using BenchmarkDotNet.Attributes;
using System.Text.Json;
using System;
using System.Collections.Generic;

namespace AdamSystems.ZVec.NET.Benchmarks;

[MemoryDiagnoser]
public class ZVecPerformanceBenchmarks
{
    private ZVecFactory _factory = null!;
    private IZvecCollection _collection = null!;
    private ZVecDoc _docToInsert = null!;
    private ZVecQuery _query = null!;

    [GlobalSetup]
    public void Setup()
    {
        _factory = new ZVecFactory();
        
        try
        {
            _factory.Initialize();
        }
        catch (DllNotFoundException)
        {
            // Native library not available in environment. Benchmarks will fail.
            Console.WriteLine("WARNING: zvec_c_api not found. Benchmarks will crash.");
            return;
        }

        var schema = new ZVecCollectionSchema
        {
            Name = "benchmarks",
            Fields = new List<ZVecFieldSchema>
            {
                new ZVecFieldSchema { Name = "id", DataType = ZVecDataType.String },
                new ZVecFieldSchema { Name = "content", DataType = ZVecDataType.String }
            },
            Vectors = new List<ZVecVectorSchema>
            {
                new ZVecVectorSchema 
                { 
                    Name = "vec", 
                    DataType = ZVecDataType.VectorFp32, 
                    Dimension = 128, 
                    IndexParam = new ZVecFlatIndexParam() 
                }
            }
        };

        // Create a memory collection to avoid IO overhead during benchmark
        var tempPath = $"./benchmark_{Guid.NewGuid():N}";
        _collection = _factory.CreateAndOpen(tempPath, schema);

        // Prepare a document
        var floats = new float[128];
        for (int i = 0; i < 128; i++) floats[i] = 0.5f;

        var denseVectors = new Dictionary<string, ReadOnlyMemory<float>> { { "vec", floats } };
        var fields = new Dictionary<string, object> { { "content", "this is a benchmark document" } };

        _docToInsert = ZVecDoc.Create("test_doc", denseVectors: denseVectors, fields: fields);

        // Prepare a query
        _query = new ZVecQuery
        {
            FieldName = "vec",
            Vector = floats
        };
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (_collection != null)
        {
            try { _collection.Destroy(); } catch { }
        }
        _factory?.Shutdown();
    }

    [Benchmark]
    public void InsertDocument()
    {
        if (_factory.IsInitialized)
        {
            _collection.Insert(_docToInsert);
        }
    }

    [Benchmark]
    public IReadOnlyList<ZVecDoc> QueryVector()
    {
        if (_factory.IsInitialized)
        {
            return _collection.Query(_query, topk: 10);
        }
        return [];
    }
}
