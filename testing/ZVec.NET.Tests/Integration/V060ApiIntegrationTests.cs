using FluentAssertions;
using ZVec.NET.Internal;
using ZVec.NET.Interop;

namespace ZVec.NET.Tests.Integration;

[Collection(nameof(NativeSessionCollection))]
public class V060ApiIntegrationTests : IClassFixture<ZVecRealNativeFixture>, IDisposable
{
    private readonly ZVecRealNativeFixture _fixture;
    private readonly string _testPath;
    private readonly List<string> _extraPaths = [];
    private IZvecFactory? _factory;

    public V060ApiIntegrationTests(ZVecRealNativeFixture fixture)
    {
        _fixture = fixture;
        _testPath = Path.Combine(Path.GetTempPath(), $"zvec_v060_{Guid.NewGuid():N}");
    }

    private IZvecFactory EnsureFactory()
    {
        _fixture.SkipIfNotAvailable();
        _factory ??= new ZVecFactory();
        _factory.Initialize();
        return _factory;
    }

    [Fact]
    public void Native_Version_Is_At_Least_0_6_0()
    {
        var factory = EnsureFactory();
        factory.GetNativeVersion().Should().MatchRegex(@"^v?0\.7(\.|$)");
        NativeMethods.zvec_get_version_major().Should().Be(0);
        NativeMethods.zvec_get_version_minor().Should().BeGreaterThanOrEqualTo(7);
    }

    [Fact]
    public void EnableRotate_Int8_Flat_CreateInsertQuery_Succeeds()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_rotate";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "rotate_int8",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 8,
                    IndexParam = new ZVecFlatIndexParam
                    {
                        QuantizeType = ZVecQuantizeType.Int8,
                        EnableRotate = true
                    }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = [0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f];
        col.Insert(ZVecDoc.Create("r1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery { FieldName = "embedding", Vector = vec },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("r1");
    }

    [Fact]
    public void FlatQueryParams_Radius_Linear_Succeeds()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_flatqp";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "flat_qp",
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

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = [1f, 0f, 0f, 0f];
        col.Insert(ZVecDoc.Create("f1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "embedding",
                Vector = vec,
                QueryParams = new ZVecFlatQueryParams
                {
                    Radius = 0f,
                    IsLinear = true,
                    ScaleFactor = 10f
                }
            },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("f1");
    }

    [Fact]
    public void HnswQueryParams_ExtendedOptions_Succeeds()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_hnswqp";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "hnsw_qp",
            Vectors =
            [
                new ZVecVectorSchema
                {
                    Name = "embedding",
                    DataType = ZVecDataType.VectorFp32,
                    Dimension = 4,
                    IndexParam = new ZVecHnswIndexParam { M = 8, EfConstruction = 64 }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = [0.2f, 0.3f, 0.4f, 0.5f];
        col.Insert(ZVecDoc.Create("h1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "embedding",
                Vector = vec,
                QueryParams = new ZVecHnswQueryParams
                {
                    EfSearch = 64,
                    IsLinear = false,
                    Radius = 0f
                }
            },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("h1");
    }

    [Fact]
    public void VamanaQueryParams_Succeeds()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_vamanaqp";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "vamana_qp",
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

        using var col = factory.CreateAndOpen(path, schema);
        float[] vec = [0.1f, 0.2f, 0.3f, 0.4f];
        col.Insert(ZVecDoc.Create("v1",
            denseVectors: new Dictionary<string, ReadOnlyMemory<float>> { ["embedding"] = vec }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "embedding",
                Vector = vec,
                QueryParams = new ZVecVamanaQueryParams { EfSearch = 32 }
            },
            topk: 1,
            includeVector: false);
        hits.Should().ContainSingle().Which.Id.Should().Be("v1");
    }

    [Fact]
    public void GroupBy_Builder_CreateDestroy_Succeeds_Without_Execution()
    {
        EnsureFactory();

        var gq = new ZVecGroupByQuery
        {
            GroupByField = "category",
            GroupSize = 1,
            Topk = 2,
            Query = new ZVecQuery
            {
                FieldName = "embedding",
                Vector = new float[] { 0.1f, 0.2f, 0.3f, 0.4f }
            }
        };

        using var builder = new NativeGroupByQueryBuilder(gq, includeVector: false);
        builder.Handle.Should().NotBe(IntPtr.Zero);
    }

    [Fact]
    public void Fts_With_AsciiFolding_And_Stemmer_Recalls()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_fts_filters";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "fts_filters",
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
                        Filters =
                        [
                            ZVecFtsTokenFilter.Lowercase,
                            ZVecFtsTokenFilter.AsciiFolding,
                            ZVecFtsTokenFilter.Stemmer
                        ],
                        ExtraParams = new ZVecFtsExtraParams { StemmerLang = "english" }
                    }
                }
            ]
        };

        using var col = factory.CreateAndOpen(path, schema);
        col.Insert(ZVecDoc.Create("doc1",
            fields: new Dictionary<string, object> { ["content"] = "Running vectors through databases" }))
            .IsSuccess.Should().BeTrue();

        var hits = col.Query(
            new ZVecQuery
            {
                FieldName = "content",
                Fts = new ZVecFtsQuery
                {
                    QueryString = "run database",
                    DefaultOperator = ZVecFtsDefaultOperator.And
                }
            },
            topk: 5);
        hits.Should().NotBeEmpty();
        hits.Should().Contain(d => d.Id == "doc1");
    }

    [Fact]
    public void Fts_Reopen_Still_Queries()
    {
        var factory = EnsureFactory();
        var path = _testPath + "_fts_reopen";
        _extraPaths.Add(path);

        var schema = new ZVecCollectionSchema
        {
            Name = "fts_reopen",
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

        using (var col = factory.CreateAndOpen(path, schema))
        {
            col.Insert(ZVecDoc.Create("p1",
                fields: new Dictionary<string, object> { ["content"] = "persistent full text search" }))
                .IsSuccess.Should().BeTrue();
        }

        using var reopened = factory.Open(path);
        var hits = reopened.Query(
            new ZVecQuery
            {
                FieldName = "content",
                Fts = new ZVecFtsQuery { QueryString = "persistent", DefaultOperator = ZVecFtsDefaultOperator.And }
            },
            topk: 5);
        hits.Should().Contain(d => d.Id == "p1");
    }

    public void Dispose()
    {
        _factory?.Shutdown();
        TryDelete(_testPath);
        foreach (var p in _extraPaths)
            TryDelete(p);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
