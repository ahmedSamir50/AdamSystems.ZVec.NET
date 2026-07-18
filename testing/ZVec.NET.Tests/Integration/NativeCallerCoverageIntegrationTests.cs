using FluentAssertions;
using ZVec.NET.Exceptions;
using ZVec.NET.Query;

namespace ZVec.NET.Tests.Integration;

/// <summary>
/// Broad native-caller smoke for production NativeMethods paths that other suites may miss.
/// Each fact exercises real DLL entry points via managed APIs (not TryGetExport).
/// </summary>
public class NativeCallerCoverageIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private IZvecFactory? _factory;
    private IZvecCollection? _collection;
    private readonly List<string> _extraPaths = [];

    public NativeCallerCoverageIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_caller_cov_{Guid.NewGuid():N}");
    }

    [Fact]
    public void Upsert_DeleteWithResults_Optimize_DropIndex_Succeed()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_cov",
            Fields =
            [
                new ZVecFieldSchema { Name = "body", DataType = ZVecDataType.String }
            ],
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecIvfIndexParam { CentroidsNum = 4 }
                }
            ]
        };

        _collection = _factory.CreateAndOpen(_testPath, schema);

        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var doc = ZVecDoc.Create("c1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["body"] = "hello" });

        _collection.Upsert(doc).IsSuccess.Should().BeTrue();
        _collection.Upsert(doc).IsSuccess.Should().BeTrue();

        _collection.GetStats().DocCount.Should().BeGreaterThanOrEqualTo(1);

        var deleteResults = _collection.DeleteWithResults(["c1"]);
        deleteResults.Should().NotBeEmpty();

        _collection.Optimize();
        _collection.DropIndex("embedding");
        _collection.CreateIndex("embedding", new ZVecFlatIndexParam());
    }

    [Fact]
    public void InsertWithResults_And_DeleteByFilter_Succeed()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var path = _testPath + "_crud_extra";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_crud_extra",
            Fields =
            [
                new ZVecFieldSchema
                {
                    Name = "category",
                    DataType = ZVecDataType.String,
                    IndexParam = new ZVecInvertIndexParam()
                }
            ],
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                }
            ]
        };

        using var col = _factory.CreateAndOpen(path, schema);
        var vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f };
        var docs = new[]
        {
            ZVecDoc.Create("a1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                fields: new Dictionary<string, object> { ["category"] = "keep" }),
            ZVecDoc.Create("a2",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
                fields: new Dictionary<string, object> { ["category"] = "drop" })
        };

        var writeResults = col.InsertWithResults(docs);
        writeResults.Should().HaveCount(2);
        writeResults.Should().OnlyContain(r => r.Code == ZVecErrorCode.Ok);

        var filter = ZVecFilterBuilder.Create()
            .Where("category", ZVecCompareOp.Eq, "drop")
            .Build();
        col.DeleteByFilter(filter).IsSuccess.Should().BeTrue();

        col.Fetch("a1").Should().NotBeNull();
        col.Fetch("a2").Should().BeNull();
    }

    [Fact]
    public void Initialize_With_QueryThreads_MemoryLimit_And_FileLog_Succeeds()
    {
        _fixture.SkipIfNotAvailable();

        var logDir = Path.Combine(Path.GetTempPath(), $"zvec_caller_logs_{Guid.NewGuid():N}");
        Directory.CreateDirectory(logDir);
        _extraPaths.Add(logDir);

        var path = _testPath + "_opts";
        _extraPaths.Add(path);

        _factory = new ZVecFactory();
        _factory.Initialize(new ZVecOptions
        {
            QueryThreads = 2,
            MemoryLimitMb = 256,
            LogType = ZVecLogType.File,
            LogLevel = ZVecLogLevel.Warn,
            LogDir = logDir,
            LogBasename = "caller_cov",
            LogFileSizeMb = 1,
            LogOverdueDays = 1
        });

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_opts",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                }
            ]
        };

        using var col = _factory.CreateAndOpen(path, schema);
        col.Insert(ZVecDoc.Create("o1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["embedding"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
            })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Fts_Index_Param_And_Query_Path_Succeed()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var ftsPath = _testPath + "_fts";
        _extraPaths.Add(ftsPath);

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_fts",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Standard,
                        Filters = [ZVecFtsTokenFilter.Lowercase]
                    }
                }
            ]
        };

        using var col = _factory.CreateAndOpen(ftsPath, schema);
        col.Insert(ZVecDoc.Create("f1",
            fields: new Dictionary<string, object> { ["content"] = "alpha beta gamma" }))
            .IsSuccess.Should().BeTrue();

        var results = col.Query(new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery { QueryString = "alpha" }
        }, topk: 5);

        results.Should().NotBeNull();
    }

    [Fact]
    public void Fts_MatchString_Only_Succeeds()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var path = _testPath + "_fts_match";
        _extraPaths.Add(path);

        // FTS-only schema (MatchString is mutually exclusive with QueryString).
        var schema = new ZVecCollectionSchema
        {
            Name = "caller_fts_match",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Standard,
                        Filters = [ZVecFtsTokenFilter.Lowercase]
                    }
                }
            ]
        };

        using var col = _factory.CreateAndOpen(path, schema);
        col.Insert(ZVecDoc.Create("m1",
            fields: new Dictionary<string, object> { ["content"] = "alpha beta gamma" }))
            .IsSuccess.Should().BeTrue();

        var results = col.Query(new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery
            {
                MatchString = "alpha",
                DefaultOperator = ZVecFtsDefaultOperator.And
            }
        }, topk: 5);

        results.Should().NotBeNull();
    }

    [Fact]
    public void MultiQuery_Fts_SubQuery_And_WeightedRerank_Succeed()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var path = _testPath + "_hybrid_fts";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_hybrid_fts",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                },
                new ZVecVectorSchema
                {
                    Name = "content",
                    DataType = ZVecDataType.String,
                    Dimension = 0,
                    IndexParam = new ZVecFtsIndexParam
                    {
                        Tokenizer = ZVecFtsTokenizer.Standard,
                        Filters = [ZVecFtsTokenFilter.Lowercase]
                    }
                }
            ]
        };

        using var col = _factory.CreateAndOpen(path, schema);
        var vector = new float[] { 1f, 0f, 0f, 0f };
        col.Insert(ZVecDoc.Create("h1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vector },
            fields: new Dictionary<string, object> { ["content"] = "alpha beta gamma" }))
            .IsSuccess.Should().BeTrue();

        var denseQ = new ZVecQuery
        {
            FieldName = "embedding",
            Vector = vector
        };
        var ftsQ = new ZVecQuery
        {
            FieldName = "content",
            Fts = new ZVecFtsQuery
            {
                QueryString = "alpha",
                DefaultOperator = ZVecFtsDefaultOperator.Or
            }
        };
        var weighted = new ZVecWeightedReranker
        {
            TopN = 5,
            Weights = new Dictionary<string, float>
            {
                ["embedding"] = 0.6f,
                ["content"] = 0.4f
            }
        };

        var multi = col.Query([denseQ, ftsQ], topk: 5, reranker: weighted);
        multi.Should().NotBeNull();
    }

    [Fact]
    public void Vamana_Index_CreateAndOpen_Succeeds()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var path = _testPath + "_vamana";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_vamana",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecVamanaIndexParam
                    {
                        MaxDegree = 16,
                        SearchListSize = 32
                    }
                }
            ]
        };

        using var col = _factory.CreateAndOpen(path, schema);
        col.Insert(ZVecDoc.Create("v1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["embedding"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
            })).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void DiskAnn_Index_CreateAndOpen_Succeeds_OnLinuxOnly()
    {
        _fixture.SkipIfNotAvailable();
        if (!OperatingSystem.IsLinux())
            Assert.Skip("DiskANN native path requires Linux + libaio");

        _factory = new ZVecFactory();
        _factory.Initialize();

        var path = _testPath + "_diskann";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_diskann",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecDiskAnnIndexParam()
                }
            ]
        };

        try
        {
            using var col = _factory.CreateAndOpen(path, schema);
            col.Insert(ZVecDoc.Create("d1",
                denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
                {
                    ["embedding"] = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
                })).IsSuccess.Should().BeTrue();
        }
        catch (ZVecNativeException ex) when (ex.ErrorCode == ZVecErrorCode.NotSupported)
        {
            // Upstream DiskANN may refuse some hosts even on Linux (e.g. missing libaio / runtime init).
            Assert.Skip($"DiskANN not usable on this host: {ex.NativeErrorMessage}");
        }
    }

    [Fact]
    public void Insert_WrongDimension_Throws_ZVecNativeException_With_NativeMessage()
    {
        _fixture.SkipIfNotAvailable();
        _factory = new ZVecFactory();
        _factory.Initialize();

        var path = _testPath + "_err";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "caller_err",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecFlatIndexParam()
                }
            ]
        };

        using var col = _factory.CreateAndOpen(path, schema);
        var act = () => col.Insert(ZVecDoc.Create("bad",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>>
            {
                ["embedding"] = new float[] { 0.1f, 0.2f } // wrong dim
            }));

        act.Should().Throw<ZVecNativeException>()
            .Which.NativeErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    public void Dispose()
    {
        _collection?.Dispose();
        _factory?.Dispose();
        TryDelete(_testPath);
        foreach (var p in _extraPaths)
            TryDelete(p);
    }

    private static void TryDelete(string path)
    {
        if (!Directory.Exists(path)) return;
        try { Directory.Delete(path, true); }
        catch { /* ignore */ }
    }
}
